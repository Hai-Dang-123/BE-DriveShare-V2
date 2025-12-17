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

        public async Task<ResponseDTO> LoginAsync(LoginDTO dto)
        {
            var user = await _unitOfWork.BaseUserRepo.FindByEmailAsync(dto.Email);
            if (user == null) return new ResponseDTO("User not found !!!", 404, false);

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!isPasswordValid) return new ResponseDTO("Wrong email or password !!!", 400, false);

            if (user.IsEmailVerified == false)
            {
                return new ResponseDTO("Email is not verified. Please verify your email before logging in.", 403, false);
            }

            // Revoke old tokens
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

            // [FIXED] DÙNG TRANSACTION KIỂU MỚI
            using var transaction = await _unitOfWork.BeginTransactionAsync();
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

                // Commit thủ công qua biến transaction
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO("Token refreshed successfully.", 200, true, new
                {
                    AccessToken = newAccessTokenKey,
                    RefreshToken = newRefreshTokenKey,
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO("An error occurred while refreshing the token.", 500, false);
            }
        }

        // ======================= REGISTER FOR ADMIN =======================
        public async Task<ResponseDTO> RegisterForAdmin(RegisterForAdminDTO dto)
        {
            if (dto.Password != dto.ConfirmPassword) return new ResponseDTO("Password is not fit with confirm password", 400, false);

            // [ADDED] VALIDATE DATE OF BIRTH (Admin phải >= 18 tuổi)
            if (dto.DateOfBirth >= DateTime.UtcNow) return new ResponseDTO("Date of birth cannot be in the future.", 400, false);
            var today = DateTime.UtcNow;
            var age = today.Year - dto.DateOfBirth.Year;
            if (dto.DateOfBirth > today.AddYears(-age)) age--; // Chưa tới sinh nhật năm nay thì trừ 1 tuổi
            if (age < 18) return new ResponseDTO("Admin must be at least 18 years old.", 400, false);
            // -----------------------------------------------------

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var checkByEmail = await _unitOfWork.BaseUserRepo.FindByEmailAndRoleAsync(dto.Email, dto.RoleName);
                if (checkByEmail != null) return new ResponseDTO("User is already in use", 200, false);

                var checkByPhone = await _unitOfWork.BaseUserRepo.FindByPhoneNumberAsync(dto.PhoneNumber);
                if (checkByPhone != null) return new ResponseDTO("PhoneNumber is already in use", 200, false);

                var role = await _unitOfWork.RoleRepo.GetByName(dto.RoleName);
                if (role == null)
                {
                    await transaction.RollbackAsync();
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
                    // [ADDED] Bổ sung lưu ngày sinh cho Admin (Nếu trong DTO có)
                    DateOfBirth = dto.DateOfBirth,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    Address = userAddress,
                };

                if (dto.AvatarFile != null)
                {
                    try { newUser.AvatarUrl = await _firebaseUploadService.UploadFileAsync(dto.AvatarFile, newUser.UserId, FirebaseFileType.AVATAR_IMAGES); }
                    catch { }
                }

                await _unitOfWork.BaseUserRepo.AddAsync(newUser);
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO("Admin account created successfully.", 201, true, new { UserId = newUser.UserId });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO("An error occurred during registration.", 500, false);
            }
        }

        // ======================= REGISTER FOR DRIVER =======================
        public async Task<ResponseDTO> RegisterForDriver(RegisterForDriverDTO dto)
        {
            if (dto.Password != dto.ConfirmPassword) return new ResponseDTO("Password does not match confirm password.", 400, false);

            // [ADDED] VALIDATE DATE OF BIRTH (Tài xế phải >= 18 tuổi)
            if (dto.DateOfBirth >= DateTime.UtcNow) return new ResponseDTO("Date of birth cannot be in the future.", 400, false);
            var today = DateTime.UtcNow;
            var age = today.Year - dto.DateOfBirth.Year;
            if (dto.DateOfBirth > today.AddYears(-age)) age--;
            if (age < 18) return new ResponseDTO("Driver must be at least 18 years old.", 400, false);
            // -----------------------------------------------------

            Guid createdUserId = Guid.Empty;
            string createdEmail = string.Empty;
            string createdFullName = string.Empty;

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var checkByEmail = await _unitOfWork.BaseUserRepo.FindByEmailAndRoleAsync(dto.Email, "Driver");
                if (checkByEmail != null) return new ResponseDTO("User is already in use", 200, false);

                var checkByPhone = await _unitOfWork.BaseUserRepo.FindByPhoneNumberAsync(dto.PhoneNumber);
                if (checkByPhone != null) return new ResponseDTO("PhoneNumber is already in use", 200, false);

                var driverRole = await _unitOfWork.RoleRepo.GetByName("Driver");
                if (driverRole == null)
                {
                    await transaction.RollbackAsync();
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
                    IsLicenseVerified = false,
                };

                if (dto.AvatarFile != null)
                {
                    try { newDriver.AvatarUrl = await _firebaseUploadService.UploadFileAsync(dto.AvatarFile, newDriver.UserId, FirebaseFileType.AVATAR_IMAGES); }
                    catch { }
                }

                await _unitOfWork.DriverRepo.AddAsync(newDriver);
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                createdUserId = newDriver.UserId;
                createdEmail = newDriver.Email;
                createdFullName = newDriver.FullName;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO("An error occurred during driver registration.", 500, false);
            }

            if (createdUserId != Guid.Empty)
            {
                try { await SendVerificationEmailPrivateAsync(createdUserId, createdEmail, createdFullName); }
                catch (Exception ex) { Console.WriteLine($"Error sending verification email: {ex.Message}"); }
            }

            return new ResponseDTO("Driver account created successfully. Please check your email to verify.", 201, true);
        }

        // ======================= REGISTER FOR OWNER =======================
        public async Task<ResponseDTO> RegisterForOwner(RegisterForOwnerDTO dto)
        {
            if (dto.Password != dto.ConfirmPassword) return new ResponseDTO("Password does not match confirm password.", 400, false);

            // [ADDED] VALIDATE DATE OF BIRTH (Chủ hàng/xe phải >= 18 tuổi)
            if (dto.DateOfBirth >= DateTime.UtcNow) return new ResponseDTO("Date of birth cannot be in the future.", 400, false);
            var today = DateTime.UtcNow;
            var age = today.Year - dto.DateOfBirth.Year;
            if (dto.DateOfBirth > today.AddYears(-age)) age--;
            if (age < 18) return new ResponseDTO("Owner must be at least 18 years old.", 400, false);
            // -----------------------------------------------------

            Guid createdUserId = Guid.Empty;
            string createdEmail = string.Empty;
            string createdFullName = string.Empty;

            using var transaction = await _unitOfWork.BeginTransactionAsync();
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
                        await transaction.RollbackAsync();
                        return new ResponseDTO("Tax code already registered.", 409, false);
                    }
                }

                var ownerRole = await _unitOfWork.RoleRepo.GetByName("Owner");
                if (ownerRole == null)
                {
                    await transaction.RollbackAsync();
                    return new ResponseDTO("Critical error: Owner role not found.", 500, false);
                }

                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                var ownerAddress = new Location(dto.Address ?? string.Empty, 0.0, 0.0);
                Location? businessAddress = !string.IsNullOrWhiteSpace(dto.BussinessAddress) ? new Location(dto.BussinessAddress, 0.0, 0.0) : null;

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
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                createdUserId = newOwner.UserId;
                createdEmail = newOwner.Email;
                createdFullName = newOwner.FullName;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO("An error occurred during owner registration.", 500, false);
            }

            if (createdUserId != Guid.Empty)
            {
                try { await SendVerificationEmailPrivateAsync(createdUserId, createdEmail, createdFullName); }
                catch (Exception ex) { Console.WriteLine($"Error sending verification email: {ex.Message}"); }
            }

            return new ResponseDTO("Owner account created successfully. Please check your email to verify.", 201, true, new { UserId = createdUserId });
        }

        // ======================= REGISTER FOR PROVIDER =======================
        public async Task<ResponseDTO> RegisterForProvider(RegisterForProviderDTO dto)
        {
            if (dto.Password != dto.ConfirmPassword) return new ResponseDTO("Password does not match confirm password.", 400, false);

            // [ADDED] VALIDATE DATE OF BIRTH (Provider phải >= 18 tuổi)
            if (dto.DateOfBirth >= DateTime.UtcNow) return new ResponseDTO("Date of birth cannot be in the future.", 400, false);
            var today = DateTime.UtcNow;
            var age = today.Year - dto.DateOfBirth.Year;
            if (dto.DateOfBirth > today.AddYears(-age)) age--;
            if (age < 18) return new ResponseDTO("Provider must be at least 18 years old.", 400, false);
            // -----------------------------------------------------

            Guid createdUserId = Guid.Empty;
            string createdEmail = string.Empty;
            string createdFullName = string.Empty;

            using var transaction = await _unitOfWork.BeginTransactionAsync();
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
                        await transaction.RollbackAsync();
                        return new ResponseDTO("Tax code already registered.", 409, false);
                    }
                }

                var providerRole = await _unitOfWork.RoleRepo.GetByName("Provider");
                if (providerRole == null)
                {
                    await transaction.RollbackAsync();
                    return new ResponseDTO("Critical error: Provider role not found.", 500, false);
                }

                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                var providerAddress = new Location(dto.Address ?? string.Empty, 0.0, 0.0);
                Location? businessAddress = !string.IsNullOrWhiteSpace(dto.BusinessAddress) ? new Location(dto.BusinessAddress, 0.0, 0.0) : null;

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
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                createdUserId = newProvider.UserId;
                createdEmail = newProvider.Email;
                createdFullName = newProvider.FullName;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO("An error occurred during provider registration.", 500, false);
            }

            if (createdUserId != Guid.Empty)
            {
                try { await SendVerificationEmailPrivateAsync(createdUserId, createdEmail, createdFullName); }
                catch (Exception ex) { Console.WriteLine($"Error sending verification email: {ex.Message}"); }
            }

            return new ResponseDTO("Provider account created successfully. Please check your email to verify.", 201, true, new { UserId = createdUserId });
        }

        // ======================= VERIFY EMAIL =======================
        public async Task<ResponseDTO> VerifyEmailAsync(Guid userId, string token)
        {
            var verificationToken = await _unitOfWork.UserTokenRepo.GetByUserIdAndTokenValueAsync(
                userId, token, TokenType.EMAIL_VERIFICATION
            );

            if (verificationToken == null || verificationToken.IsRevoked)
                return new ResponseDTO("Verification link is invalid or has been used.", 400, false);

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

            // [FIXED] TRANSACTION
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                user.IsEmailVerified = true;
                user.LastUpdatedAt = DateTime.UtcNow;
                await _unitOfWork.BaseUserRepo.UpdateAsync(user);

                verificationToken.IsRevoked = true;
                await _unitOfWork.UserTokenRepo.UpdateAsync(verificationToken);

                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO("Email verified successfully. Account is now active.", 200, true);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO("An error occurred during email verification.", 500, false);
            }
        }

        // ======================= PRIVATE HELPER: SEND EMAIL =======================
        private async Task SendVerificationEmailPrivateAsync(Guid userId, string email, string fullName)
        {
            // Logic gửi mail giữ nguyên, không cần Transaction DB ở đây
            try
            {
                string tokenValue = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

                // Lưu token
                // Ở đây là 1 thao tác nhỏ, có thể không cần Transaction lớn
                var verificationToken = new UserToken
                {
                    UserTokenId = Guid.NewGuid(),
                    TokenValue = tokenValue,
                    UserId = userId,
                    IsRevoked = false,
                    TokenType = TokenType.EMAIL_VERIFICATION,
                    CreatedAt = DateTime.UtcNow,
                    ExpiredAt = DateTime.UtcNow.AddHours(24)
                };

                await _unitOfWork.UserTokenRepo.AddAsync(verificationToken);
                await _unitOfWork.SaveChangeAsync();

                string verificationLink = $"http://localhost:8081/verify-email?userId={userId}&token={tokenValue}";
                await _emailService.SendEmailVerificationLinkAsync(email, fullName, verificationLink);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send verification email to {email}: {ex.Message}");
            }
        }
    }
}