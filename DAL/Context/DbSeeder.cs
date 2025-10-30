using Common.Enums;
using Common.Enums.Status;
using Common.Enums.Type;
using Common.ValueObjects;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace DAL.Context
{
    public class DbSeeder
    {
        // Bỏ _context đi vì Seed dùng ModelBuilder
        // private readonly DriverShareAppContext _context; 

        // ───────────────────────────────────────────────
        // 🔹 ROLE IDs (Giữ nguyên)
        // ───────────────────────────────────────────────
        private static readonly Guid AdminRole = Guid.Parse("D4DAB1C3-6D48-4B23-8369-2D1C9C828F22");
        private static readonly Guid DriverRole = Guid.Parse("A1DAB1C3-6D48-4B23-8369-2D1C9C828F22");
        private static readonly Guid OwnerRole = Guid.Parse("B2DAB1C3-6D48-4B23-8369-2D1C9C828F22");
        private static readonly Guid StaffRole = Guid.Parse("E3DAB1C3-6D48-4B23-8369-2D1C9C828F22");
        private static readonly Guid ProviderRole = Guid.Parse("F5DAB1C3-6D48-4B23-8369-2D1C9C828F22"); // Thêm Provider Role ID

        // ───────────────────────────────────────────────
        // 🔹 USER IDs (Thêm ProviderID)
        // ───────────────────────────────────────────────
        private static readonly Guid AdminID = Guid.Parse("12345678-90AB-CDEF-1234-567890ABCDEF");
        private static readonly Guid DriverID = Guid.Parse("22345678-90AB-CDEF-1234-567890ABCDEF");
        private static readonly Guid OwnerID = Guid.Parse("32345678-90AB-CDEF-1234-567890ABCDEF");
        private static readonly Guid StaffID = Guid.Parse("42345678-90AB-CDEF-1234-567890ABCDEF");
        private static readonly Guid ProviderID = Guid.Parse("62345678-90AB-CDEF-1234-567890ABCDEF"); // Thêm Provider ID

        // ───────────────────────────────────────────────
        // 🔹 WALLET IDs (Thêm mới)
        // ───────────────────────────────────────────────
        private static readonly Guid AdminWalletID = Guid.NewGuid();
        private static readonly Guid DriverWalletID = Guid.NewGuid();
        private static readonly Guid OwnerWalletID = Guid.NewGuid();
        private static readonly Guid StaffWalletID = Guid.NewGuid();
        private static readonly Guid ProviderWalletID = Guid.NewGuid();


        // ───────────────────────────────────────────────
        // 🔹 HÀM SEED CHÍNH (Thêm các hàm seed mới)
        // ───────────────────────────────────────────────
        public static void Seed(ModelBuilder modelBuilder)
        {
            SeedRole(modelBuilder);
            SeedUser(modelBuilder);
            SeedWallets(modelBuilder); // Thêm seed Wallet
            SeedDriver(modelBuilder);
            SeedOwner(modelBuilder);
            SeedProvider(modelBuilder);
        }

        // ───────────────────────────────────────────────
        // 🔹 ROLES (Thêm Provider Role)
        // ───────────────────────────────────────────────
        private static void SeedRole(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Role>().HasData(
                new Role { RoleId = AdminRole, RoleName = "Admin" },
                new Role { RoleId = DriverRole, RoleName = "Driver" },
                new Role { RoleId = OwnerRole, RoleName = "Owner" },
                new Role { RoleId = StaffRole, RoleName = "Staff" },
                new Role { RoleId = ProviderRole, RoleName = "Provider" } // Thêm Provider Role
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 USERS (Admin, Staff - Thêm WalletId, Address)
        // ───────────────────────────────────────────────
        private static void SeedUser(ModelBuilder modelBuilder)
        {
            string fixedHashedPassword = "$2a$11$rTz6DZiEeBqhVrzF25CgTOBPf41jpn2Tg/nnIqnX8KS6uIerB/1dm"; // Mật khẩu là "123"

            // Tạo một địa chỉ mẫu
            //var sampleAddress = new Location("123 Đường ABC", 10.7769, 106.7009); // Ví dụ: TP.HCM

            modelBuilder.Entity<BaseUser>().HasData(
                new BaseUser
                {
                    UserId = AdminID,
                    FullName = "Admin_Name",
                    Email = "admin@example.com",
                    RoleId = AdminRole,
                    PasswordHash = fixedHashedPassword,
                    PhoneNumber = "0123456789",
                    Status = UserStatus.ACTIVE,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    //Address = sampleAddress,   // Gán Địa chỉ
                    IsEmailVerified = true,
                    IsPhoneVerified = true
                },
                new BaseUser
                {
                    UserId = StaffID,
                    FullName = "Staff_Name",
                    Email = "staff@example.com",
                    RoleId = StaffRole,
                    PasswordHash = fixedHashedPassword,
                    PhoneNumber = "0445566777",
                    Status = UserStatus.ACTIVE,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    //Address = sampleAddress,  // Gán Địa chỉ
                    IsEmailVerified = true,
                    IsPhoneVerified = true
                }
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 WALLETS (Thêm mới)
        // ───────────────────────────────────────────────
        private static void SeedWallets(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Wallet>().HasData(
                new Wallet { WalletId = AdminWalletID, UserId = AdminID, Balance = 1000000, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
                new Wallet { WalletId = DriverWalletID, UserId = DriverID, Balance = 50000, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
                new Wallet { WalletId = OwnerWalletID, UserId = OwnerID, Balance = 5000000, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
                new Wallet { WalletId = StaffWalletID, UserId = StaffID, Balance = 0, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
                new Wallet { WalletId = ProviderWalletID, UserId = ProviderID, Balance = 2000000, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow }
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 DRIVER (Thêm mới - Dùng Entity<Driver>)
        // ───────────────────────────────────────────────
        private static void SeedDriver(ModelBuilder modelBuilder)
        {
            string fixedHashedPassword = "$2a$11$rTz6DZiEeBqhVrzF25CgTOBPf41jpn2Tg/nnIqnX8KS6uIerB/1dm"; // Mật khẩu là "123"
            //var driverAddress = new Location("456 Đường XYZ", 10.8231, 106.6297); // Ví dụ: TP.HCM

            modelBuilder.Entity<Driver>().HasData(
                new Driver
                {
                    // --- BaseUser Fields ---
                    UserId = DriverID,
                    FullName = "Driver_Name",
                    Email = "driver@example.com",
                    PhoneNumber = "0987654321",
                    PasswordHash = fixedHashedPassword,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    Status = UserStatus.ACTIVE,
                    DateOfBirth = new DateTime(1990, 5, 15),
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    //Address = driverAddress,
                    RoleId = DriverRole,

                    // --- Driver Specific Fields ---
                    LicenseNumber = "GPLX12345",
                    LicenseClass = "B2",
                    LicenseExpiryDate = DateTime.UtcNow.AddYears(5),
                    IsLicenseVerified = true,
                    IsInTrip = false // Mặc định là không trong chuyến đi
                }
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 OWNER (Thêm mới - Dùng Entity<Owner>)
        // ───────────────────────────────────────────────
        private static void SeedOwner(ModelBuilder modelBuilder)
        {
            string fixedHashedPassword = "$2a$11$rTz6DZiEeBqhVrzF25CgTOBPf41jpn2Tg/nnIqnX8KS6uIerB/1dm"; // Mật khẩu là "123"
            //var ownerAddress = new Location("789 Đường LMN", 21.0285, 105.8542); // Ví dụ: Hà Nội
            //var businessAddress = new Location("Lô A1, KCN Sóng Thần", 10.9171, 106.7513); // Ví dụ: Bình Dương

            modelBuilder.Entity<Owner>().HasData(
                new Owner
                {
                    // --- BaseUser Fields ---
                    UserId = OwnerID,
                    FullName = "Owner_Name",
                    Email = "owner@example.com",
                    PhoneNumber = "0112233445",
                    PasswordHash = fixedHashedPassword,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    Status = UserStatus.ACTIVE,
                    DateOfBirth = new DateTime(1985, 10, 20),
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    //Address = ownerAddress,
                    RoleId = OwnerRole,

                    // --- Owner Specific Fields ---
                    CompanyName = "Công ty Vận Tải ABC",
                    TaxCode = "0312345678",
                    //BusinessAddress = businessAddress,
                    AverageRating = 4.5m // Có thể null
                }
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 PROVIDER (Thêm mới - Dùng Entity<Provider>)
        // ───────────────────────────────────────────────
        private static void SeedProvider(ModelBuilder modelBuilder)
        {
            string fixedHashedPassword = "$2a$11$rTz6DZiEeBqhVrzF25CgTOBPf41jpn2Tg/nnIqnX8KS6uIerB/1dm"; // Mật khẩu là "123"
            //var providerAddress = new Location("Khu phố 1, Phường PQR", 16.0479, 108.2209); // Ví dụ: Đà Nẵng
            //var providerBusinessAddress = new Location("Số 10, Đường STU", 16.0544, 108.2022); // Ví dụ: Đà Nẵng

            modelBuilder.Entity<Provider>().HasData(
                new Provider
                {
                    // --- BaseUser Fields ---
                    UserId = ProviderID,
                    FullName = "Provider_Name",
                    Email = "provider@example.com",
                    PhoneNumber = "0556677889",
                    PasswordHash = fixedHashedPassword,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    Status = UserStatus.ACTIVE,
                    DateOfBirth = new DateTime(1988, 3, 1),
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    //Address = providerAddress,
                    RoleId = ProviderRole, // Gán đúng Role ID

                    // --- Provider Specific Fields ---
                    CompanyName = "Nhà cung cấp XYZ",
                    TaxCode = "0401234567",
                    //BusinessAddress = providerBusinessAddress,
                    AverageRating = 4.8m
                }
            );
        }
    }
}