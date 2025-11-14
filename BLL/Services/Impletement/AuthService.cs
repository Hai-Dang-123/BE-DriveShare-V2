using BLL.Services.Interface;
using BLL.Utilities;
using Common.Constants;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using Common.Settings;
using Common.ValueObjects;
using DAL.Entities;
using DAL.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        private readonly IFirebaseUploadService _firebaseUploadService;
        public AuthService (IUnitOfWork unitOfWork, UserUtility userUtility, IFirebaseUploadService firebaseUploadService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _firebaseUploadService = firebaseUploadService;
        }

        public async Task<ResponseDTO> LoginAsync(LoginDTO dto)
        {
            // kiểm tra email
            var user = await _unitOfWork.BaseUserRepo.FindByEmailAsync(dto.Email);
            if (user == null)
            {
                return new ResponseDTO("User not found !!!", 404, false);
            }

            // kiểm tra mật khẩu
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!isPasswordValid)
            {
                return new ResponseDTO("Wrong email or password !!!", 400, false);
            }

            //kiểm tra token
            var exitsRefreshToken = await _unitOfWork.UserTokenRepo.GetRefreshTokenByUserID(user.UserId);
            if (exitsRefreshToken != null)
            {
                exitsRefreshToken.IsRevoked = true;
                await _unitOfWork.UserTokenRepo.UpdateAsync(exitsRefreshToken);
            }

            //khởi tạo claim
            var claims = new List<Claim>
            {
                new Claim(JwtConstant.KeyClaim.userId, user.UserId.ToString()),
                new Claim(JwtConstant.KeyClaim.fullName, user.FullName),
                new Claim(JwtConstant.KeyClaim.Role, user.Role.RoleName)
            };

            //tạo refesh token
            var refreshTokenKey = JwtProvider.GenerateRefreshToken(claims);
            var accessTokenKey = JwtProvider.GenerateAccessToken(claims);

            var refreshToken = new UserToken
            {
                UserTokenId = Guid.NewGuid(),
                TokenValue = refreshTokenKey,
                UserId = user.UserId,
                IsRevoked = false,
                TokenType = TokenType.REFRESH,
                CreatedAt = DateTime.UtcNow,
                ExpiredAt = DateTime.UtcNow.AddDays(JwtSettingModel.ExpireDayRefreshToken)
            };
            try
            {
                await _unitOfWork.UserTokenRepo.AddAsync(refreshToken);

                await _unitOfWork.SaveChangeAsync();
            }
            catch (Exception ex)
            {
                return new ResponseDTO("An error occurred. Please try again later.", 500, false);
            }


            return new ResponseDTO("Login Successfully !!!", 200, true, new
            {
                AccessToken = accessTokenKey,
                RefreshToken = refreshToken.TokenValue,
            });
        }

        public async Task<ResponseDTO> LogoutAsync()
        {
            var userId = _userUtility.GetUserIdFromToken();
            if (userId == Guid.Empty)
            {
                return new ResponseDTO("User is not found ", 400, false);
            }
            var existingToken = await _unitOfWork.UserTokenRepo.GetRefreshTokenByUserID(userId);
            if (existingToken == null || existingToken.IsRevoked || existingToken.TokenType != TokenType.REFRESH)
            {
                return new ResponseDTO("Token is not valid", 400, false);
            }
            existingToken.IsRevoked = true;
            try
            {
                await _unitOfWork.UserTokenRepo.UpdateAsync(existingToken);
                await _unitOfWork.SaveChangeAsync();
            }
            catch (Exception ex)
            {
                return new ResponseDTO("An error occurred during logout.", 500, false);
            }
            return new ResponseDTO("Logout success", 200, true);
        }

        public async Task<ResponseDTO> RefreshTokenAsync(RefreshTokenDTO dto) // Changed signature
        {
            // 1. Lấy RefreshToken string từ DTO
            string? refreshTokenValue = dto?.RefreshTokenValue;
            if (string.IsNullOrWhiteSpace(refreshTokenValue))
            {
                // Use AuthMessages if available, otherwise string literal
                return new ResponseDTO("Refresh token is required.", 400, false);
            }
            var existingToken = await _unitOfWork.UserTokenRepo.GetValidRefreshTokenWithUserAsync(refreshTokenValue);

            // Kiểm tra token không hợp lệ
            if (existingToken == null)
            {
                return new ResponseDTO("Invalid refresh token.", 401, false); // Unauthorized
            }
            if (existingToken.IsRevoked)
            {
                return new ResponseDTO("Refresh token has been revoked.", 401, false);
            }
            if (existingToken.ExpiredAt < DateTime.UtcNow)
            {
                existingToken.IsRevoked = true;
                await _unitOfWork.UserTokenRepo.UpdateAsync(existingToken);
                await _unitOfWork.SaveChangeAsync(); 
                return new ResponseDTO("Refresh token has expired.", 401, false);
            }


            // Lấy user từ token đã include
            var user = existingToken.User;
            // Kiểm tra user và role tồn tại
            if (user == null || user.Role == null)
            {
                // Log lỗi này vì không nên xảy ra nếu DB nhất quán
                Console.WriteLine($"Error: User or Role not found for valid refresh token ID: {existingToken.UserTokenId}");
                return new ResponseDTO("User associated with token not found.", 500, false); // Internal Server Error
            }


            // --- Token hợp lệ, tiến hành tạo token mới ---
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 3. Thu hồi token cũ (IsRevoked = true)
                existingToken.IsRevoked = true;
                await _unitOfWork.UserTokenRepo.UpdateAsync(existingToken); // Update không async

                // 4. Tạo AccessToken mới và RefreshToken mới
                var claims = new List<Claim>
                {
                    new Claim(JwtConstant.KeyClaim.userId, user.UserId.ToString()),
                    new Claim(JwtConstant.KeyClaim.fullName, user.FullName), 
                    new Claim(JwtConstant.KeyClaim.Role, user.Role.RoleName)
                };

                var newAccessTokenKey = JwtProvider.GenerateAccessToken(claims);
                var newRefreshTokenKey = JwtProvider.GenerateRefreshToken(claims);

                // 5. Lưu RefreshToken mới vào DB
                var newRefreshToken = new UserToken
                {
                    UserTokenId = Guid.NewGuid(),
                    TokenValue = newRefreshTokenKey,
                    UserId = user.UserId,
                    IsRevoked = false,
                    TokenType = TokenType.REFRESH,
                    CreatedAt = DateTime.UtcNow,
                    ExpiredAt = DateTime.UtcNow.AddDays(JwtSettingModel.ExpireDayRefreshToken)
                };
                await _unitOfWork.UserTokenRepo.AddAsync(newRefreshToken);

                await _unitOfWork.CommitTransactionAsync();

                // 6. Trả về cả hai token mới
                return new ResponseDTO("Token refreshed successfully.", 200, true, new
                {
                    AccessToken = newAccessTokenKey,
                    RefreshToken = newRefreshTokenKey, 
                });
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();

                return new ResponseDTO("An error occurred while refreshing the token.", 500, false);
            }
        }

        public async Task<ResponseDTO> RegisterForAdmin(RegisterForAdminDTO dto)
        {
            
            if (dto.Password != dto.ConfirmPassword)
            {
                return new ResponseDTO("Password is not fit with confirm password", 400, false);
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var checkByEmail = await _unitOfWork.BaseUserRepo.FindByEmailAndRoleAsync(dto.Email, dto.RoleName);

                if (checkByEmail != null)
                {
                    return new ResponseDTO("User is already in use", 200, false);
                }
                var checkByPhone = await _unitOfWork.BaseUserRepo.FindByPhoneNumberAsync(dto.PhoneNumber);
                if (checkByPhone != null)
                {
                    return new ResponseDTO("PhoneNumber is already in use", 200, false);

                }
                var role = await _unitOfWork.RoleRepo.GetByName(dto.RoleName);
                if (role == null)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return new ResponseDTO("Critical error: Admin role not found.", 500, false);
                }
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);

                // Giả lập lấy tọa độ
                double latitude = 0.0;
                double longitude = 0.0;
                // TODO: Call Geocoding service

                var userAddress = new Location(dto.Address ?? string.Empty, latitude, longitude);


                var newUser = new BaseUser
                {
                    UserId = Guid.NewGuid(),
                    FullName = dto.FullName,
                    Email = dto.Email,
                    PhoneNumber = dto.PhoneNumber,
                    PasswordHash = hashedPassword,
                    RoleId = role.RoleId,
                    Status = UserStatus.ACTIVE,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    IsEmailVerified = true,
                    IsPhoneVerified = true, // Admin tự tạo, verify sẵn
                    Address = userAddress,
                };

                // Xử lý Avatar (nếu có)
                if (dto.AvatarFile != null)
                {
                    try
                    {
                        newUser.AvatarUrl = await _firebaseUploadService.UploadFileAsync(dto.AvatarFile, newUser.UserId, FirebaseFileType.AVATAR_IMAGES);
                    }
                    catch (Exception uploadEx)
                    {
                        //Log uploadEx
                    }
                }

                await _unitOfWork.BaseUserRepo.AddAsync(newUser);
                await _unitOfWork.CommitTransactionAsync();

                return new ResponseDTO("Admin account created successfully.", 201, true, new { UserId = newUser.UserId });
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("An error occurred during registration.", 500, false);
            }
        }

        public async Task<ResponseDTO> RegisterForDriver(RegisterForDriverDTO dto)
        {
            if (dto.Password != dto.ConfirmPassword)
            {
                return new ResponseDTO("Password does not match confirm password.", 400, false);
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var checkByEmail = await _unitOfWork.BaseUserRepo.FindByEmailAndRoleAsync(dto.Email, "Driver");

                if (checkByEmail != null)
                {
                    return new ResponseDTO("User is already in use", 200, false);
                }
                var checkByPhone = await _unitOfWork.BaseUserRepo.FindByPhoneNumberAsync(dto.PhoneNumber);
                if (checkByPhone != null)
                {
                    return new ResponseDTO("PhoneNumber is already in use", 200, false);

                }

                var driverRole = await _unitOfWork.RoleRepo.GetByName("Driver");
                if (driverRole == null)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return new ResponseDTO("Critical error: Driver role not found.", 500, false);
                }

                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);


                // Lấy tọa độ cho địa chỉ
                double latitude = 0.0;
                double longitude = 0.0;
                // TODO: Call Geocoding service
                var driverAddress = new Location(dto.Address ?? string.Empty, latitude, longitude);

                var newDriver = new Driver // Tạo đối tượng Driver
                {
                    // --- BaseUser Fields ---
                    UserId = Guid.NewGuid(),
                    FullName = dto.FullName,
                    Email = dto.Email,
                    PhoneNumber = dto.PhoneNumber,
                    PasswordHash = hashedPassword,
                    RoleId = driverRole.RoleId,
                    Status = UserStatus.INACTIVE,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    DateOfBirth = dto.DateOfBirth,
                    IsEmailVerified = false,
                    IsPhoneVerified = false, // Cần xác thực sau
                    Address = driverAddress,

                    // --- Driver Specific Fields ---
                    LicenseNumber = string.Empty, // Thiếu trong DTO, cần cập nhật sau
                    LicenseClass = string.Empty,  // Thiếu trong DTO
                    LicenseExpiryDate = null,
                    IsLicenseVerified = false,
                    DriverStatus = DriverStatus.AVAILABLE,
                };

                // Xử lý Avatar (nếu có)
                if (dto.AvatarFile != null)
                {
                    try
                    {
                        newDriver.AvatarUrl = await _firebaseUploadService.UploadFileAsync(dto.AvatarFile, newDriver.UserId, FirebaseFileType.AVATAR_IMAGES);
                    }
                    catch (Exception uploadEx) { /* Log warning, proceed without avatar */ }
                }


                // Sử dụng DriverRepo để Add
                await _unitOfWork.DriverRepo.AddAsync(newDriver);
                await _unitOfWork.CommitTransactionAsync();

                // TODO: Gửi email/SMS xác thực

                return new ResponseDTO("Driver account created successfully. Please verify your email/phone.", 201, true);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("An error occurred during driver registration.", 500, false);
            }
        }

        public async Task<ResponseDTO> RegisterForOwner(RegisterForOwnerDTO dto)
        {
            if (dto.Password != dto.ConfirmPassword)
            {
                return new ResponseDTO("Password does not match confirm password.", 400, false);
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var checkByEmail = await _unitOfWork.BaseUserRepo.FindByEmailAndRoleAsync(dto.Email, "Owner");

                if (checkByEmail != null)
                {
                    return new ResponseDTO("User is already in use", 200, false);
                }
                var checkByPhone = await _unitOfWork.BaseUserRepo.FindByPhoneNumberAsync(dto.PhoneNumber);
                if (checkByPhone != null)
                {
                    return new ResponseDTO("PhoneNumber is already in use", 200, false);

                }

                // Kiểm tra TaxCode nếu được cung cấp và là duy nhất (tùy nghiệp vụ)
                if (!string.IsNullOrWhiteSpace(dto.TaxCode))
                {
                    var existingOwnerByTaxCode = await _unitOfWork.OwnerRepo.GetOwnerByTaxCodeAsync(dto.TaxCode);
                    if (existingOwnerByTaxCode != null)
                    {
                        await _unitOfWork.RollbackTransactionAsync();
                        return new ResponseDTO("Tax code already registered.", 409, false);
                    }
                }


                var ownerRole = await _unitOfWork.RoleRepo.GetByName("Owner");
                if (ownerRole == null)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return new ResponseDTO("Critical error: Owner role not found.", 500, false);
                }

                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            

                // Lấy tọa độ cho địa chỉ cá nhân
                double latitude = 0.0, longitude = 0.0;
                // TODO: Call Geocoding service for dto.Address
                var ownerAddress = new Location(dto.Address ?? string.Empty, latitude, longitude);

                // Lấy tọa độ cho địa chỉ kinh doanh (nếu có)
                Location? businessAddress = null;
                if (!string.IsNullOrWhiteSpace(dto.BussinessAddress)) // Sửa tên biến DTO nếu cần
                {
                    double bizLat = 0.0, bizLon = 0.0;
                    // TODO: Call Geocoding service for dto.BussinessAddress
                    businessAddress = new Location(dto.BussinessAddress, bizLat, bizLon);
                }

                var newOwner = new Owner // Tạo đối tượng Owner
                {
                    // --- BaseUser Fields ---
                    UserId = Guid.NewGuid(),
                    FullName = dto.FullName,
                    Email = dto.Email,
                    PhoneNumber = dto.PhoneNumber,
                    PasswordHash = hashedPassword,
                    RoleId = ownerRole.RoleId,
                    Status = UserStatus.INACTIVE, // Owner cần xác minh giấy tờ KD
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    DateOfBirth = dto.DateOfBirth,
                    IsEmailVerified = false,
                    IsPhoneVerified = false,
                    Address = ownerAddress,

                    // --- Owner Specific Fields ---
                    CompanyName = dto.CompanyName,
                    TaxCode = dto.TaxCode,
                    BusinessAddress = businessAddress, // Gán địa chỉ KD đã geocode
                    AverageRating = null // Ban đầu chưa có rating
                };

                // Xử lý Avatar (nếu có)
                if (dto.AvatarFile != null)
                {
                    try
                    {
                        newOwner.AvatarUrl = await _firebaseUploadService.UploadFileAsync(dto.AvatarFile, newOwner.UserId, FirebaseFileType.AVATAR_IMAGES);
                    }
                    catch { } // Log warning
                }

                // Sử dụng OwnerRepo để Add
                await _unitOfWork.OwnerRepo.AddAsync(newOwner);
                await _unitOfWork.CommitTransactionAsync();

                // TODO: Gửi email/SMS xác thực

                return new ResponseDTO("Owner account created successfully. Please verify your email/phone and complete business verification.", 201, true, new { UserId = newOwner.UserId });
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                // TODO: Log the exception ex
                return new ResponseDTO("An error occurred during owner registration.", 500, false);
            }
        }

        public async Task<ResponseDTO> RegisterForProvider(RegisterForProviderDTO dto)
        {
            if (dto.Password != dto.ConfirmPassword)
            {
                return new ResponseDTO("Password does not match confirm password.", 400, false);
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var checkByEmail = await _unitOfWork.BaseUserRepo.FindByEmailAndRoleAsync(dto.Email, "Provider");

                if (checkByEmail != null)
                {
                    return new ResponseDTO("User is already in use", 200, false);
                }
                var checkByPhone = await _unitOfWork.BaseUserRepo.FindByPhoneNumberAsync(dto.PhoneNumber);
                if (checkByPhone != null)
                {
                    return new ResponseDTO("PhoneNumber is already in use", 200, false);

                }

                // Kiểm tra TaxCode nếu được cung cấp và là duy nhất (tùy nghiệp vụ)
                if (!string.IsNullOrWhiteSpace(dto.TaxCode))
                {
                    var existingProviderByTaxCode = await _unitOfWork.ProviderRepo.GetProviderByTaxCodeAsync(dto.TaxCode);
                    if (existingProviderByTaxCode != null)
                    {
                        await _unitOfWork.RollbackTransactionAsync();
                        return new ResponseDTO("Tax code already registered.", 409, false);
                    }
                }


                var providerRole = await _unitOfWork.RoleRepo.GetByName("Provider");
                if (providerRole == null)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return new ResponseDTO("Critical error: Provider role not found.", 500, false);
                }

                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);


                // Lấy tọa độ cho địa chỉ cá nhân
                double latitude = 0.0, longitude = 0.0;
                // TODO: Call Geocoding service for dto.Address
                var providerAddress = new Location(dto.Address ?? string.Empty, latitude, longitude);

                // Lấy tọa độ cho địa chỉ kinh doanh (nếu có)
                Location? businessAddress = null;
                if (!string.IsNullOrWhiteSpace(dto.BusinessAddress))
                {
                    double bizLat = 0.0, bizLon = 0.0;
                    // TODO: Call Geocoding service for dto.BusinessAddress
                    businessAddress = new Location(dto.BusinessAddress, bizLat, bizLon);
                }


                var newProvider = new Provider // Tạo đối tượng Provider
                {
                    // --- BaseUser Fields ---
                    UserId = Guid.NewGuid(),
                    FullName = dto.FullName,
                    Email = dto.Email,
                    PhoneNumber = dto.PhoneNumber,
                    PasswordHash = hashedPassword,
                    RoleId = providerRole.RoleId,
                    Status = UserStatus.INACTIVE, // Provider cũng cần xác minh
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    DateOfBirth = dto.DateOfBirth,
                    IsEmailVerified = false,
                    IsPhoneVerified = false,
                    Address = providerAddress,

                    // --- Provider Specific Fields ---
                    CompanyName = dto.CompanyName,
                    TaxCode = dto.TaxCode,
                    BusinessAddress = businessAddress,
                    AverageRating = 5.0m // Rating mặc định hoặc null tùy bạn
                };

                // Xử lý Avatar (nếu có)
                if (dto.AvatarFile != null)
                {
                    try
                    {
                        newProvider.AvatarUrl = await _firebaseUploadService.UploadFileAsync(dto.AvatarFile, newProvider.UserId, FirebaseFileType.AVATAR_IMAGES);
                    }
                    catch { } // Log warning
                }


                // Sử dụng ProviderRepo để Add
                await _unitOfWork.ProviderRepo.AddAsync(newProvider);
                await _unitOfWork.CommitTransactionAsync();

                // TODO: Gửi email/SMS xác thực

                return new ResponseDTO("Provider account created successfully. Please verify your email/phone and complete business verification.", 201, true, new { UserId = newProvider.UserId });
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                // TODO: Log the exception ex
                return new ResponseDTO("An error occurred during provider registration.", 500, false);
            }
        }


        // HELPER
        private async Task RevokeAllUserRefreshTokens(Guid userId)
        {
            var userTokens = await _unitOfWork.UserTokenRepo.GetAllAsync(
                filter: rt => rt.UserId == userId && rt.TokenType == TokenType.REFRESH && !rt.IsRevoked
            );
            foreach (var token in userTokens)
            {
                token.IsRevoked = true;
                await _unitOfWork.UserTokenRepo.UpdateAsync(token);
            }
            // Consider calling SaveChangesAsync here or ensure it's called after
        }
    }
}
