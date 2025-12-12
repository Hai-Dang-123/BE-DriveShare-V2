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
        private static readonly Guid OwnerID_2 = Guid.Parse("69cf944d-7eee-4b23-ab1a-7f8412081635");
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
        // 🔹 TEMPLATE IDs (CONTRACT)
        // ───────────────────────────────────────────────
        private static readonly Guid ProviderContractTemplateID = Guid.Parse("C0100001-0001-0000-0000-000000000001");
        private static readonly Guid DriverContractTemplateID = Guid.Parse("C0200001-0002-0000-0000-000000000001");

        // ───────────────────────────────────────────────
        // 🔹 TEMPLATE IDs (DELIVERY RECORD - HÀNG HÓA)
        // ───────────────────────────────────────────────
        private static readonly Guid PickupRecordTemplateID = Guid.Parse("D0100001-0003-0000-0000-000000000001");
        private static readonly Guid DropoffRecordTemplateID = Guid.Parse("D0200001-0004-0000-0000-000000000001");

        // ───────────────────────────────────────────────
        // 🔹 TEMPLATE IDs (VEHICLE HANDOVER - XE)  <-- MỚI THÊM
        // ───────────────────────────────────────────────
        // Dùng Guid khác biệt để dễ nhận diện (D03, D04)
        private static readonly Guid VehiclePickupTemplateID = Guid.Parse("D0300001-0005-0000-0000-000000000001");
        private static readonly Guid VehicleDropoffTemplateID = Guid.Parse("D0400001-0006-0000-0000-000000000001");

        // ───────────────────────────────────────────────
        // 🔹 VEHICLE TYPE IDs
        // ───────────────────────────────────────────────
        private static readonly Guid VehicleTypeID_1T5 = Guid.Parse("A0000001-0001-0000-0000-000000000001");
        private static readonly Guid VehicleTypeID_8T = Guid.Parse("A0000002-0002-0000-0000-000000000001");
        private static readonly Guid VehicleTypeID_Container = Guid.Parse("A0000003-0003-0000-0000-000000000001");
        private static readonly Guid VehicleTypeID_Refrigerated = Guid.Parse("A0000004-0004-0000-0000-000000000001");


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
            SeedDeliveryRecordTemplates(modelBuilder); // Đã cập nhật thêm Xe

            SeedVehicleType(modelBuilder);

            // [NEW] Seed Giấy tờ tùy thân
            SeedUserDocuments(modelBuilder);
            SeedOwnerDriverLinks(modelBuilder);
        }

        // ... (Các hàm SeedRole, SeedUser, SeedWallets, SeedDriver, SeedOwner, SeedProvider, SeedContractTemplates giữ nguyên như cũ) ...
        // Tôi ẩn bớt code cũ để tập trung vào phần thay đổi bên dưới
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

        private static void SeedUser(ModelBuilder modelBuilder)
        {
            string fixedHashedPassword = "$2a$11$rTz6DZiEeBqhVrzF25CgTOBPf41jpn2Tg/nnIqnX8KS6uIerB/1dm"; // "123"
            modelBuilder.Entity<BaseUser>().HasData(
                new BaseUser { UserId = AdminID, FullName = "Admin_Name", Email = "admin@example.com", RoleId = AdminRole, PasswordHash = fixedHashedPassword, PhoneNumber = "0123456789", Status = UserStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow, IsEmailVerified = true, IsPhoneVerified = true },
                new BaseUser { UserId = StaffID, FullName = "Staff_Name", Email = "staff@example.com", RoleId = StaffRole, PasswordHash = fixedHashedPassword, PhoneNumber = "0445566777", Status = UserStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow, IsEmailVerified = true, IsPhoneVerified = true }
            );
        }

        private static void SeedWallets(ModelBuilder modelBuilder)
        {
            decimal initialBalance = 500000000m;
            modelBuilder.Entity<Wallet>().HasData(
                new Wallet { WalletId = AdminWalletID, UserId = AdminID, Balance = initialBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
                new Wallet { WalletId = DriverWalletID, UserId = DriverID, Balance = initialBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
                new Wallet { WalletId = OwnerWalletID, UserId = OwnerID, Balance = initialBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
                new Wallet { WalletId = StaffWalletID, UserId = StaffID, Balance = initialBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
                new Wallet { WalletId = ProviderWalletID, UserId = ProviderID, Balance = initialBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow },
                new Wallet { WalletId = DriverWalletID_2, UserId = DriverID_2, Balance = initialBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow }
            );
        }

        private static void SeedDriver(ModelBuilder modelBuilder)
        {
            string fixedHashedPassword = "$2a$11$rTz6DZiEeBqhVrzF25CgTOBPf41jpn2Tg/nnIqnX8KS6uIerB/1dm";
            modelBuilder.Entity<Driver>().HasData(
                new Driver { UserId = DriverID, FullName = "Tài xế Văn A", Email = "tienvo5668@gmail.com", PhoneNumber = "0987654321", PasswordHash = fixedHashedPassword, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow, Status = UserStatus.ACTIVE, DateOfBirth = new DateTime(1990, 5, 15), IsEmailVerified = true, IsPhoneVerified = true, RoleId = DriverRole, LicenseNumber = "GPLX12345", LicenseClass = "B2", LicenseExpiryDate = DateTime.UtcNow.AddYears(5), IsLicenseVerified = true, DriverStatus = DriverStatus.AVAILABLE, HasDeclaredInitialHistory = true },
                new Driver { UserId = DriverID_2, FullName = "Tài xế Văn B", Email = "driver2@example.com", PhoneNumber = "0987111222", PasswordHash = fixedHashedPassword, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow, Status = UserStatus.ACTIVE, DateOfBirth = new DateTime(1995, 1, 1), IsEmailVerified = true, IsPhoneVerified = true, RoleId = DriverRole, LicenseNumber = "GPLX67890", LicenseClass = "C", LicenseExpiryDate = DateTime.UtcNow.AddYears(3), IsLicenseVerified = true, DriverStatus = DriverStatus.AVAILABLE , HasDeclaredInitialHistory = true}
            );
        }

        private static void SeedOwner(ModelBuilder modelBuilder)
        {
            string fixedHashedPassword = "$2a$11$rTz6DZiEeBqhVrzF25CgTOBPf41jpn2Tg/nnIqnX8KS6uIerB/1dm";
            modelBuilder.Entity<Owner>().HasData(
                new Owner { UserId = OwnerID, FullName = "Owner_Name", Email = "trangphse171412@fpt.edu.vn", PhoneNumber = "0112233445", PasswordHash = fixedHashedPassword, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow, Status = UserStatus.ACTIVE, DateOfBirth = new DateTime(1985, 10, 20), IsEmailVerified = true, IsPhoneVerified = true, RoleId = OwnerRole, CompanyName = "Công ty Vận Tải ABC", TaxCode = "0312345678", AverageRating = 4.5m },
                new Owner { UserId = OwnerID_2, FullName = "Owner_Name_2", Email = "voluongnhuttien@gmail.com", PhoneNumber = "0112233445", PasswordHash = fixedHashedPassword, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow, Status = UserStatus.ACTIVE, DateOfBirth = new DateTime(1985, 10, 20), IsEmailVerified = true, IsPhoneVerified = true, RoleId = OwnerRole, CompanyName = "Công ty Vận Tải ABC", TaxCode = "0312345678", AverageRating = 4.5m }

            );
        }

        private static void SeedProvider(ModelBuilder modelBuilder)
        {
            string fixedHashedPassword = "$2a$11$rTz6DZiEeBqhVrzF25CgTOBPf41jpn2Tg/nnIqnX8KS6uIerB/1dm";
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

        private static void SeedContractTemplates(ModelBuilder modelBuilder)
        {
            var seedTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            modelBuilder.Entity<ContractTemplate>().HasData(
                new ContractTemplate { ContractTemplateId = ProviderContractTemplateID, ContractTemplateName = "Hợp đồng Vận chuyển (Owner-Provider)", Version = "1.0", Type = ContractType.PROVIDER_CONTRACT, CreatedAt = seedTime },
                new ContractTemplate { ContractTemplateId = DriverContractTemplateID, ContractTemplateName = "Hợp đồng Thuê Tài xế (Owner-Driver)", Version = "1.0", Type = ContractType.DRIVER_CONTRACT, CreatedAt = seedTime }
            );
            modelBuilder.Entity<ContractTerm>().HasData(
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = ProviderContractTemplateID, Order = 1, Content = "Bên A (Chủ xe) đồng ý cung cấp dịch vụ vận tải theo các điều khoản đã thỏa thuận." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = ProviderContractTemplateID, Order = 2, Content = "Bên B (Chủ hàng/Provider) đồng ý thanh toán cước phí vận chuyển đúng hạn." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = ProviderContractTemplateID, Order = 3, Content = "Trách nhiệm bồi thường thiệt hại sẽ được áp dụng theo quy định hiện hành nếu hàng hóa bị hư hỏng, mất mát do lỗi của Bên A." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = ProviderContractTemplateID, Order = 4, Content = "Hợp đồng có hiệu lực kể từ thời điểm cả hai bên xác nhận ký." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = DriverContractTemplateID, Order = 1, Content = "Bên A (Chủ xe) đồng ý thuê Bên B (Tài xế) để thực hiện các chuyến vận chuyển được chỉ định." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = DriverContractTemplateID, Order = 2, Content = "Bên B (Tài xế) có trách nhiệm bảo quản phương tiện, hàng hóa và tuân thủ các quy định về an toàn giao thông." }
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 DELIVERY RECORD TEMPLATES (CẬP NHẬT THÊM XE)
        // ───────────────────────────────────────────────
        private static void SeedDeliveryRecordTemplates(ModelBuilder modelBuilder)
        {
            var seedTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<DeliveryRecordTemplate>().HasData(
                // --- 1. MẪU GIAO NHẬN HÀNG HÓA (CARGO) ---
                new DeliveryRecordTemplate
                {
                    DeliveryRecordTemplateId = PickupRecordTemplateID,
                    TemplateName = "Biên bản Giao hàng (Tài xế nhận hàng)",
                    Version = "1.0",
                    Type = DeliveryRecordType.PICKUP, // Loại PICKUP
                    Status = DeliveryRecordTemplateStatus.ACTIVE,
                    CreatedAt = seedTime
                },
                new DeliveryRecordTemplate
                {
                    DeliveryRecordTemplateId = DropoffRecordTemplateID,
                    TemplateName = "Biên bản Trả hàng (Tài xế trả hàng)",
                    Version = "1.0",
                    Type = DeliveryRecordType.DROPOFF, // Loại DROPOFF
                    Status = DeliveryRecordTemplateStatus.ACTIVE,
                    CreatedAt = seedTime
                },

                // --- 2. MẪU GIAO NHẬN XE (VEHICLE HANDOVER) - MỚI ---
                new DeliveryRecordTemplate
                {
                    DeliveryRecordTemplateId = VehiclePickupTemplateID,
                    TemplateName = "Biên bản Bàn Giao Xe (Chủ xe -> Tài xế)",
                    Version = "1.0",
                    Type = DeliveryRecordType.HANDOVER, // Vẫn dùng PICKUP nhưng context là Xe
                    Status = DeliveryRecordTemplateStatus.ACTIVE,
                    CreatedAt = seedTime
                },
                new DeliveryRecordTemplate
                {
                    DeliveryRecordTemplateId = VehicleDropoffTemplateID,
                    TemplateName = "Biên bản Thu Hồi Xe (Tài xế -> Chủ xe)",
                    Version = "1.0",
                    Type = DeliveryRecordType.RETURN, // Vẫn dùng DROPOFF nhưng context là Xe
                    Status = DeliveryRecordTemplateStatus.ACTIVE,
                    CreatedAt = seedTime
                }
            );

            // --- TERMS (ĐIỀU KHOẢN CHI TIẾT) ---
            modelBuilder.Entity<DeliveryRecordTerm>().HasData(
                // A. Terms cho GIAO HÀNG (CARGO PICKUP)
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 1, Content = "Tài xế đã xác nhận số lượng, chủng loại hàng hóa đúng với thông tin trên ứng dụng." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 2, Content = "Tình trạng hàng hóa bên ngoài nguyên vẹn, không móp méo, ướt, hoặc rách vỡ." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 3, Content = "Tài xế đã chụp ảnh xác nhận tình trạng hàng hóa khi nhận." },

                // B. Terms cho TRẢ HÀNG (CARGO DROPOFF)
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 1, Content = "Người nhận đã xác nhận số lượng, chủng loại hàng hóa đúng với thông tin trên ứng dụng." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 2, Content = "Tình trạng hàng hóa bên ngoài nguyên vẹn. Người nhận không có khiếu nại về tình trạng bên ngoài của hàng hóa." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 3, Content = "Người nhận đã ký tên/chụp ảnh xác nhận đã nhận hàng." },

                // C. Terms cho GIAO XE (VEHICLE PICKUP - Giao cho tài xế)
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 1, Content = "Giấy tờ xe đầy đủ (Đăng ký, Đăng kiểm, Bảo hiểm bắt buộc)." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 2, Content = "Hệ thống đèn (Pha, cos, xi-nhan, phanh) và còi hoạt động bình thường." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 3, Content = "Lốp xe (bao gồm lốp dự phòng) đủ áp suất, gai lốp còn tốt." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 4, Content = "Ngoại thất xe sạch sẽ, các vết trầy xước cũ (nếu có) đã được ghi nhận." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 5, Content = "Nội thất xe sạch sẽ, không có mùi lạ, điều hòa hoạt động tốt." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 6, Content = "Mức nhiên liệu và số Odometer đã được ghi nhận chính xác trên ứng dụng." },

                // D. Terms cho TRẢ XE (VEHICLE DROPOFF - Nhận lại từ tài xế)
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehicleDropoffTemplateID, DisplayOrder = 1, Content = "Ngoại thất xe nguyên vẹn như lúc nhận (không phát sinh vết trầy xước/móp méo mới)." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehicleDropoffTemplateID, DisplayOrder = 2, Content = "Nội thất xe sạch sẽ, tài xế đã dọn dẹp rác cá nhân." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehicleDropoffTemplateID, DisplayOrder = 3, Content = "Tài xế bàn giao đầy đủ chìa khóa và giấy tờ xe gốc." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehicleDropoffTemplateID, DisplayOrder = 4, Content = "Mức nhiên liệu khi trả tương đương hoặc đúng theo thỏa thuận so với lúc nhận." }
            );
        }

        private static void SeedVehicleType(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<VehicleType>().HasData(
                new VehicleType { VehicleTypeId = VehicleTypeID_1T5, VehicleTypeName = "Xe tải 1.5 tấn", Description = "Xe tải hạng nhẹ, phù hợp chở hàng nội thành." },
                new VehicleType { VehicleTypeId = VehicleTypeID_8T, VehicleTypeName = "Xe tải 8 tấn", Description = "Xe tải hạng trung (2 chân), chở hàng liên tỉnh." },
                new VehicleType { VehicleTypeId = VehicleTypeID_Container, VehicleTypeName = "Xe đầu kéo", Description = "Xe đầu kéo chuyên dụng chở container 20ft hoặc 40ft." },
                new VehicleType { VehicleTypeId = VehicleTypeID_Refrigerated, VehicleTypeName = "Xe tải thùng lạnh", Description = "Xe tải chuyên dụng có thùng giữ nhiệt, chở hàng đông lạnh." }
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 USER DOCUMENTS (EKYC DATA) - [NEW]
        // ───────────────────────────────────────────────
        private static void SeedUserDocuments(ModelBuilder modelBuilder)
        {
            var seedTime = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);

            modelBuilder.Entity<UserDocument>().HasData(
                // 1. DRIVER 1: Đã xác thực đầy đủ (CCCD + GPLX)
                new UserDocument
                {
                    UserDocumentId = Guid.NewGuid(),
                    UserId = DriverID, // Tài xế Văn A
                    DocumentType = DocumentType.CCCD,
                    Status = VerifileStatus.ACTIVE,
                    IdentityNumber = "079090001234",
                    FullName = "TÀI XẾ VĂN A",
                    DateOfBirth = new DateTime(1990, 5, 15),
                    PlaceOfOrigin = "Hồ Chí Minh",
                    PlaceOfResidence = "123 Lê Lợi, Q1, TP.HCM",
                    IssueDate = new DateTime(2021, 5, 15),
                    ExpiryDate = new DateTime(2041, 5, 15),
                    IssuePlace = "Cục Cảnh sát QLHC về TTXH",
                    FrontImageUrl = "https://example.com/driver1_cccd_front.jpg",
                    BackImageUrl = "https://example.com/driver1_cccd_back.jpg",
                    PortraitImageUrl = "https://example.com/driver1_face.jpg",
                    IsDocumentReal = true,
                    FaceMatchScore = 98.5,
                    CreatedAt = seedTime,
                    VerifiedAt = seedTime
                },
                new UserDocument
                {
                    UserDocumentId = Guid.NewGuid(),
                    UserId = DriverID,
                    DocumentType = DocumentType.DRIVER_LINCENSE,
                    Status = VerifileStatus.ACTIVE,
                    IdentityNumber = "790123456789", // Số bằng lái
                    FullName = "TÀI XẾ VĂN A",
                    DateOfBirth = new DateTime(1990, 5, 15),
                    LicenseClass = "C", // Bằng C
                    IssueDate = new DateTime(2020, 1, 10),
                    ExpiryDate = new DateTime(2025, 1, 10),
                    IssuePlace = "Sở GTVT TP.HCM",
                    FrontImageUrl = "https://example.com/driver1_license_front.jpg",
                    BackImageUrl = "https://example.com/driver1_license_back.jpg",
                    IsDocumentReal = true,
                    CreatedAt = seedTime,
                    VerifiedAt = seedTime
                },

                // 2. DRIVER 2: Mới đăng ký, chưa xác thực (Để test luồng verify)
                // (Không seed data cho DriverID_2 để giả lập trường hợp chưa có gì)

                // 3. OWNER 1: Đã xác thực CCCD (Owner chỉ cần CCCD)
                new UserDocument
                {
                    UserDocumentId = Guid.NewGuid(),
                    UserId = OwnerID, // Owner Name
                    DocumentType = DocumentType.CCCD,
                    Status = VerifileStatus.ACTIVE,
                    IdentityNumber = "079085005678",
                    FullName = "OWNER NAME",
                    DateOfBirth = new DateTime(1985, 10, 20),
                    PlaceOfOrigin = "Hà Nội",
                    PlaceOfResidence = "456 Nguyễn Huệ, Q1, TP.HCM",
                    IssueDate = new DateTime(2018, 10, 20),
                    ExpiryDate = new DateTime(2038, 10, 20),
                    IssuePlace = "Cục Cảnh sát ĐKQL cư trú và DLQG về dân cư",
                    FrontImageUrl = "https://example.com/owner1_cccd_front.jpg",
                    BackImageUrl = "https://example.com/owner1_cccd_back.jpg",
                    PortraitImageUrl = "https://example.com/owner1_face.jpg",
                    IsDocumentReal = true,
                    FaceMatchScore = 95.0,
                    CreatedAt = seedTime,
                    VerifiedAt = seedTime
                },

                // 4. OWNER 2: Bị từ chối (Để test luồng Rejection / Manual Review)
                new UserDocument
                {
                    UserDocumentId = Guid.NewGuid(),
                    UserId = OwnerID_2, // Owner Name 2
                    DocumentType = DocumentType.CCCD,
                    Status = VerifileStatus.REJECTED, // Bị từ chối
                    RejectionReason = "Ảnh mờ, không nhìn rõ số CMND; Nghi vấn chỉnh sửa ảnh.",
                    FrontImageUrl = "https://example.com/owner2_cccd_front_blur.jpg",
                    BackImageUrl = "https://example.com/owner2_cccd_back.jpg",
                    CreatedAt = DateTime.UtcNow.AddDays(-1), // Tạo hôm qua
                    LastUpdatedAt = DateTime.UtcNow
                },

                // 5. PROVIDER 1: Đã xác thực
                new UserDocument
                {
                    UserDocumentId = Guid.NewGuid(),
                    UserId = ProviderID,
                    DocumentType = DocumentType.CCCD,
                    Status = VerifileStatus.ACTIVE,
                    IdentityNumber = "079088009999",
                    FullName = "PROVIDER NAME",
                    DateOfBirth = new DateTime(1988, 3, 1),
                    PlaceOfOrigin = "Đà Nẵng",
                    IssueDate = new DateTime(2019, 1, 1),
                    ExpiryDate = new DateTime(2039, 1, 1),
                    FrontImageUrl = "https://example.com/provider_cccd.jpg",
                    IsDocumentReal = true,
                    FaceMatchScore = 92.0,
                    CreatedAt = seedTime,
                    VerifiedAt = seedTime
                }
            );
        } 
        

    // ───────────────────────────────────────────────
        // 🔹 OWNER DRIVER LINKS (SEED ĐỘI XE) - [NEW]
        // ───────────────────────────────────────────────
        private static void SeedOwnerDriverLinks(ModelBuilder modelBuilder)
        {
            var seedTime = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);

            modelBuilder.Entity<OwnerDriverLink>().HasData(
                // 1. Mối quan hệ ĐÃ DUYỆT (Tài xế Văn A thuộc đội của Owner Name)
                new OwnerDriverLink
                {
                    OwnerDriverLinkId = Guid.NewGuid(),
                    OwnerId = OwnerID,
                    DriverId = DriverID, // Tài xế Văn A
                    Status = FleetJoinStatus.APPROVED, // Đã gia nhập thành công
                    RequestedAt = seedTime.AddDays(-10), // Gửi yêu cầu 10 ngày trước
                    ApprovedAt = seedTime.AddDays(-9)    // Được duyệt 9 ngày trước
                },

                // 2. Mối quan hệ ĐANG CHỜ (Tài xế Văn B xin vào đội của Owner Name)
                new OwnerDriverLink
                {
                    OwnerDriverLinkId = Guid.NewGuid(),
                    OwnerId = OwnerID,
                    DriverId = DriverID_2, // Tài xế Văn B
                    Status = FleetJoinStatus.PENDING, // Đang chờ duyệt
                    RequestedAt = DateTime.UtcNow.AddHours(-2), // Mới gửi yêu cầu 2 tiếng trước
                    ApprovedAt = null
                },

                // 3. (Optional) Mối quan hệ của Owner 2 với Tài xế Văn A (Một tài xế có thể thuộc nhiều đội? Tùy logic nghiệp vụ)
                // Giả sử logic là 1 tài xế chỉ thuộc 1 đội tại 1 thời điểm thì không nên seed cái này.
                // Nhưng nếu logic là Many-to-Many (Cộng tác viên) thì OK.
                new OwnerDriverLink
                {
                    OwnerDriverLinkId = Guid.NewGuid(),
                    OwnerId = OwnerID_2, // Owner Name 2
                    DriverId = DriverID, // Tài xế Văn A cũng đang pending bên này
                    Status = FleetJoinStatus.PENDING,
                    RequestedAt = DateTime.UtcNow.AddHours(-5),
                    ApprovedAt = null
                }
            );
        }
    }
    }