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
        // 🔹 1. ROLE IDs
        // ───────────────────────────────────────────────
        private static readonly Guid AdminRole = Guid.Parse("D4DAB1C3-6D48-4B23-8369-2D1C9C828F22");
        private static readonly Guid DriverRole = Guid.Parse("A1DAB1C3-6D48-4B23-8369-2D1C9C828F22");
        private static readonly Guid OwnerRole = Guid.Parse("B2DAB1C3-6D48-4B23-8369-2D1C9C828F22");
        private static readonly Guid StaffRole = Guid.Parse("E3DAB1C3-6D48-4B23-8369-2D1C9C828F22");
        private static readonly Guid ProviderRole = Guid.Parse("F5DAB1C3-6D48-4B23-8369-2D1C9C828F22");

        // ───────────────────────────────────────────────
        // 🔹 2. USER IDs
        // ───────────────────────────────────────────────
        private static readonly Guid AdminID = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid StaffID = Guid.Parse("22222222-2222-2222-2222-222222222222");

        // --- SPECIFIC USERS ---
        private static readonly Guid Driver_DangLaiKP_ID = Guid.Parse("33333333-3333-3333-3333-333333333301");
        private static readonly Guid Driver_DangLVH_ID = Guid.Parse("33333333-3333-3333-3333-333333333302");
        private static readonly Guid Owner_VoLuong_ID = Guid.Parse("44444444-4444-4444-4444-444444444401");
        private static readonly Guid Provider_HoangPM_ID = Guid.Parse("55555555-5555-5555-5555-555555555501");

        // --- SAMPLE USERS ---
        private static readonly Guid Driver_Sample1_ID = Guid.Parse("33333333-3333-3333-3333-333333333311");
        private static readonly Guid Driver_Sample2_ID = Guid.Parse("33333333-3333-3333-3333-333333333312");
        private static readonly Guid Owner_Sample1_ID = Guid.Parse("44444444-4444-4444-4444-444444444411");
        private static readonly Guid Owner_Sample2_ID = Guid.Parse("44444444-4444-4444-4444-444444444412");
        private static readonly Guid Provider_Sample1_ID = Guid.Parse("55555555-5555-5555-5555-555555555511");
        private static readonly Guid Provider_Sample2_ID = Guid.Parse("55555555-5555-5555-5555-555555555512");

        // ───────────────────────────────────────────────
        // 🔹 4. TEMPLATE IDs
        // ───────────────────────────────────────────────
        private static readonly Guid ProviderContractTemplateID = Guid.Parse("C0100001-0001-0000-0000-000000000001");
        private static readonly Guid DriverContractTemplateID = Guid.Parse("C0200001-0002-0000-0000-000000000001");
        private static readonly Guid PickupRecordTemplateID = Guid.Parse("D0100001-0003-0000-0000-000000000001");
        private static readonly Guid DropoffRecordTemplateID = Guid.Parse("D0200001-0004-0000-0000-000000000001");
        private static readonly Guid VehiclePickupTemplateID = Guid.Parse("D0300001-0005-0000-0000-000000000001");
        private static readonly Guid VehicleDropoffTemplateID = Guid.Parse("D0400001-0006-0000-0000-000000000001");

        // ───────────────────────────────────────────────
        // 🔹 5. VEHICLE TYPE IDs
        // ───────────────────────────────────────────────
        // ───────────────────────────────────────────────
        // 🔹 5. VEHICLE TYPE IDs (FULL LIST XE TẢI VN)
        // ───────────────────────────────────────────────
        // --- Xe Tải Nhẹ (Chạy phố/Nội đô) ---
        private static readonly Guid VT_Van = Guid.Parse("A0000000-0000-0000-0000-000000000001"); // Xe Van/Bán tải
        private static readonly Guid VT_1T = Guid.Parse("A0000001-0000-0000-0000-000000000001");  // 1 Tấn
        private static readonly Guid VT_1T25 = Guid.Parse("A0000001-0000-0000-0000-000000000002"); // 1.25 Tấn
        private static readonly Guid VT_1T5 = Guid.Parse("A0000001-0001-0000-0000-000000000001"); // 1.5 Tấn (Giữ ID cũ)
        private static readonly Guid VT_1T9 = Guid.Parse("A0000001-0000-0000-0000-000000000003"); // 1.9 Tấn (Vào phố)
        private static readonly Guid VT_2T4 = Guid.Parse("A0000002-0000-0000-0000-000000000001"); // 2.4 Tấn (Vào phố)
        private static readonly Guid VT_2T5 = Guid.Parse("A0000002-0002-0000-0000-000000000001"); // 2.5 Tấn (Giữ ID cũ)

        // --- Xe Tải Trung (Chạy liên tỉnh/KCN) ---
        private static readonly Guid VT_3T5 = Guid.Parse("A0000003-0000-0000-0000-000000000001"); // 3.5 Tấn
        private static readonly Guid VT_5T = Guid.Parse("A0000003-0003-0000-0000-000000000001");  // 5 Tấn (Giữ ID cũ)
        private static readonly Guid VT_6T = Guid.Parse("A0000003-0000-0000-0000-000000000002");  // 6-7 Tấn

        // --- Xe Tải Nặng (Đường dài/Bắc Nam) ---
        private static readonly Guid VT_8T = Guid.Parse("A0000004-0004-0000-0000-000000000001");  // 8 Tấn (2 chân) - Giữ ID cũ
        private static readonly Guid VT_3Chan = Guid.Parse("A0000004-0000-0000-0000-000000000002"); // 3 Chân (15 Tấn)
        private static readonly Guid VT_4Chan = Guid.Parse("A0000004-0000-0000-0000-000000000003"); // 4 Chân (18 Tấn)
        private static readonly Guid VT_5Chan = Guid.Parse("A0000004-0000-0000-0000-000000000004"); // 5 Chân (22 Tấn)

        // --- Xe Container & Đầu kéo ---
        private static readonly Guid VT_Container20 = Guid.Parse("A0000005-0000-0000-0000-000000000002"); // Cont 20 feet
        private static readonly Guid VT_Container40 = Guid.Parse("A0000005-0000-0000-0000-000000000003"); // Cont 40 feet
        private static readonly Guid VT_Container45 = Guid.Parse("A0000005-0000-0000-0000-000000000004"); // Cont 45 feet

        // --- Xe Chuyên Dụng ---
        // (Lưu ý: Giữ VT_Container cũ làm xe đông lạnh như bạn yêu cầu ở prompt trước để không lỗi code cũ)
        private static readonly Guid VT_DongLanh = Guid.Parse("A0000005-0000-0000-0000-000000000001"); // Xe Đông Lạnh (ID cũ là VT_Container)
        private static readonly Guid VT_Ben = Guid.Parse("A0000006-0000-0000-0000-000000000001");      // Xe Ben
        private static readonly Guid VT_CanCau = Guid.Parse("A0000006-0000-0000-0000-000000000002");   // Xe Cẩu

        // ───────────────────────────────────────────────
        // 🔹 MAIN SEED METHOD
        // ───────────────────────────────────────────────
        public static void Seed(ModelBuilder modelBuilder)
        {
            SeedRole(modelBuilder);

            // 1. Seed User & Derived Entities
            SeedUser(modelBuilder);     // Chỉ Admin & Staff
            SeedDriver(modelBuilder);   // Full Driver
            SeedOwner(modelBuilder);    // Full Owner
            SeedProvider(modelBuilder); // Full Provider

            // 2. Seed Address (Owned Type - Fix lỗi InvalidOperation)
            SeedAddress(modelBuilder);

            // 3. Other Data
            SeedWallets(modelBuilder);
            SeedContractTemplates(modelBuilder);
            SeedDeliveryRecordTemplates(modelBuilder);
            SeedVehicleType(modelBuilder);

            // 4. Documents & Links
            SeedUserDocuments(modelBuilder); // Fix lỗi thiếu FrontImageUrl
            SeedOwnerDriverLinks(modelBuilder);

            // 5. Truck Restrictions (Cấm tải)
            SeedTruckRestriction(modelBuilder);
        }

        // ───────────────────────────────────────────────
        // 🔹 SEED IMPLEMENTATION
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

        private static void SeedUser(ModelBuilder modelBuilder)
        {
            string pass = "$2a$11$rTz6DZiEeBqhVrzF25CgTOBPf41jpn2Tg/nnIqnX8KS6uIerB/1dm"; // Pass: "123"
            var now = DateTime.UtcNow;

            // Admin & Staff Only - Không gán Address ở đây
            modelBuilder.Entity<BaseUser>().HasData(
                new BaseUser { UserId = AdminID, FullName = "System Admin", Email = "admin@system.com", RoleId = AdminRole, PasswordHash = pass, PhoneNumber = "0909000999", Status = UserStatus.ACTIVE, CreatedAt = now, LastUpdatedAt = now, IsEmailVerified = true, IsPhoneVerified = true, AvatarUrl = null },
                new BaseUser { UserId = StaffID, FullName = "Support Staff", Email = "staff@system.com", RoleId = StaffRole, PasswordHash = pass, PhoneNumber = "0909000888", Status = UserStatus.ACTIVE, CreatedAt = now, LastUpdatedAt = now, IsEmailVerified = true, IsPhoneVerified = true, AvatarUrl = null }
            );
        }

        private static void SeedDriver(ModelBuilder modelBuilder)
        {
            string pass = "$2a$11$rTz6DZiEeBqhVrzF25CgTOBPf41jpn2Tg/nnIqnX8KS6uIerB/1dm";
            var now = DateTime.UtcNow;

            var drivers = new List<Driver>
            {
                new Driver
                {
                    UserId = Driver_DangLaiKP_ID, FullName = "Đặng Lai KP", Email = "danglaikp@gmail.com", RoleId = DriverRole, PasswordHash = pass, PhoneNumber = "0988111222", Status = UserStatus.ACTIVE, CreatedAt = now, IsEmailVerified = true, IsPhoneVerified = true, AvatarUrl = null,
                    DateOfBirth = new DateTime(1995, 1, 1), LicenseNumber = "GPLX_DLKP", LicenseClass = "C", LicenseExpiryDate = now.AddYears(5), IsLicenseVerified = true, DriverStatus = DriverStatus.AVAILABLE, HasDeclaredInitialHistory = true
                },
                new Driver
                {
                    UserId = Driver_DangLVH_ID, FullName = "Đặng LVH", Email = "danglvhse151369@fpt.edu.vn", RoleId = DriverRole, PasswordHash = pass, PhoneNumber = "0988111333", Status = UserStatus.ACTIVE, CreatedAt = now, IsEmailVerified = true, IsPhoneVerified = true, AvatarUrl = null,
                    DateOfBirth = new DateTime(1996, 1, 1), LicenseNumber = "GPLX_DLVH", LicenseClass = "C", LicenseExpiryDate = now.AddYears(5), IsLicenseVerified = true, DriverStatus = DriverStatus.AVAILABLE, HasDeclaredInitialHistory = true
                },
                new Driver
                {
                    UserId = Driver_Sample1_ID, FullName = "Tài xế Mẫu 1", Email = "driver1@gmail.com", RoleId = DriverRole, PasswordHash = pass, PhoneNumber = "0988000001", Status = UserStatus.ACTIVE, CreatedAt = now, IsEmailVerified = true, IsPhoneVerified = true, AvatarUrl = null,
                    DateOfBirth = new DateTime(1990, 1, 1), LicenseNumber = "GPLX_S1", LicenseClass = "C", LicenseExpiryDate = now.AddYears(5), IsLicenseVerified = true, DriverStatus = DriverStatus.AVAILABLE, HasDeclaredInitialHistory = true
                },
                new Driver
                {
                    UserId = Driver_Sample2_ID, FullName = "Tài xế Mẫu 2", Email = "driver2@gmail.com", RoleId = DriverRole, PasswordHash = pass, PhoneNumber = "0988000002", Status = UserStatus.ACTIVE, CreatedAt = now, IsEmailVerified = true, IsPhoneVerified = true, AvatarUrl = null,
                    DateOfBirth = new DateTime(1992, 1, 1), LicenseNumber = "GPLX_S2", LicenseClass = "B2", LicenseExpiryDate = now.AddYears(5), IsLicenseVerified = true, DriverStatus = DriverStatus.AVAILABLE, HasDeclaredInitialHistory = true
                }
            };
            modelBuilder.Entity<Driver>().HasData(drivers);
        }

        private static void SeedOwner(ModelBuilder modelBuilder)
        {
            string pass = "$2a$11$rTz6DZiEeBqhVrzF25CgTOBPf41jpn2Tg/nnIqnX8KS6uIerB/1dm";
            var now = DateTime.UtcNow;

            var owners = new List<Owner>
            {
                new Owner
                {
                    UserId = Owner_VoLuong_ID, FullName = "Võ Lương Nhựt Tiến", Email = "voluongnhuttien@gmail.com", RoleId = OwnerRole, PasswordHash = pass, PhoneNumber = "0911222333", Status = UserStatus.ACTIVE, CreatedAt = now, IsEmailVerified = true, IsPhoneVerified = true, AvatarUrl = null,
                    DateOfBirth = new DateTime(1985, 1, 1), CompanyName = "Công ty Vận tải Võ Lương", TaxCode = "MST001", AverageRating = 5.0m
                },
                new Owner
                {
                    UserId = Owner_Sample1_ID, FullName = "Chủ xe Mẫu 1", Email = "owner1@gmail.com", RoleId = OwnerRole, PasswordHash = pass, PhoneNumber = "0911000001", Status = UserStatus.ACTIVE, CreatedAt = now, IsEmailVerified = true, IsPhoneVerified = true, AvatarUrl = null,
                    DateOfBirth = new DateTime(1980, 1, 1), CompanyName = "Vận tải Mẫu 1", TaxCode = "MST002", AverageRating = 5.0m
                },
                new Owner
                {
                    UserId = Owner_Sample2_ID, FullName = "Chủ xe Mẫu 2", Email = "owner2@gmail.com", RoleId = OwnerRole, PasswordHash = pass, PhoneNumber = "0911000002", Status = UserStatus.ACTIVE, CreatedAt = now, IsEmailVerified = true, IsPhoneVerified = true, AvatarUrl = null,
                    DateOfBirth = new DateTime(1982, 1, 1), CompanyName = "Vận tải Mẫu 2", TaxCode = "MST003", AverageRating = 5.0m
                }
            };
            modelBuilder.Entity<Owner>().HasData(owners);
        }

        private static void SeedProvider(ModelBuilder modelBuilder)
        {
            string pass = "$2a$11$rTz6DZiEeBqhVrzF25CgTOBPf41jpn2Tg/nnIqnX8KS6uIerB/1dm";
            var now = DateTime.UtcNow;

            var providers = new List<Provider>
            {
                new Provider
                {
                    UserId = Provider_HoangPM_ID, FullName = "Hoàng PM", Email = "hoangpmse171847@fpt.edu.vn", RoleId = ProviderRole, PasswordHash = pass, PhoneNumber = "0933444555", Status = UserStatus.ACTIVE, CreatedAt = now, IsEmailVerified = true, IsPhoneVerified = true, AvatarUrl = null,
                    DateOfBirth = new DateTime(1990, 5, 5), CompanyName = "Nông sản Hoàng PM", TaxCode = "MST_P001", AverageRating = 4.9m
                },
                new Provider
                {
                    UserId = Provider_Sample1_ID, FullName = "Nhà cung cấp Mẫu 1", Email = "provider1@gmail.com", RoleId = ProviderRole, PasswordHash = pass, PhoneNumber = "0933000001", Status = UserStatus.ACTIVE, CreatedAt = now, IsEmailVerified = true, IsPhoneVerified = true, AvatarUrl = null,
                    DateOfBirth = new DateTime(1990, 5, 5), CompanyName = "Kho hàng Mẫu 1", TaxCode = "MST_P002", AverageRating = 4.9m
                },
                new Provider
                {
                    UserId = Provider_Sample2_ID, FullName = "Nhà cung cấp Mẫu 2", Email = "provider2@gmail.com", RoleId = ProviderRole, PasswordHash = pass, PhoneNumber = "0933000002", Status = UserStatus.ACTIVE, CreatedAt = now, IsEmailVerified = true, IsPhoneVerified = true, AvatarUrl = null,
                    DateOfBirth = new DateTime(1990, 5, 5), CompanyName = "Kho hàng Mẫu 2", TaxCode = "MST_P003", AverageRating = 4.9m
                }
            };
            modelBuilder.Entity<Provider>().HasData(providers);
        }

        private static void SeedAddress(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BaseUser>().OwnsOne(u => u.Address).HasData(
                new { BaseUserUserId = AdminID, Address = "Lô E2a-7, Đường D1, Khu Công Nghệ Cao, P.Long Thạnh Mỹ, TP.Thủ Đức, TP.HCM", Latitude = 10.855323, Longitude = 106.785243 },
                new { BaseUserUserId = StaffID, Address = "Tòa nhà Bitexco, số 2 Hải Triều, Bến Nghé, Quận 1, TP.HCM", Latitude = 10.771587, Longitude = 106.704257 },
                new { BaseUserUserId = Driver_DangLaiKP_ID, Address = "395 Kinh Dương Vương, An Lạc, Bình Tân, TP.HCM", Latitude = 10.742353, Longitude = 106.615654 },
                new { BaseUserUserId = Driver_DangLVH_ID, Address = "102 Lê Văn Việt, Hiệp Phú, TP.Thủ Đức, TP.HCM", Latitude = 10.846512, Longitude = 106.774921 },
                new { BaseUserUserId = Driver_Sample1_ID, Address = "QL22, Bà Điểm, Hóc Môn, TP.HCM", Latitude = 10.843940, Longitude = 106.612883 },
                new { BaseUserUserId = Driver_Sample2_ID, Address = "Lô II, Đường số 2, KCN Tân Bình, Tây Thạnh, Tân Phú, TP.HCM", Latitude = 10.806623, Longitude = 106.626354 },
                new { BaseUserUserId = Owner_VoLuong_ID, Address = "1295 Nguyễn Thị Định, Cát Lái, TP.Thủ Đức, TP.HCM", Latitude = 10.767895, Longitude = 106.775874 },
                new { BaseUserUserId = Owner_Sample1_ID, Address = "456 Nguyễn Văn Linh, Tân Phú, Quận 7, TP.HCM", Latitude = 10.729355, Longitude = 106.721833 },
                new { BaseUserUserId = Owner_Sample2_ID, Address = "Đại lộ Bình Dương, Phú Thọ, Thủ Dầu Một, Bình Dương", Latitude = 10.980645, Longitude = 106.674391 },
                new { BaseUserUserId = Provider_HoangPM_ID, Address = "QL1A, Tam Bình, TP.Thủ Đức, TP.HCM", Latitude = 10.868743, Longitude = 106.745421 },
                new { BaseUserUserId = Provider_Sample1_ID, Address = "Đại lộ Độc Lập, An Bình, Dĩ An, Bình Dương", Latitude = 10.902621, Longitude = 106.747388 },
                new { BaseUserUserId = Provider_Sample2_ID, Address = "Đại lộ Nguyễn Văn Linh, Khu Phố 6, Quận 8, TP.HCM", Latitude = 10.716321, Longitude = 106.626743 }
            );
        }

        private static void SeedWallets(ModelBuilder modelBuilder)
        {
            var now = DateTime.UtcNow;
            decimal defaultBalance = 500_000_000m;
            decimal providerSpecialBalance = 14_900_000m;

            var wallets = new List<Wallet>
            {
                new Wallet { WalletId = Guid.NewGuid(), UserId = AdminID, Balance = defaultBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = now, LastUpdatedAt = now },
                new Wallet { WalletId = Guid.NewGuid(), UserId = StaffID, Balance = defaultBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = now, LastUpdatedAt = now },
                new Wallet { WalletId = Guid.NewGuid(), UserId = Driver_DangLaiKP_ID, Balance = defaultBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = now, LastUpdatedAt = now },
                new Wallet { WalletId = Guid.NewGuid(), UserId = Driver_DangLVH_ID, Balance = defaultBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = now, LastUpdatedAt = now },
                new Wallet { WalletId = Guid.NewGuid(), UserId = Driver_Sample1_ID, Balance = defaultBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = now, LastUpdatedAt = now },
                new Wallet { WalletId = Guid.NewGuid(), UserId = Driver_Sample2_ID, Balance = defaultBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = now, LastUpdatedAt = now },
                new Wallet { WalletId = Guid.NewGuid(), UserId = Owner_VoLuong_ID, Balance = defaultBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = now, LastUpdatedAt = now },
                new Wallet { WalletId = Guid.NewGuid(), UserId = Owner_Sample1_ID, Balance = defaultBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = now, LastUpdatedAt = now },
                new Wallet { WalletId = Guid.NewGuid(), UserId = Owner_Sample2_ID, Balance = defaultBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = now, LastUpdatedAt = now },
                new Wallet { WalletId = Guid.NewGuid(), UserId = Provider_HoangPM_ID, Balance = providerSpecialBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = now, LastUpdatedAt = now },
                new Wallet { WalletId = Guid.NewGuid(), UserId = Provider_Sample1_ID, Balance = defaultBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = now, LastUpdatedAt = now },
                new Wallet { WalletId = Guid.NewGuid(), UserId = Provider_Sample2_ID, Balance = defaultBalance, Currency = "VND", Status = WalletStatus.ACTIVE, CreatedAt = now, LastUpdatedAt = now }
            };

            modelBuilder.Entity<Wallet>().HasData(wallets);
        }

        private static void SeedContractTemplates(ModelBuilder modelBuilder)
        {
            var now = DateTime.UtcNow;

            // 1. TẠO TEMPLATE HỢP ĐỒNG
            modelBuilder.Entity<ContractTemplate>().HasData(
                new ContractTemplate
                {
                    ContractTemplateId = ProviderContractTemplateID,
                    ContractTemplateName = "Hợp đồng Vận chuyển Hàng hóa (Owner - Provider)",
                    Version = "3.0 VN - Full",
                    Type = ContractType.PROVIDER_CONTRACT,
                    CreatedAt = now
                },
                new ContractTemplate
                {
                    ContractTemplateId = DriverContractTemplateID,
                    ContractTemplateName = "Hợp đồng Hợp tác/Thuê Tài xế (Owner - Driver)",
                    Version = "3.0 VN - Full",
                    Type = ContractType.DRIVER_CONTRACT,
                    CreatedAt = now
                }
            );

            // 2. TẠO CÁC ĐIỀU KHOẢN (TERMS)
            modelBuilder.Entity<ContractTerm>().HasData(

                // ==================================================================================
                // A. HỢP ĐỒNG VẬN CHUYỂN (PROVIDER_CONTRACT) - 10 ĐIỀU
                // ==================================================================================
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = ProviderContractTemplateID,
                    Order = 1,
                    Content = "Điều 1: Trách nhiệm Bên A (Chủ xe): Cam kết cung cấp phương tiện đúng chủng loại, tải trọng, sạch sẽ và đảm bảo kỹ thuật để vận hành an toàn."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = ProviderContractTemplateID,
                    Order = 2,
                    Content = "Điều 2: Trách nhiệm Bên B (Chủ hàng): Chịu trách nhiệm về tính pháp lý của hàng hóa, đảm bảo không vận chuyển hàng cấm, hàng lậu."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = ProviderContractTemplateID,
                    Order = 3,
                    Content = "Điều 3: Quy cách đóng gói: Bên B có trách nhiệm đóng gói hàng hóa chắc chắn, dán nhãn mác rõ ràng trước khi giao cho Bên A."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = ProviderContractTemplateID,
                    Order = 4,
                    Content = "Điều 4: Thời gian và Địa điểm: Bên A cam kết có mặt tại điểm lấy hàng và giao hàng đúng khung giờ đã thỏa thuận trên hệ thống."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = ProviderContractTemplateID,
                    Order = 5,
                    Content = "Điều 5: Bốc xếp hàng hóa: Cước vận chuyển chưa bao gồm phí bốc xếp hai đầu, trừ khi có thỏa thuận khác được ghi nhận trên đơn hàng."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = ProviderContractTemplateID,
                    Order = 6,
                    Content = "Điều 6: Thanh toán: Bên B thanh toán 100% cước phí vận chuyển qua ví điện tử của hệ thống ngay sau khi Tài xế xác nhận giao hàng thành công."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = ProviderContractTemplateID,
                    Order = 7,
                    Content = "Điều 7: Bảo hiểm và Đền bù: Bên A chịu trách nhiệm bồi thường 100% giá trị hàng hóa nếu xảy ra mất mát, hư hỏng do lỗi chủ quan trong quá trình vận chuyển."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = ProviderContractTemplateID,
                    Order = 8,
                    Content = "Điều 8: Bất khả kháng: Các bên được miễn trừ trách nhiệm trong trường hợp thiên tai, dịch bệnh, hỏa hoạn hoặc quyết định cấm đường đột xuất của cơ quan chức năng."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = ProviderContractTemplateID,
                    Order = 9,
                    Content = "Điều 9: Phạt vi phạm: Bên nào đơn phương hủy chuyến muộn hơn thời gian quy định sẽ chịu phạt theo chính sách hủy chuyến của nền tảng."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = ProviderContractTemplateID,
                    Order = 10,
                    Content = "Điều 10: Cam kết chung: Hai bên cam kết thực hiện đúng các điều khoản trên và tuân thủ các quy định của pháp luật Việt Nam hiện hành."
                },

                // ==================================================================================
                // B. HỢP ĐỒNG TÀI XẾ (DRIVER_CONTRACT) - 10 ĐIỀU
                // ==================================================================================
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = DriverContractTemplateID,
                    Order = 1,
                    Content = "Điều 1: Phạm vi hợp tác: Bên A (Chủ xe) giao phương tiện cho Bên B (Tài xế) để khai thác dịch vụ vận tải trên nền tảng."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = DriverContractTemplateID,
                    Order = 2,
                    Content = "Điều 2: Điều kiện Tài xế: Bên B cam kết có Giấy phép lái xe hợp lệ, đủ sức khỏe, không sử dụng rượu bia và chất kích thích khi làm việc."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = DriverContractTemplateID,
                    Order = 3,
                    Content = "Điều 3: Quản lý tài sản: Bên B có trách nhiệm giữ gìn vệ sinh xe, bảo quản các trang thiết bị đi kèm (kích, lốp dự phòng, bình chữa cháy)."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = DriverContractTemplateID,
                    Order = 4,
                    Content = "Điều 4: Tuân thủ luật giao thông: Bên B chịu trách nhiệm hoàn toàn về các khoản phạt vi phạm giao thông (phạt nóng và phạt nguội) phát sinh trong thời gian cầm lái."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = DriverContractTemplateID,
                    Order = 5,
                    Content = "Điều 5: Chi phí vận hành: Chi phí nhiên liệu, phí cầu đường (nếu không được bên thuê bao) sẽ được phân chia theo thỏa thuận cụ thể trên từng chuyến."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = DriverContractTemplateID,
                    Order = 6,
                    Content = "Điều 6: Thu nhập và Thanh toán: Thu nhập của Bên B được hệ thống tính toán tự động dựa trên doanh thu chuyến đi và được chuyển vào ví cá nhân."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = DriverContractTemplateID,
                    Order = 7,
                    Content = "Điều 7: Ứng xử và Thái độ: Bên B cam kết mặc đúng đồng phục (nếu có), giao tiếp lịch sự với khách hàng và hỗ trợ bốc xếp nhẹ khi cần thiết."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = DriverContractTemplateID,
                    Order = 8,
                    Content = "Điều 8: Trung thực: Nghiêm cấm hành vi gian lận cước phí, tự ý nhận hàng ngoài hệ thống hoặc thông đồng với khách hàng để trục lợi."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = DriverContractTemplateID,
                    Order = 9,
                    Content = "Điều 9: Xử lý sự cố: Trong trường hợp xảy ra tai nạn, va quẹt, Bên B phải giữ nguyên hiện trường, báo ngay cho Bên A và cơ quan chức năng."
                },
                new ContractTerm
                {
                    ContractTermId = Guid.NewGuid(),
                    ContractTemplateId = DriverContractTemplateID,
                    Order = 10,
                    Content = "Điều 10: Chấm dứt hợp đồng: Bên A có quyền đơn phương chấm dứt hợp đồng và thu hồi xe nếu Bên B vi phạm nghiêm trọng các quy định an toàn hoặc gian lận."
                }
            );
        }

        private static void SeedDeliveryRecordTemplates(ModelBuilder modelBuilder)
        {
            var now = DateTime.UtcNow;

            // 1. TẠO CÁC MẪU BIÊN BẢN (TEMPLATES)
            modelBuilder.Entity<DeliveryRecordTemplate>().HasData(
                new DeliveryRecordTemplate
                {
                    DeliveryRecordTemplateId = PickupRecordTemplateID,
                    TemplateName = "Biên bản Giao Nhận Hàng (Pickup Checklist)",
                    Version = "3.0 VN - Full",
                    Type = DeliveryRecordType.PICKUP,
                    Status = DeliveryRecordTemplateStatus.ACTIVE,
                    CreatedAt = now
                },
                new DeliveryRecordTemplate
                {
                    DeliveryRecordTemplateId = DropoffRecordTemplateID,
                    TemplateName = "Biên bản Bàn Giao Hàng (Dropoff Checklist)",
                    Version = "3.0 VN - Full",
                    Type = DeliveryRecordType.DROPOFF,
                    Status = DeliveryRecordTemplateStatus.ACTIVE,
                    CreatedAt = now
                },
                new DeliveryRecordTemplate
                {
                    DeliveryRecordTemplateId = VehiclePickupTemplateID,
                    TemplateName = "Biên bản Bàn Giao Xe (Giao cho Tài xế)",
                    Version = "3.0 VN - Full",
                    Type = DeliveryRecordType.HANDOVER,
                    Status = DeliveryRecordTemplateStatus.ACTIVE,
                    CreatedAt = now
                },
                new DeliveryRecordTemplate
                {
                    DeliveryRecordTemplateId = VehicleDropoffTemplateID,
                    TemplateName = "Biên bản Thu Hồi Xe (Nhận lại từ Tài xế)",
                    Version = "3.0 VN - Full",
                    Type = DeliveryRecordType.RETURN,
                    Status = DeliveryRecordTemplateStatus.ACTIVE,
                    CreatedAt = now
                }
            );

            // 2. TẠO CHI TIẾT 10 TERMS CHO MỖI BIÊN BẢN
            modelBuilder.Entity<DeliveryRecordTerm>().HasData(

                // ==================================================================================
                // A. PICKUP (NHẬN HÀNG) - 10 TERMS
                // ==================================================================================
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 1, Content = "Kiểm tra số lượng thực tế khớp với Phiếu xuất kho/Vận đơn." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 2, Content = "Kiểm tra tình trạng bao bì: Nguyên vẹn, không móp méo, rách, ẩm ướt." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 3, Content = "Nhãn mác hàng hóa (Shipping Mark) đầy đủ, rõ ràng, đúng mã hàng." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 4, Content = "Đã nhận đủ chứng từ đi đường (Hóa đơn GTGT/Phiếu vận chuyển nội bộ)." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 5, Content = "Hàng hóa đã được xếp lên xe an toàn, phân bổ trọng lượng đều." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 6, Content = "Đã thực hiện chèn lót, chằng buộc (Lashing) chắc chắn." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 7, Content = "Kiểm tra nhiệt độ hàng hóa (đối với hàng đông lạnh/mát) đạt yêu cầu." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 8, Content = "Xác nhận tải trọng xe không vượt quá mức cho phép sau khi xếp hàng." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 9, Content = "Chụp ảnh hiện trạng hàng hóa sau khi xếp xong lên thùng xe." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 10, Content = "Tài xế và Kho/Chủ hàng cùng ký xác nhận vào biên bản nhận hàng." },

                // ==================================================================================
                // B. DROPOFF (TRẢ HÀNG) - 10 TERMS
                // ==================================================================================
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 1, Content = "Kiểm tra kẹp chì/niêm phong (Seal) còn nguyên vẹn trước khi mở thùng." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 2, Content = "Hàng hóa được dỡ xuống an toàn, đúng quy trình." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 3, Content = "Kiểm đếm số lượng hàng thực nhận so với chứng từ giao hàng." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 4, Content = "Kiểm tra ngoại quan bao bì tại điểm giao (không vỡ, móp trong quá trình vận chuyển)." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 5, Content = "Ghi nhận hàng hư hỏng/thiếu hụt (nếu có) vào biên bản sự cố." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 6, Content = "Bàn giao đầy đủ hóa đơn, chứng từ gốc cho người nhận." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 7, Content = "Thu hồi các chứng từ cần ký nhận (POD - Proof of Delivery) mang về." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 8, Content = "Thu hồi Pallet/Vật tư luân chuyển (nếu có yêu cầu đổi trả)." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 9, Content = "Thực hiện thu hộ (COD) và xác nhận số tiền (nếu có)." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 10, Content = "Người nhận ký, ghi rõ họ tên và đóng dấu xác nhận đã nhận đủ hàng." },

                // ==================================================================================
                // C. HANDOVER (GIAO XE CHO TÀI XẾ) - 10 TERMS
                // ==================================================================================
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 1, Content = "Bàn giao bộ giấy tờ xe gốc (Đăng ký, Đăng kiểm, Bảo hiểm TNDS, Phù hiệu)." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 2, Content = "Ngoại thất: Sơn vỏ không trầy xước mới, kính chắn gió/gương chiếu hậu nguyên vẹn." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 3, Content = "Hệ thống đèn (Pha, cos, xi-nhan, đèn hậu, đèn phanh) hoạt động tốt." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 4, Content = "Lốp xe (4 bánh + lốp dự phòng): Đủ áp suất, gai lốp tốt, đủ ốc tắc-kê." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 5, Content = "Khoang động cơ: Nước làm mát, dầu máy, dầu phanh, nước rửa kính ở mức chuẩn." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 6, Content = "Nội thất cabin: Sạch sẽ, ghế ngồi/dây đai an toàn hoạt động tốt." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 7, Content = "Hệ thống điện: Điều hòa, còi, gạt mưa, màn hình/radio hoạt động bình thường." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 8, Content = "Dụng cụ theo xe: Con đội, tay mở lốp, bình chữa cháy, tam giác cảnh báo đầy đủ." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 9, Content = "Thiết bị giám sát hành trình (GPS) và Camera nghị định 10 hoạt động tốt." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 10, Content = "Ghi nhận chỉ số Odometer (Km) và mức nhiên liệu hiện tại trên đồng hồ." },

                // ==================================================================================
                // D. RETURN (THU HỒI XE TỪ TÀI XẾ) - 10 TERMS
                // ==================================================================================
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehicleDropoffTemplateID, DisplayOrder = 1, Content = "Kiểm tra ngoại thất: Không phát sinh vết va quẹt, móp méo mới so với biên bản giao." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehicleDropoffTemplateID, DisplayOrder = 2, Content = "Kiểm tra lốp xe: Không bị rách, phù, hoặc mòn bất thường." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehicleDropoffTemplateID, DisplayOrder = 3, Content = "Nội thất cabin đã được dọn dẹp sạch sẽ, không để lại rác thải cá nhân." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehicleDropoffTemplateID, DisplayOrder = 4, Content = "Sàn thùng xe/Thùng container sạch sẽ, đã được quét dọn/rửa sau khi trả hàng." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehicleDropoffTemplateID, DisplayOrder = 5, Content = "Bàn giao lại đầy đủ chìa khóa xe và bộ giấy tờ xe gốc." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehicleDropoffTemplateID, DisplayOrder = 6, Content = "Kiểm tra đầy đủ dụng cụ theo xe (Con đội, bình chữa cháy,...) như lúc giao." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehicleDropoffTemplateID, DisplayOrder = 7, Content = "Xác nhận mức nhiên liệu hoàn trả (Bù trừ chi phí nếu thiếu so với thỏa thuận)." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehicleDropoffTemplateID, DisplayOrder = 8, Content = "Kiểm tra sơ bộ động cơ, không có tiếng kêu lạ hoặc báo đèn lỗi (Check Engine)." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehicleDropoffTemplateID, DisplayOrder = 9, Content = "Tra cứu phạt nguội: Chưa ghi nhận vi phạm giao thông trong thời gian sử dụng." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehicleDropoffTemplateID, DisplayOrder = 10, Content = "Tài xế bàn giao lại các chứng từ phát sinh (vé cầu đường, biên lai) nếu có." }
            );
        }

        private static void SeedVehicleType(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<VehicleType>().HasData(
                // =========================================================
                // 1. NHÓM XE TẢI NHẸ (LIGHT TRUCKS) - CHẠY NỘI ĐÔ
                // =========================================================
                new VehicleType
                {
                    VehicleTypeId = VT_Van,
                    VehicleTypeName = "Xe Tải Van (500kg - 990kg)",
                    Description = "Xe tải van cỡ nhỏ, được phép lưu thông trong nội đô 24/24, không bị cấm giờ."
                },
                new VehicleType
                {
                    VehicleTypeId = VT_1T,
                    VehicleTypeName = "Xe Tải 1 Tấn",
                    Description = "Xe tải nhỏ, phù hợp chuyển nhà, chở hàng tiêu dùng nhẹ."
                },
                new VehicleType
                {
                    VehicleTypeId = VT_1T25,
                    VehicleTypeName = "Xe Tải 1.25 Tấn",
                    Description = "Xe tải nhẹ phổ biến, kích thước nhỏ gọn dễ luồn lách."
                },
                new VehicleType
                {
                    VehicleTypeId = VT_1T5,
                    VehicleTypeName = "Xe Tải 1.5 Tấn",
                    Description = "Xe tải hạng nhẹ, phù hợp phân phối hàng hóa từ kho tổng vào đại lý."
                },
                new VehicleType
                {
                    VehicleTypeId = VT_1T9,
                    VehicleTypeName = "Xe Tải 1.9 Tấn",
                    Description = "Tải trọng tối ưu để vào phố ban ngày (tổng trọng tải dưới 5 tấn)."
                },
                new VehicleType
                {
                    VehicleTypeId = VT_2T4,
                    VehicleTypeName = "Xe Tải 2.4 Tấn",
                    Description = "Dòng xe tải cao nhất được phép vào nội đô (có giấy phép) ở một số thành phố."
                },
                new VehicleType
                {
                    VehicleTypeId = VT_2T5,
                    VehicleTypeName = "Xe Tải 2.5 Tấn",
                    Description = "Xe tải nhẹ thùng rộng, thường bị cấm giờ cao điểm tại các thành phố lớn."
                },

                // =========================================================
                // 2. NHÓM XE TẢI TRUNG (MEDIUM TRUCKS) - LIÊN TỈNH/KCN
                // =========================================================
                new VehicleType
                {
                    VehicleTypeId = VT_3T5,
                    VehicleTypeName = "Xe Tải 3.5 Tấn",
                    Description = "Xe tải tầm trung, phù hợp chở hàng nguyên vật liệu, nông sản liên tỉnh cự ly ngắn."
                },
                new VehicleType
                {
                    VehicleTypeId = VT_5T,
                    VehicleTypeName = "Xe Tải 5 Tấn",
                    Description = "Xe tải trung thùng dài 6m2, chuyên chở sắt thép, ống nhựa, hàng cồng kềnh."
                },
                new VehicleType
                {
                    VehicleTypeId = VT_6T,
                    VehicleTypeName = "Xe Tải 6-7 Tấn",
                    Description = "Xe tải thùng lớn, tải trọng cao, chuyên chạy tuyến Bắc - Nam hoặc liên tỉnh."
                },

                // =========================================================
                // 3. NHÓM XE TẢI NẶNG (HEAVY TRUCKS) - ĐƯỜNG DÀI
                // =========================================================
                new VehicleType
                {
                    VehicleTypeId = VT_8T,
                    VehicleTypeName = "Xe Tải 8 Tấn (2 Chân)",
                    Description = "Xe hạng nặng 2 trục, thùng dài 8m-9m, chuyên chở hàng logistics đường dài."
                },
                new VehicleType
                {
                    VehicleTypeId = VT_3Chan,
                    VehicleTypeName = "Xe Tải 3 Chân (15 Tấn)",
                    Description = "Xe 3 trục, tải trọng thực chở ~15 tấn, thùng dài, ổn định cao."
                },
                new VehicleType
                {
                    VehicleTypeId = VT_4Chan,
                    VehicleTypeName = "Xe Tải 4 Chân (18 Tấn)",
                    Description = "Xe 4 trục, tải trọng thực chở ~18 tấn, phù hợp chở nông sản, máy móc hạng nặng."
                },
                new VehicleType
                {
                    VehicleTypeId = VT_5Chan,
                    VehicleTypeName = "Xe Tải 5 Chân (22 Tấn)",
                    Description = "Xe 5 trục, tải trọng tối đa ~22 tấn, thay thế container đi các tỉnh vùng cao."
                },

                // =========================================================
                // 4. NHÓM XE CONTAINER & ĐẦU KÉO
                // =========================================================
                //new VehicleType
                //{
                //    VehicleTypeId = VT_Container20,
                //    VehicleTypeName = "Đầu Kéo Container 20 Feet",
                //    Description = "Xe đầu kéo kéo rơ-moóc chở container 20ft (hàng nặng, gọn)."
                //},
                //new VehicleType
                //{
                //    VehicleTypeId = VT_Container40,
                //    VehicleTypeName = "Đầu Kéo Container 40 Feet",
                //    Description = "Xe đầu kéo kéo rơ-moóc chở container 40ft (hàng nhẹ, cồng kềnh, hàng may mặc)."
                //},
                //new VehicleType
                //{
                //    VehicleTypeId = VT_Container45,
                //    VehicleTypeName = "Đầu Kéo Container 45 Feet",
                //    Description = "Xe đầu kéo siêu trường, chở container 45ft, dung tích chứa hàng cực lớn."
                //},

                // =========================================================
                // 5. NHÓM XE CHUYÊN DỤNG (SPECIALIZED)
                // =========================================================
                new VehicleType
                {
                    VehicleTypeId = VT_DongLanh, // Đây là VT_Container cũ của bạn
                    VehicleTypeName = "Xe Tải Đông Lạnh (Refrigerated)",
                    Description = "Xe tải thùng bảo ôn có máy lạnh, chuyên chở thực phẩm tươi sống, vắc xin, hàng đông lạnh."
                },
                new VehicleType
                {
                    VehicleTypeId = VT_Ben,
                    VehicleTypeName = "Xe Ben (Dump Truck)",
                    Description = "Xe tải tự đổ, chuyên chở vật liệu xây dựng (cát, đá, sỏi) rời."
                },
                new VehicleType
                {
                    VehicleTypeId = VT_CanCau,
                    VehicleTypeName = "Xe Tải Gắn Cẩu",
                    Description = "Xe tải thùng có gắn cẩu tự hành, phù hợp chở cây cảnh, sắt thép, máy móc cần nâng hạ."
                }
            );
        }

        // 🔹 FIX LỖI USER DOCUMENT: ĐẢM BẢO CÓ FRONT IMAGE URL
        private static void SeedUserDocuments(ModelBuilder modelBuilder)
        {
            var docs = new List<UserDocument>();
            var now = DateTime.UtcNow;
            var rand = new Random();

            var allUsers = new List<(Guid Id, string Name)>
            {
                (Driver_DangLaiKP_ID, "DANG LAI KP"), (Driver_DangLVH_ID, "DANG LVH"),
                (Driver_Sample1_ID, "TAI XE MAU 1"), (Driver_Sample2_ID, "TAI XE MAU 2"),
                (Owner_VoLuong_ID, "VO LUONG NHUT TIEN"), (Owner_Sample1_ID, "CHU XE MAU 1"), (Owner_Sample2_ID, "CHU XE MAU 2"),
                (Provider_HoangPM_ID, "HOANG PM"), (Provider_Sample1_ID, "PROVIDER MAU 1"), (Provider_Sample2_ID, "PROVIDER MAU 2")
            };

            // 1. CCCD
            foreach (var user in allUsers)
            {
                docs.Add(new UserDocument
                {
                    UserDocumentId = Guid.NewGuid(),
                    UserId = user.Id,
                    DocumentType = DocumentType.CCCD,
                    Status = VerifileStatus.ACTIVE,
                    IdentityNumber = "07909" + rand.Next(1000000, 9999999),
                    FullName = user.Name,
                    DateOfBirth = new DateTime(1990, 1, 1),
                    IssueDate = now.AddYears(-2),
                    ExpiryDate = now.AddYears(10),
                    FrontImageUrl = "https://example.com/cccd_front.jpg", // <--- BẮT BUỘC CÓ
                    BackImageUrl = "https://example.com/cccd_back.jpg",
                    IsDocumentReal = true,
                    VerifiedAt = now
                });
            }

            // 2. GPLX & GKSK (Drivers Only)
            var drivers = new List<Guid> { Driver_DangLaiKP_ID, Driver_DangLVH_ID, Driver_Sample1_ID, Driver_Sample2_ID };
            foreach (var drvId in drivers)
            {
                // GPLX
                docs.Add(new UserDocument
                {
                    UserDocumentId = Guid.NewGuid(),
                    UserId = drvId,
                    DocumentType = DocumentType.DRIVER_LINCENSE,
                    Status = VerifileStatus.ACTIVE,
                    IdentityNumber = "GPLX" + rand.Next(100000, 999999),
                    IssueDate = now.AddYears(-1),
                    ExpiryDate = now.AddYears(4),
                    FrontImageUrl = "https://example.com/gplx_front.jpg", // <--- BẮT BUỘC CÓ
                    BackImageUrl = "https://example.com/gplx_back.jpg",
                    IsDocumentReal = true,
                    VerifiedAt = now
                });

                // GKSK
                docs.Add(new UserDocument
                {
                    UserDocumentId = Guid.NewGuid(),
                    UserId = drvId,
                    DocumentType = DocumentType.HEALTH_CHECK,
                    Status = VerifileStatus.ACTIVE,
                    IssueDate = now.AddMonths(-1),
                    ExpiryDate = now.AddMonths(5),
                    FrontImageUrl = "https://example.com/health_front.jpg", // <--- BẮT BUỘC CÓ
                    // BackImageUrl = null (Tùy entity config, nhưng Front là bắt buộc)
                    IsDocumentReal = true,
                    VerifiedAt = now
                });
            }
            modelBuilder.Entity<UserDocument>().HasData(docs);
        }

        private static void SeedOwnerDriverLinks(ModelBuilder modelBuilder)
        {
            var now = DateTime.UtcNow;
            modelBuilder.Entity<OwnerDriverLink>().HasData(
                new OwnerDriverLink { OwnerDriverLinkId = Guid.NewGuid(), OwnerId = Owner_VoLuong_ID, DriverId = Driver_Sample1_ID, Status = FleetJoinStatus.APPROVED, RequestedAt = now.AddDays(-10), ApprovedAt = now.AddDays(-9) },
                new OwnerDriverLink { OwnerDriverLinkId = Guid.NewGuid(), OwnerId = Owner_VoLuong_ID, DriverId = Driver_Sample2_ID, Status = FleetJoinStatus.PENDING, RequestedAt = now.AddHours(-2) }
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 SEED TRUCK RESTRICTION (FULL VIETNAM)
        // ───────────────────────────────────────────────
        private static void SeedTruckRestriction(ModelBuilder modelBuilder)
        {
            var restrictions = new List<TruckRestriction>();

            // =================================================================================
            // 1. TP. HỒ CHÍ MINH (Quyết định 23/2018/QĐ-UBND)
            // =================================================================================

            // --- Xe tải nhẹ (Dưới 2.5 tấn) ---
            // Cấm cao điểm Sáng (6h-9h) và Chiều (16h-20h)
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "TP.HCM - Nội Đô",
                TruckType = "LightTruck",
                BanStartTime = new TimeSpan(6, 0, 0),
                BanEndTime = new TimeSpan(9, 0, 0),
                Description = "Cấm xe tải nhẹ vào nội đô giờ cao điểm Sáng"
            });
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "TP.HCM - Nội Đô",
                TruckType = "LightTruck",
                BanStartTime = new TimeSpan(16, 0, 0),
                BanEndTime = new TimeSpan(20, 0, 0),
                Description = "Cấm xe tải nhẹ vào nội đô giờ cao điểm Chiều"
            });

            // --- Xe tải nặng & Container ---
            // Cấm lưu thông từ 6h đến 22h (Chỉ chạy đêm)
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "TP.HCM - Nội Đô",
                TruckType = "HeavyTruck",
                BanStartTime = new TimeSpan(6, 0, 0),
                BanEndTime = new TimeSpan(22, 0, 0),
                Description = "Cấm xe tải nặng lưu thông ban ngày vào nội đô"
            });
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "TP.HCM - Nội Đô",
                TruckType = "Container",
                BanStartTime = new TimeSpan(6, 0, 0),
                BanEndTime = new TimeSpan(22, 0, 0),
                Description = "Cấm xe Container lưu thông ban ngày vào nội đô"
            });

            // =================================================================================
            // 2. HÀ NỘI (Quyết định 06/2013/QĐ-UBND)
            // =================================================================================

            // --- Xe tải nhẹ (< 1.25 tấn) ---
            // Cấm cao điểm: Sáng 6h-9h, Chiều 16h30-19h30
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "Hà Nội - Vành Đai 3",
                TruckType = "LightTruck",
                BanStartTime = new TimeSpan(6, 0, 0),
                BanEndTime = new TimeSpan(9, 0, 0),
                Description = "Cấm xe tải nhẹ giờ cao điểm Sáng"
            });
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "Hà Nội - Vành Đai 3",
                TruckType = "LightTruck",
                BanStartTime = new TimeSpan(16, 30, 0),
                BanEndTime = new TimeSpan(19, 30, 0),
                Description = "Cấm xe tải nhẹ giờ cao điểm Chiều"
            });

            // --- Xe tải nặng (> 1.25 tấn) ---
            // Cấm 6h-21h trong khu vực hạn chế
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "Hà Nội - Nội Thành",
                TruckType = "HeavyTruck",
                BanStartTime = new TimeSpan(6, 0, 0),
                BanEndTime = new TimeSpan(21, 0, 0),
                Description = "Cấm xe tải nặng vào nội thành ban ngày"
            });

            // =================================================================================
            // 3. ĐÀ NẴNG (Các tuyến đường du lịch & trung tâm)
            // =================================================================================

            // Cấm tải chung giờ cao điểm: 6h30-8h30, 16h30-18h30
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "Đà Nẵng - Trung Tâm",
                TruckType = "Truck",
                BanStartTime = new TimeSpan(6, 30, 0),
                BanEndTime = new TimeSpan(8, 30, 0),
                Description = "Cấm các loại xe tải giờ cao điểm Sáng"
            });
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "Đà Nẵng - Trung Tâm",
                TruckType = "Truck",
                BanStartTime = new TimeSpan(16, 30, 0),
                BanEndTime = new TimeSpan(18, 30, 0),
                Description = "Cấm các loại xe tải giờ cao điểm Chiều"
            });

            // Khu vực Ngũ Hành Sơn - Cấm Container 24/24 một số tuyến
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "Đà Nẵng - Đường Biển",
                TruckType = "Container",
                BanStartTime = new TimeSpan(0, 0, 0),
                BanEndTime = new TimeSpan(23, 59, 0),
                Description = "Cấm tuyệt đối xe Container vào các tuyến đường du lịch biển"
            });

            // =================================================================================
            // 4. HẢI PHÒNG (Thành phố Cảng)
            // =================================================================================
            // Cấm xe Container vào trung tâm giờ hành chính
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "Hải Phòng - Nội Thành",
                TruckType = "Container",
                BanStartTime = new TimeSpan(5, 0, 0),
                BanEndTime = new TimeSpan(20, 0, 0),
                Description = "Cấm xe Container vào các tuyến phố trung tâm ban ngày"
            });

            // =================================================================================
            // 5. BÌNH DƯƠNG (Thủ phủ Công nghiệp)
            // =================================================================================
            // QL13, ĐT743... Cấm Container giờ cao điểm để tránh kẹt xe KCN
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "Bình Dương - KCN (QL13/ĐT743)",
                TruckType = "Container",
                BanStartTime = new TimeSpan(6, 0, 0),
                BanEndTime = new TimeSpan(8, 0, 0),
                Description = "Cấm Container/Xe tải nặng giờ cao điểm Sáng"
            });
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "Bình Dương - KCN (QL13/ĐT743)",
                TruckType = "Container",
                BanStartTime = new TimeSpan(16, 0, 0),
                BanEndTime = new TimeSpan(18, 0, 0),
                Description = "Cấm Container/Xe tải nặng giờ cao điểm Chiều"
            });

            // =================================================================================
            // 6. ĐỒNG NAI (Biên Hòa)
            // =================================================================================
            // Khu vực ngã tư Vũng Tàu, Amata
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "Đồng Nai - Biên Hòa",
                TruckType = "HeavyTruck",
                BanStartTime = new TimeSpan(6, 0, 0),
                BanEndTime = new TimeSpan(8, 30, 0),
                Description = "Cấm tải nặng lưu thông giờ cao điểm Sáng"
            });
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "Đồng Nai - Biên Hòa",
                TruckType = "HeavyTruck",
                BanStartTime = new TimeSpan(16, 0, 0),
                BanEndTime = new TimeSpan(18, 30, 0),
                Description = "Cấm tải nặng lưu thông giờ cao điểm Chiều"
            });

            // =================================================================================
            // 7. CẦN THƠ (Ninh Kiều)
            // =================================================================================
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "Cần Thơ - Ninh Kiều",
                TruckType = "Truck",
                BanStartTime = new TimeSpan(6, 0, 0),
                BanEndTime = new TimeSpan(8, 0, 0),
                Description = "Cấm tải trung tâm Ninh Kiều giờ Sáng"
            });
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "Cần Thơ - Ninh Kiều",
                TruckType = "Truck",
                BanStartTime = new TimeSpan(17, 0, 0),
                BanEndTime = new TimeSpan(19, 0, 0),
                Description = "Cấm tải trung tâm Ninh Kiều giờ Chiều"
            });

            // =================================================================================
            // 8. BÀ RỊA - VŨNG TÀU (QL51 & Nội đô Vũng Tàu)
            // =================================================================================
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "Vũng Tàu - Nội Đô",
                TruckType = "HeavyTruck",
                BanStartTime = new TimeSpan(6, 0, 0),
                BanEndTime = new TimeSpan(20, 0, 0),
                Description = "Cấm xe tải nặng vào trung tâm TP.Vũng Tàu ban ngày"
            });

            // =================================================================================
            // 9. KHÁNH HÒA (Nha Trang)
            // =================================================================================
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "Nha Trang - Đường Trần Phú",
                TruckType = "Truck",
                BanStartTime = new TimeSpan(0, 0, 0),
                BanEndTime = new TimeSpan(23, 59, 0),
                Description = "Cấm xe tải lưu thông đường Trần Phú (đường biển) 24/24"
            });
            restrictions.Add(new TruckRestriction
            {
                TruckRestrictionId = Guid.NewGuid(),
                ZoneName = "Nha Trang - Trung Tâm",
                TruckType = "HeavyTruck",
                BanStartTime = new TimeSpan(6, 0, 0),
                BanEndTime = new TimeSpan(22, 0, 0),
                Description = "Cấm xe tải trên 5 tấn vào trung tâm ban ngày"
            });

            modelBuilder.Entity<TruckRestriction>().HasData(restrictions);
        }
    }
}