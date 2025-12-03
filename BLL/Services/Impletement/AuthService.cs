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
using System.Security.Claims;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        private readonly IFirebaseUploadService _firebaseUploadService;
        private readonly IEmailService _emailService;

        public AuthService(IUnitOfWork unitOfWork, UserUtility userUtility, IFirebaseUploadService firebaseUploadService, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _firebaseUploadService = firebaseUploadService;
            _emailService = emailService;
        }

        // ... [Giữ nguyên LoginAsync, LogoutAsync, RefreshTokenAsync] ...

        public async Task<ResponseDTO> LoginAsync(LoginDTO dto)
        {
            // (Giữ nguyên code cũ của bạn)
            var user = await _unitOfWork.BaseUserRepo.FindByEmailAsync(dto.Email);
            if (user == null) return new ResponseDTO("User not found !!!", 404, false);

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!isPasswordValid) return new ResponseDTO("Wrong email or password !!!", 400, false);

            if (user.IsEmailVerified == false)
            {
                return new ResponseDTO("Email is not verified. Please verify your email before logging in.", 403, false);
            }

            

            var exitsRefreshToken = await _unitOfWork.UserTokenRepo.GetRefreshTokenByUserID(user.UserId);
            if (exitsRefreshToken != null)
            {
                exitsRefreshToken.IsRevoked = true;
                await _unitOfWork.UserTokenRepo.UpdateAsync(exitsRefreshToken);
            }

            var claims = new List<Claim>
            {
                new Claim(JwtConstant.KeyClaim.userId, user.UserId.ToString()),
                new Claim(JwtConstant.KeyClaim.fullName, user.FullName),
                new Claim(JwtConstant.KeyClaim.Role, user.Role.RoleName)
            };

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
            catch (Exception)
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
            // (Giữ nguyên code cũ)
            var userId = _userUtility.GetUserIdFromToken();
            if (userId == Guid.Empty) return new ResponseDTO("User is not found ", 400, false);

            var existingToken = await _unitOfWork.UserTokenRepo.GetRefreshTokenByUserID(userId);
            if (existingToken == null || existingToken.IsRevoked || existingToken.TokenType != TokenType.REFRESH)
                return new ResponseDTO("Token is not valid", 400, false);

            existingToken.IsRevoked = true;
            try
            {
                await _unitOfWork.UserTokenRepo.UpdateAsync(existingToken);
                await _unitOfWork.SaveChangeAsync();
            }
            catch
            {
                return new ResponseDTO("An error occurred during logout.", 500, false);
            }
            return new ResponseDTO("Logout success", 200, true);
        }

        public async Task<ResponseDTO> RefreshTokenAsync(RefreshTokenDTO dto)
        {
            // (Giữ nguyên code cũ)
            string? refreshTokenValue = dto?.RefreshTokenValue;
            if (string.IsNullOrWhiteSpace(refreshTokenValue)) return new ResponseDTO("Refresh token is required.", 400, false);

            var existingToken = await _unitOfWork.UserTokenRepo.GetValidRefreshTokenWithUserAsync(refreshTokenValue);
            if (existingToken == null) return new ResponseDTO("Invalid refresh token.", 401, false);
            if (existingToken.IsRevoked) return new ResponseDTO("Refresh token has been revoked.", 401, false);
            if (existingToken.ExpiredAt < DateTime.UtcNow)
            {
                existingToken.IsRevoked = true;
                await _unitOfWork.UserTokenRepo.UpdateAsync(existingToken);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO("Refresh token has expired.", 401, false);
            }

            var user = existingToken.User;
            if (user == null || user.Role == null) return new ResponseDTO("User associated with token not found.", 500, false);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                existingToken.IsRevoked = true;
                await _unitOfWork.UserTokenRepo.UpdateAsync(existingToken);

                var claims = new List<Claim>
                {
                    new Claim(JwtConstant.KeyClaim.userId, user.UserId.ToString()),
                    new Claim(JwtConstant.KeyClaim.fullName, user.FullName),
                    new Claim(JwtConstant.KeyClaim.Role, user.Role.RoleName)
                };

                var newAccessTokenKey = JwtProvider.GenerateAccessToken(claims);
                var newRefreshTokenKey = JwtProvider.GenerateRefreshToken(claims);

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

                return new ResponseDTO("Token refreshed successfully.", 200, true, new
                {
                    AccessToken = newAccessTokenKey,
                    RefreshToken = newRefreshTokenKey,
                });
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("An error occurred while refreshing the token.", 500, false);
            }
        }


        // ======================= REGISTER FOR ADMIN =======================
        public async Task<ResponseDTO> RegisterForAdmin(RegisterForAdminDTO dto)
        {
            if (dto.Password != dto.ConfirmPassword) return new ResponseDTO("Password is not fit with confirm password", 400, false);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var checkByEmail = await _unitOfWork.BaseUserRepo.FindByEmailAndRoleAsync(dto.Email, dto.RoleName);
                if (checkByEmail != null) return new ResponseDTO("User is already in use", 200, false);

                var checkByPhone = await _unitOfWork.BaseUserRepo.FindByPhoneNumberAsync(dto.PhoneNumber);
                if (checkByPhone != null) return new ResponseDTO("PhoneNumber is already in use", 200, false);

                var role = await _unitOfWork.RoleRepo.GetByName(dto.RoleName);
                if (role == null)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return new ResponseDTO("Critical error: Admin role not found.", 500, false);
                }

                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                var userAddress = new Location(dto.Address ?? string.Empty, 0.0, 0.0);

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
                    IsEmailVerified = true, // Admin tự tạo, mặc định true (hoặc false tùy bạn)
                    IsPhoneVerified = true,
                    Address = userAddress,
                };

                if (dto.AvatarFile != null)
                {
                    try { newUser.AvatarUrl = await _firebaseUploadService.UploadFileAsync(dto.AvatarFile, newUser.UserId, FirebaseFileType.AVATAR_IMAGES); }
                    catch { }
                }

                await _unitOfWork.BaseUserRepo.AddAsync(newUser);
                await _unitOfWork.CommitTransactionAsync();

                // Admin thường verify sẵn, nhưng nếu muốn gửi mail thông báo thì gọi ở đây.
                // await SendVerificationEmailPrivateAsync(newUser.UserId, newUser.Email, newUser.FullName);

                return new ResponseDTO("Admin account created successfully.", 201, true, new { UserId = newUser.UserId });
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("An error occurred during registration.", 500, false);
            }
        }

        // ======================= REGISTER FOR DRIVER =======================
        public async Task<ResponseDTO> RegisterForDriver(RegisterForDriverDTO dto)
        {
            if (dto.Password != dto.ConfirmPassword) return new ResponseDTO("Password does not match confirm password.", 400, false);

            // 1. Khai báo biến tạm để lưu thông tin User sau khi tạo thành công
            Guid createdUserId = Guid.Empty;
            string createdEmail = string.Empty;
            string createdFullName = string.Empty;

            // --- BẮT ĐẦU TRANSACTION ---
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var checkByEmail = await _unitOfWork.BaseUserRepo.FindByEmailAndRoleAsync(dto.Email, "Driver");
                if (checkByEmail != null) return new ResponseDTO("User is already in use", 200, false);

                var checkByPhone = await _unitOfWork.BaseUserRepo.FindByPhoneNumberAsync(dto.PhoneNumber);
                if (checkByPhone != null) return new ResponseDTO("PhoneNumber is already in use", 200, false);

                var driverRole = await _unitOfWork.RoleRepo.GetByName("Driver");
                if (driverRole == null)
                {
                    // Chưa Commit thì mới được Rollback
                    await _unitOfWork.RollbackTransactionAsync();
                    return new ResponseDTO("Critical error: Driver role not found.", 500, false);
                }

                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                var driverAddress = new Location(dto.Address ?? string.Empty, 0.0, 0.0);

                var newDriver = new Driver
                {
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
                    IsPhoneVerified = false,
                    Address = driverAddress,
                    DriverStatus = DriverStatus.AVAILABLE,

                    LicenseClass = null,
                    LicenseExpiryDate = null,
                    LicenseNumber = null,
                    IsLicenseVerified = false,
                    
                };

                if (dto.AvatarFile != null)
                {
                    try { newDriver.AvatarUrl = await _firebaseUploadService.UploadFileAsync(dto.AvatarFile, newDriver.UserId, FirebaseFileType.AVATAR_IMAGES); }
                    catch { }
                }

                await _unitOfWork.DriverRepo.AddAsync(newDriver);

                // 2. COMMIT TRANSACTION
                await _unitOfWork.CommitTransactionAsync();

                // 3. Gán dữ liệu ra biến tạm (ĐỂ GỬI MAIL SAU)
                createdUserId = newDriver.UserId;
                createdEmail = newDriver.Email;
                createdFullName = newDriver.FullName;
            }
            catch (Exception)
            {
                // Chỉ Rollback khi lỗi xảy ra TRƯỚC khi Commit
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("An error occurred during driver registration.", 500, false);
            }

            // 4. GỬI EMAIL (NẰM HOÀN TOÀN NGOÀI KHỐI TRY-CATCH CỦA DB)
            if (createdUserId != Guid.Empty)
            {
                try
                {
                    await SendVerificationEmailPrivateAsync(createdUserId, createdEmail, createdFullName);
                }
                catch (Exception ex)
                {
                    // Nếu gửi mail lỗi, chỉ log lại. KHÔNG return lỗi 500 vì User đã tạo thành công.
                    Console.WriteLine($"Error sending verification email: {ex.Message}");
                }
            }

            return new ResponseDTO("Driver account created successfully. Please check your email to verify.", 201, true);
        }

        // ======================= REGISTER FOR OWNER =======================
        public async Task<ResponseDTO> RegisterForOwner(RegisterForOwnerDTO dto)
        {
            if (dto.Password != dto.ConfirmPassword) return new ResponseDTO("Password does not match confirm password.", 400, false);

            Guid createdUserId = Guid.Empty;
            string createdEmail = string.Empty;
            string createdFullName = string.Empty;

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var checkByEmail = await _unitOfWork.BaseUserRepo.FindByEmailAndRoleAsync(dto.Email, "Owner");
                if (checkByEmail != null) return new ResponseDTO("User is already in use", 200, false);

                var checkByPhone = await _unitOfWork.BaseUserRepo.FindByPhoneNumberAsync(dto.PhoneNumber);
                if (checkByPhone != null) return new ResponseDTO("PhoneNumber is already in use", 200, false);

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
                var ownerAddress = new Location(dto.Address ?? string.Empty, 0.0, 0.0);
                Location? businessAddress = !string.IsNullOrWhiteSpace(dto.BussinessAddress)
                    ? new Location(dto.BussinessAddress, 0.0, 0.0) : null;

                var newOwner = new Owner
                {
                    UserId = Guid.NewGuid(),
                    FullName = dto.FullName,
                    Email = dto.Email,
                    PhoneNumber = dto.PhoneNumber,
                    PasswordHash = hashedPassword,
                    RoleId = ownerRole.RoleId,
                    Status = UserStatus.INACTIVE,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    DateOfBirth = dto.DateOfBirth,
                    IsEmailVerified = false,
                    IsPhoneVerified = false,
                    Address = ownerAddress,
                    CompanyName = dto.CompanyName,
                    TaxCode = dto.TaxCode,
                    BusinessAddress = businessAddress,
                };

                if (dto.AvatarFile != null)
                {
                    try { newOwner.AvatarUrl = await _firebaseUploadService.UploadFileAsync(dto.AvatarFile, newOwner.UserId, FirebaseFileType.AVATAR_IMAGES); }
                    catch { }
                }

                await _unitOfWork.OwnerRepo.AddAsync(newOwner);

                await _unitOfWork.CommitTransactionAsync(); // Commit thành công

                createdUserId = newOwner.UserId;
                createdEmail = newOwner.Email;
                createdFullName = newOwner.FullName;
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("An error occurred during owner registration.", 500, false);
            }

            // Gửi Email bên ngoài
            if (createdUserId != Guid.Empty)
            {
                try
                {
                    await SendVerificationEmailPrivateAsync(createdUserId, createdEmail, createdFullName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending verification email: {ex.Message}");
                }
            }

            return new ResponseDTO("Owner account created successfully. Please check your email to verify.", 201, true, new { UserId = createdUserId });
        }

        // ======================= REGISTER FOR PROVIDER =======================
        public async Task<ResponseDTO> RegisterForProvider(RegisterForProviderDTO dto)
        {
            if (dto.Password != dto.ConfirmPassword) return new ResponseDTO("Password does not match confirm password.", 400, false);

            Guid createdUserId = Guid.Empty;
            string createdEmail = string.Empty;
            string createdFullName = string.Empty;

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var checkByEmail = await _unitOfWork.BaseUserRepo.FindByEmailAndRoleAsync(dto.Email, "Provider");
                if (checkByEmail != null) return new ResponseDTO("User is already in use", 200, false);

                var checkByPhone = await _unitOfWork.BaseUserRepo.FindByPhoneNumberAsync(dto.PhoneNumber);
                if (checkByPhone != null) return new ResponseDTO("PhoneNumber is already in use", 200, false);

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
                var providerAddress = new Location(dto.Address ?? string.Empty, 0.0, 0.0);
                Location? businessAddress = !string.IsNullOrWhiteSpace(dto.BusinessAddress)
                    ? new Location(dto.BusinessAddress, 0.0, 0.0) : null;

                var newProvider = new Provider
                {
                    UserId = Guid.NewGuid(),
                    FullName = dto.FullName,
                    Email = dto.Email,
                    PhoneNumber = dto.PhoneNumber,
                    PasswordHash = hashedPassword,
                    RoleId = providerRole.RoleId,
                    Status = UserStatus.INACTIVE,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    DateOfBirth = dto.DateOfBirth,
                    IsEmailVerified = false,
                    IsPhoneVerified = false,
                    Address = providerAddress,
                    CompanyName = dto.CompanyName,
                    TaxCode = dto.TaxCode,
                    BusinessAddress = businessAddress,
                    AverageRating = 5.0m
                };

                if (dto.AvatarFile != null)
                {
                    try { newProvider.AvatarUrl = await _firebaseUploadService.UploadFileAsync(dto.AvatarFile, newProvider.UserId, FirebaseFileType.AVATAR_IMAGES); }
                    catch { }
                }

                await _unitOfWork.ProviderRepo.AddAsync(newProvider);

                await _unitOfWork.CommitTransactionAsync(); // Commit thành công

                createdUserId = newProvider.UserId;
                createdEmail = newProvider.Email;
                createdFullName = newProvider.FullName;
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("An error occurred during provider registration.", 500, false);
            }

            // Gửi Email bên ngoài
            if (createdUserId != Guid.Empty)
            {
                try
                {
                    await SendVerificationEmailPrivateAsync(createdUserId, createdEmail, createdFullName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending verification email: {ex.Message}");
                }
            }

            return new ResponseDTO("Provider account created successfully. Please check your email to verify.", 201, true, new { UserId = createdUserId });
        }

        // ======================= PRIVATE HELPER: SEND EMAIL =======================
        // Hàm này được gọi sau khi Register xong (đã có User trong DB)
        private async Task SendVerificationEmailPrivateAsync(Guid userId, string email, string fullName)
        {
            try
            {
                // 1. Tạo Token (UserToken)
                string tokenValue = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

                // Revoke token cũ (nếu có - dù mới đăng ký thì chưa có, nhưng code cho chắc)
                var oldTokens = await _unitOfWork.UserTokenRepo.GetAllAsync(
                    filter: t => t.UserId == userId && t.TokenType == TokenType.EMAIL_VERIFICATION && !t.IsRevoked
                );
                foreach (var token in oldTokens)
                {
                    token.IsRevoked = true;
                    await _unitOfWork.UserTokenRepo.UpdateAsync(token);
                }

                // Add token mới
                var verificationToken = new UserToken
                {
                    UserTokenId = Guid.NewGuid(),
                    TokenValue = tokenValue,
                    UserId = userId,
                    IsRevoked = false,
                    TokenType = TokenType.EMAIL_VERIFICATION, // Đảm bảo Enum này tồn tại
                    CreatedAt = DateTime.UtcNow,
                    ExpiredAt = DateTime.UtcNow.AddHours(24)
                };

                await _unitOfWork.UserTokenRepo.AddAsync(verificationToken);
                await _unitOfWork.SaveChangeAsync(); // Lưu Token xuống DB

                // 2. Tạo link & Gửi Email
                // Thay thế domain localhost bằng domain thật của FE khi deploy
                string verificationLink = $"http://localhost:8081/verify-email?userId={userId}&token={tokenValue}";

                await _emailService.SendEmailVerificationLinkAsync(email, fullName, verificationLink);
            }
            catch (Exception ex)
            {
                // Log lỗi nhưng KHÔNG throw để tránh làm failed request Register (User đã tạo rồi)
                Console.WriteLine($"Failed to send verification email to {email}: {ex.Message}");
            }
        }

        // ======================= VERIFY EMAIL =======================
        public async Task<ResponseDTO> VerifyEmailAsync(Guid userId, string token)
        {
            // Bây giờ hàm này sẽ hoạt động vì Repository đã có Method GetByUserIdAndTokenValueAsync
            var verificationToken = await _unitOfWork.UserTokenRepo.GetByUserIdAndTokenValueAsync(
                userId,
                token,
                TokenType.EMAIL_VERIFICATION
            );

            if (verificationToken == null || verificationToken.IsRevoked)
            {
                return new ResponseDTO("Verification link is invalid or has been used.", 400, false);
            }

            if (verificationToken.ExpiredAt < DateTime.UtcNow)
            {
                verificationToken.IsRevoked = true;
                await _unitOfWork.UserTokenRepo.UpdateAsync(verificationToken);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO("Verification link has expired.", 400, false);
            }

            var user = await _unitOfWork.BaseUserRepo.GetByIdAsync(userId);
            if (user == null) return new ResponseDTO("User associated with token not found.", 500, false);

            if (user.IsEmailVerified == true)
            {
                verificationToken.IsRevoked = true;
                await _unitOfWork.UserTokenRepo.UpdateAsync(verificationToken);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO("Email is already verified.", 200, true);
            }

            // Dùng Transaction cho việc update User & Token
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                user.IsEmailVerified = true;
                user.LastUpdatedAt = DateTime.UtcNow;
                await _unitOfWork.BaseUserRepo.UpdateAsync(user);

                verificationToken.IsRevoked = true;
                await _unitOfWork.UserTokenRepo.UpdateAsync(verificationToken);

                await _unitOfWork.CommitTransactionAsync();
                return new ResponseDTO("Email verified successfully. Account is now active.", 200, true);
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("An error occurred during email verification.", 500, false);
            }
        }
    }
}