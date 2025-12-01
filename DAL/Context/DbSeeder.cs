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
        // ───────────────────────────────────────────────
        // 🔹 ROLE IDs
        // ───────────────────────────────────────────────
        private static readonly Guid AdminRole = Guid.Parse("D4DAB1C3-6D48-4B23-8369-2D1C9C828F22");
        private static readonly Guid DriverRole = Guid.Parse("A1DAB1C3-6D48-4B23-8369-2D1C9C828F22");
        private static readonly Guid OwnerRole = Guid.Parse("B2DAB1C3-6D48-4B23-8369-2D1C9C828F22");
        private static readonly Guid StaffRole = Guid.Parse("E3DAB1C3-6D48-4B23-8369-2D1C9C828F22");
        private static readonly Guid ProviderRole = Guid.Parse("F5DAB1C3-6D48-4B23-8369-2D1C9C828F22");

        // ───────────────────────────────────────────────
        // 🔹 USER IDs
        // ───────────────────────────────────────────────
        private static readonly Guid AdminID = Guid.Parse("12345678-90AB-CDEF-1234-567890ABCDEF");
        private static readonly Guid DriverID = Guid.Parse("22345678-90AB-CDEF-1234-567890ABCDEF");
        private static readonly Guid OwnerID = Guid.Parse("32345678-90AB-CDEF-1234-567890ABCDEF");
        private static readonly Guid StaffID = Guid.Parse("42345678-90AB-CDEF-1234-567890ABCDEF");
        private static readonly Guid ProviderID = Guid.Parse("62345678-90AB-CDEF-1234-567890ABCDEF");
        private static readonly Guid ProviderID_02 = Guid.Parse("13f2b6c3-6bc0-48ef-877f-1c2e0c244c9c");

        private static readonly Guid DriverID_2 = Guid.Parse("22345678-90AB-CDEF-0002-567890ABCDEF");

        // ───────────────────────────────────────────────
        // 🔹 WALLET IDs
        // ───────────────────────────────────────────────
        private static readonly Guid AdminWalletID = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid DriverWalletID = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid OwnerWalletID = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid StaffWalletID = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid ProviderWalletID = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly Guid DriverWalletID_2 = Guid.Parse("66666666-6666-6666-6666-666666666666");


        // ───────────────────────────────────────────────
        // 🔹 TEMPLATE IDs
        // ───────────────────────────────────────────────
        private static readonly Guid ProviderContractTemplateID = Guid.Parse("C0100001-0001-0000-0000-000000000001");
        private static readonly Guid DriverContractTemplateID = Guid.Parse("C0200001-0002-0000-0000-000000000001");

        private static readonly Guid PickupRecordTemplateID = Guid.Parse("D0100001-0003-0000-0000-000000000001");
        private static readonly Guid DropoffRecordTemplateID = Guid.Parse("D0200001-0004-0000-0000-000000000001");

        // ───────────────────────────────────────────────
        // 🔹 VEHICLE TYPE IDs (⚠️ ĐÃ SỬA LỖI)
        // ───────────────────────────────────────────────
        private static readonly Guid VehicleTypeID_1T5 = Guid.Parse("A0000001-0001-0000-0000-000000000001"); // (Tên cũ: VTC-0001...)
        private static readonly Guid VehicleTypeID_8T = Guid.Parse("A0000002-0002-0000-0000-000000000001"); // (Tên cũ: VTC-0002...)
        private static readonly Guid VehicleTypeID_Container = Guid.Parse("A0000003-0003-0000-0000-000000000001"); // (Tên cũ: VTC-0003...)
        private static readonly Guid VehicleTypeID_Refrigerated = Guid.Parse("A0000004-0004-0000-0000-000000000001"); // (Tên cũ: VTC-0004...)


        // ───────────────────────────────────────────────
        // 🔹 HÀM SEED CHÍNH
        // ───────────────────────────────────────────────
        public static void Seed(ModelBuilder modelBuilder)
        {
            SeedRole(modelBuilder);
            SeedUser(modelBuilder);
            SeedWallets(modelBuilder);
            SeedDriver(modelBuilder);
            SeedOwner(modelBuilder);
            SeedProvider(modelBuilder);

            SeedContractTemplates(modelBuilder);
            SeedDeliveryRecordTemplates(modelBuilder);

            SeedVehicleType(modelBuilder);
        }

        // ───────────────────────────────────────────────
        // 🔹 ROLES
        // ───────────────────────────────────────────────
        private static void SeedRole(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Role>().HasData(
                new Role { RoleId = AdminRole, RoleName = "Admin" },
                new Role { RoleId = DriverRole, RoleName = "Driver" },
                new Role { RoleId = OwnerRole, RoleName = "Owner" },
                new Role { RoleId = StaffRole, RoleName = "Staff" },
                new Role { RoleId = ProviderRole, RoleName = "Provider" }
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 USERS (Admin, Staff)
        // ───────────────────────────────────────────────
        private static void SeedUser(ModelBuilder modelBuilder)
        {
            string fixedHashedPassword = "$2a$11$rTz6DZiEeBqhVrzF25CgTOBPf41jpn2Tg/nnIqnX8KS6uIerB/1dm"; // Mật khẩu là "123"

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
                    IsEmailVerified = true,
                    IsPhoneVerified = true
                }
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 WALLETS (Đã sửa số dư)
        // ───────────────────────────────────────────────
        private static void SeedWallets(ModelBuilder modelBuilder)
        {
            decimal initialBalance = 500000000m; // 500 triệu

            modelBuilder.Entity<Wallet>().HasData(
                new Wallet { WalletId = AdminWalletID, UserId = AdminID, Balance = initialBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
                new Wallet { WalletId = DriverWalletID, UserId = DriverID, Balance = initialBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
                new Wallet { WalletId = OwnerWalletID, UserId = OwnerID, Balance = initialBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
                new Wallet { WalletId = StaffWalletID, UserId = StaffID, Balance = initialBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
                new Wallet { WalletId = ProviderWalletID, UserId = ProviderID, Balance = initialBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
                new Wallet { WalletId = DriverWalletID_2, UserId = DriverID_2, Balance = initialBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow }
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 DRIVER
        // ───────────────────────────────────────────────
        private static void SeedDriver(ModelBuilder modelBuilder)
        {
            string fixedHashedPassword = "$2a$11$rTz6DZiEeBqhVrzF25CgTOBPf41jpn2Tg/nnIqnX8KS6uIerB/1dm"; // Mật khẩu là "123"

            modelBuilder.Entity<Driver>().HasData(
                new Driver
                {
                    // --- BaseUser Fields ---
                    UserId = DriverID,
                    FullName = "Tài xế Văn A",
                    Email = "driver@example.com",
                    PhoneNumber = "0987654321",
                    PasswordHash = fixedHashedPassword,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    Status = UserStatus.ACTIVE,
                    DateOfBirth = new DateTime(1990, 5, 15),
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    RoleId = DriverRole,

                    // --- Driver Specific Fields ---
                    LicenseNumber = "GPLX12345",
                    LicenseClass = "B2",
                    LicenseExpiryDate = DateTime.UtcNow.AddYears(5),
                    IsLicenseVerified = true,
                    DriverStatus = DriverStatus.AVAILABLE
                },
                new Driver
                {
                    // --- BaseUser Fields ---
                    UserId = DriverID_2,
                    FullName = "Tài xế Văn B",
                    Email = "driver2@example.com",
                    PhoneNumber = "0987111222",
                    PasswordHash = fixedHashedPassword,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    Status = UserStatus.ACTIVE,
                    DateOfBirth = new DateTime(1995, 1, 1),
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    RoleId = DriverRole,

                    // --- Driver Specific Fields ---
                    LicenseNumber = "GPLX67890",
                    LicenseClass = "C",
                    LicenseExpiryDate = DateTime.UtcNow.AddYears(3),
                    IsLicenseVerified = true,
                    DriverStatus = DriverStatus.AVAILABLE
                }
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 OWNER
        // ───────────────────────────────────────────────
        private static void SeedOwner(ModelBuilder modelBuilder)
        {
            string fixedHashedPassword = "$2a$11$rTz6DZiEeBqhVrzF25CgTOBPf41jpn2Tg/nnIqnX8KS6uIerB/1dm"; // Mật khẩu là "123"

            modelBuilder.Entity<Owner>().HasData(
                new Owner
                {
                    // --- BaseUser Fields ---
                    UserId = OwnerID,
                    FullName = "Owner_Name",
                    Email = "danglaikp@gmail.com",
                    PhoneNumber = "0112233445",
                    PasswordHash = fixedHashedPassword,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    Status = UserStatus.ACTIVE,
                    DateOfBirth = new DateTime(1985, 10, 20),
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    RoleId = OwnerRole,

                    // --- Owner Specific Fields ---
                    CompanyName = "Công ty Vận Tải ABC",
                    TaxCode = "0312345678",
                    AverageRating = 4.5m
                }
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 PROVIDER
        // ───────────────────────────────────────────────
        private static void SeedProvider(ModelBuilder modelBuilder)
        {
            string fixedHashedPassword = "$2a$11$rTz6DZiEeBqhVrzF25CgTOBPf41jpn2Tg/nnIqnX8KS6uIerB/1dm"; // Mật khẩu là "123"

            modelBuilder.Entity<Provider>().HasData(
                new Provider
                {
                    // --- BaseUser Fields ---
                    UserId = ProviderID,
                    FullName = "Provider_Name",
                    Email = "danglvhse151369@fpt.edu.vn",
                    PhoneNumber = "0556677889",
                    PasswordHash = fixedHashedPassword,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    Status = UserStatus.ACTIVE,
                    DateOfBirth = new DateTime(1988, 3, 1),
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    RoleId = ProviderRole,

                    // --- Provider Specific Fields ---
                    CompanyName = "Nhà cung cấp XYZ",
                    TaxCode = "0401234567",
                    AverageRating = 4.8m
                },
                 new Provider
                 {
                     // --- BaseUser Fields ---
                     UserId = ProviderID_02,
                     FullName = "Provider_Name_2",
                     Email = "tienvlnse172689@fpt.edu.vn",
                     PhoneNumber = "094792002",
                     PasswordHash = fixedHashedPassword,
                     CreatedAt = DateTime.UtcNow,
                     LastUpdatedAt = DateTime.UtcNow,
                     Status = UserStatus.ACTIVE,
                     DateOfBirth = new DateTime(1988, 3, 1),
                     IsEmailVerified = true,
                     IsPhoneVerified = true,
                     RoleId = ProviderRole,

                     // --- Provider Specific Fields ---
                     CompanyName = "Nhà cung cấp XYZ",
                     TaxCode = "0401234567",
                     AverageRating = 4.8m
                 }
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 CONTRACT TEMPLATES
        // ───────────────────────────────────────────────
        private static void SeedContractTemplates(ModelBuilder modelBuilder)
        {
            var seedTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<ContractTemplate>().HasData(
                new ContractTemplate
                {
                    ContractTemplateId = ProviderContractTemplateID,
                    ContractTemplateName = "Hợp đồng Vận chuyển (Owner-Provider)",
                    Version = "1.0",
                    Type = ContractType.PROVIDER_CONTRACT,
                    CreatedAt = seedTime
                },
                new ContractTemplate
                {
                    ContractTemplateId = DriverContractTemplateID,
                    ContractTemplateName = "Hợp đồng Thuê Tài xế (Owner-Driver)",
                    Version = "1.0",
                    Type = ContractType.DRIVER_CONTRACT,
                    CreatedAt = seedTime
                }
            );

            modelBuilder.Entity<ContractTerm>().HasData(
                // Provider Terms
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = ProviderContractTemplateID, Order = 1, Content = "Bên A (Chủ xe) đồng ý cung cấp dịch vụ vận tải theo các điều khoản đã thỏa thuận." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = ProviderContractTemplateID, Order = 2, Content = "Bên B (Chủ hàng/Provider) đồng ý thanh toán cước phí vận chuyển đúng hạn." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = ProviderContractTemplateID, Order = 3, Content = "Trách nhiệm bồi thường thiệt hại sẽ được áp dụng theo quy định hiện hành nếu hàng hóa bị hư hỏng, mất mát do lỗi của Bên A." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = ProviderContractTemplateID, Order = 4, Content = "Hợp đồng có hiệu lực kể từ thời điểm cả hai bên xác nhận ký." },
                // Driver Terms
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = DriverContractTemplateID, Order = 1, Content = "Bên A (Chủ xe) đồng ý thuê Bên B (Tài xế) để thực hiện các chuyến vận chuyển được chỉ định." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = DriverContractTemplateID, Order = 2, Content = "Bên B (Tài xế) có trách nhiệm bảo quản phương tiện, hàng hóa và tuân thủ các quy định về an toàn giao thông." }
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 DELIVERY RECORD TEMPLATES
        // ───────────────────────────────────────────────
        private static void SeedDeliveryRecordTemplates(ModelBuilder modelBuilder)
        {
            var seedTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<DeliveryRecordTemplate>().HasData(
                new DeliveryRecordTemplate
                {
                    DeliveryRecordTemplateId = PickupRecordTemplateID,
                    TemplateName = "Biên bản Giao hàng (Tài xế nhận hàng)",
                    Version = "1.0",
                    Type = DeliveryRecordType.PICKUP,
                    Status = DeliveryRecordTemplateStatus.ACTIVE,
                    CreatedAt = seedTime
                },
                new DeliveryRecordTemplate
                {
                    DeliveryRecordTemplateId = DropoffRecordTemplateID,
                    TemplateName = "Biên bản Trả hàng (Tài xế trả hàng)",
                    Version = "1.0",
                    Type = DeliveryRecordType.DROPOFF,
                    Status = DeliveryRecordTemplateStatus.ACTIVE,
                    CreatedAt = seedTime
                }
            );

            modelBuilder.Entity<DeliveryRecordTerm>().HasData(
                // Pickup Terms
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 1, Content = "Tài xế đã xác nhận số lượng, chủng loại hàng hóa đúng với thông tin trên ứng dụng." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 2, Content = "Tình trạng hàng hóa bên ngoài nguyên vẹn, không móp méo, ướt, hoặc rách vỡ." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 3, Content = "Tài xế đã chụp ảnh xác nhận tình trạng hàng hóa khi nhận." },
                // Dropoff Terms
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 1, Content = "Người nhận đã xác nhận số lượng, chủng loại hàng hóa đúng với thông tin trên ứng dụng." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 2, Content = "Tình trạng hàng hóa bên ngoài nguyên vẹn. Người nhận không có khiếu nại về tình trạng bên ngoài của hàng hóa." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 3, Content = "Người nhận đã ký tên/chụp ảnh xác nhận đã nhận hàng." }
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 VEHICLE TYPES (Đã sửa loại xe)
        // ───────────────────────────────────────────────
        private static void SeedVehicleType(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<VehicleType>().HasData(
                new VehicleType
                {
                    VehicleTypeId = VehicleTypeID_1T5,
                    VehicleTypeName = "Xe tải 1.5 tấn",
                    Description = "Xe tải hạng nhẹ, phù hợp chở hàng nội thành."
                },
                new VehicleType
                {
                    VehicleTypeId = VehicleTypeID_8T,
                    VehicleTypeName = "Xe tải 8 tấn",
                    Description = "Xe tải hạng trung (2 chân), chở hàng liên tỉnh."
                },
                new VehicleType
                {
                    VehicleTypeId = VehicleTypeID_Container,
                    VehicleTypeName = "Xe đầu kéo",
                    Description = "Xe đầu kéo chuyên dụng chở container 20ft hoặc 40ft."
                },
                new VehicleType
                {
                    VehicleTypeId = VehicleTypeID_Refrigerated, // Đổi từ Pickup
                    VehicleTypeName = "Xe tải thùng lạnh",
                    Description = "Xe tải chuyên dụng có thùng giữ nhiệt, chở hàng đông lạnh."
                }
            );
        }
    }
}