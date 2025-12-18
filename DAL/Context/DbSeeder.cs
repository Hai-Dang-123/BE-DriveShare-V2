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

        // --- SPECIFIC USERS (Theo yêu cầu) ---
        private static readonly Guid Driver_DangLaiKP_ID = Guid.Parse("33333333-3333-3333-3333-333333333301");
        private static readonly Guid Driver_DangLVH_ID = Guid.Parse("33333333-3333-3333-3333-333333333302");
        private static readonly Guid Owner_VoLuong_ID = Guid.Parse("44444444-4444-4444-4444-444444444401");
        private static readonly Guid Provider_HoangPM_ID = Guid.Parse("55555555-5555-5555-5555-555555555501");

        // --- SAMPLE USERS (Mẫu) ---
        private static readonly Guid Driver_Sample1_ID = Guid.Parse("33333333-3333-3333-3333-333333333311"); // Link Owner Approved
        private static readonly Guid Driver_Sample2_ID = Guid.Parse("33333333-3333-3333-3333-333333333312"); // Link Owner Pending
        private static readonly Guid Owner_Sample1_ID = Guid.Parse("44444444-4444-4444-4444-444444444411");
        private static readonly Guid Owner_Sample2_ID = Guid.Parse("44444444-4444-4444-4444-444444444412");
        private static readonly Guid Provider_Sample1_ID = Guid.Parse("55555555-5555-5555-5555-555555555511");
        private static readonly Guid Provider_Sample2_ID = Guid.Parse("55555555-5555-5555-5555-555555555512");

        // ───────────────────────────────────────────────
        // 🔹 3. WALLET IDs (Tương ứng với User)
        // ───────────────────────────────────────────────
        // ... (Generated dựa trên logic UserID để code gọn hơn, xem hàm SeedWallets)

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
        private static readonly Guid VT_1T5 = Guid.Parse("A0000001-0000-0000-0000-000000000001");
        private static readonly Guid VT_2T5 = Guid.Parse("A0000002-0000-0000-0000-000000000001");
        private static readonly Guid VT_5T = Guid.Parse("A0000003-0000-0000-0000-000000000001");
        private static readonly Guid VT_8T = Guid.Parse("A0000004-0000-0000-0000-000000000001");
        private static readonly Guid VT_Container = Guid.Parse("A0000005-0000-0000-0000-000000000001");

        // ───────────────────────────────────────────────
        // 🔹 MAIN SEED METHOD
        // ───────────────────────────────────────────────
        public static void Seed(ModelBuilder modelBuilder)
        {
            SeedRole(modelBuilder);
            SeedUser(modelBuilder);
            SeedWallets(modelBuilder); // Setup tiền theo yêu cầu
            SeedDriver(modelBuilder);
            SeedOwner(modelBuilder);
            SeedProvider(modelBuilder);

            SeedContractTemplates(modelBuilder); // Nhiều term hơn
            SeedDeliveryRecordTemplates(modelBuilder);
            SeedVehicleType(modelBuilder); // Chỉ xe tải

            SeedUserDocuments(modelBuilder); // Full xác thực (CCCD, GPLX, GKSK)
            SeedOwnerDriverLinks(modelBuilder); // Link Driver mẫu với Owner VoLuong
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

            // ───────────────────────────────────────────────
            // 1. ADMIN & STAFF (Trụ sở chính)
            // ───────────────────────────────────────────────
            modelBuilder.Entity<BaseUser>().HasData(
                new BaseUser
                {
                    UserId = AdminID,
                    FullName = "System Admin",
                    Email = "admin@system.com",
                    RoleId = AdminRole,
                    PasswordHash = pass,
                    PhoneNumber = "0909000999",
                    Status = UserStatus.ACTIVE,
                    CreatedAt = now,
                    LastUpdatedAt = now,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    AvatarUrl = null,
                    // Địa chỉ: Khu Công Nghệ Cao, Q9
                    Address = new Location("Lô E2a-7, Đường D1, Khu Công Nghệ Cao, P.Long Thạnh Mỹ, TP.Thủ Đức, TP.HCM", 10.855323, 106.785243)
                },
                new BaseUser
                {
                    UserId = StaffID,
                    FullName = "Support Staff",
                    Email = "staff@system.com",
                    RoleId = StaffRole,
                    PasswordHash = pass,
                    PhoneNumber = "0909000888",
                    Status = UserStatus.ACTIVE,
                    CreatedAt = now,
                    LastUpdatedAt = now,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    AvatarUrl = null,
                    // Địa chỉ: Văn phòng đại diện Q1
                    Address = new Location("Tòa nhà Bitexco, số 2 Hải Triều, Bến Nghé, Quận 1, TP.HCM", 10.771587, 106.704257)
                }
            );

            // ───────────────────────────────────────────────
            // 2. DRIVERS (Tài xế)
            // ───────────────────────────────────────────────
            modelBuilder.Entity<BaseUser>().HasData(
                // CỤ THỂ 1: Đặng Lai KP
                new BaseUser
                {
                    UserId = Driver_DangLaiKP_ID,
                    FullName = "Đặng Lai KP",
                    Email = "danglaikp@gmail.com",
                    RoleId = DriverRole,
                    PasswordHash = pass,
                    PhoneNumber = "0988111222",
                    Status = UserStatus.ACTIVE,
                    CreatedAt = now,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    AvatarUrl = null,
                    // Địa chỉ: Khu vực bến xe Miền Tây (Bình Tân)
                    Address = new Location("395 Kinh Dương Vương, An Lạc, Bình Tân, TP.HCM", 10.742353, 106.615654)
                },
                // CỤ THỂ 2: Đặng LVH
                new BaseUser
                {
                    UserId = Driver_DangLVH_ID,
                    FullName = "Đặng LVH",
                    Email = "danglvhse151369@fpt.edu.vn",
                    RoleId = DriverRole,
                    PasswordHash = pass,
                    PhoneNumber = "0988111333",
                    Status = UserStatus.ACTIVE,
                    CreatedAt = now,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    AvatarUrl = null,
                    // Địa chỉ: Khu vực Ngã 4 Thủ Đức
                    Address = new Location("102 Lê Văn Việt, Hiệp Phú, TP.Thủ Đức, TP.HCM", 10.846512, 106.774921)
                },
                // MẪU 1
                new BaseUser
                {
                    UserId = Driver_Sample1_ID,
                    FullName = "Tài xế Mẫu 1",
                    Email = "driver1@gmail.com",
                    RoleId = DriverRole,
                    PasswordHash = pass,
                    PhoneNumber = "0988000001",
                    Status = UserStatus.ACTIVE,
                    CreatedAt = now,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    AvatarUrl = null,
                    // Địa chỉ: Bến xe An Sương
                    Address = new Location("QL22, Bà Điểm, Hóc Môn, TP.HCM", 10.843940, 106.612883)
                },
                // MẪU 2
                new BaseUser
                {
                    UserId = Driver_Sample2_ID,
                    FullName = "Tài xế Mẫu 2",
                    Email = "driver2@gmail.com",
                    RoleId = DriverRole,
                    PasswordHash = pass,
                    PhoneNumber = "0988000002",
                    Status = UserStatus.ACTIVE,
                    CreatedAt = now,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    AvatarUrl = null,
                    // Địa chỉ: Khu công nghiệp Tân Bình
                    Address = new Location("Lô II, Đường số 2, KCN Tân Bình, Tây Thạnh, Tân Phú, TP.HCM", 10.806623, 106.626354)
                }
            );

            // ───────────────────────────────────────────────
            // 3. OWNERS (Chủ xe)
            // ───────────────────────────────────────────────
            modelBuilder.Entity<BaseUser>().HasData(
                // CỤ THỂ: Võ Lương Nhựt Tiến
                new BaseUser
                {
                    UserId = Owner_VoLuong_ID,
                    FullName = "Võ Lương Nhựt Tiến",
                    Email = "voluongnhuttien@gmail.com",
                    RoleId = OwnerRole,
                    PasswordHash = pass,
                    PhoneNumber = "0911222333",
                    Status = UserStatus.ACTIVE,
                    CreatedAt = now,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    AvatarUrl = null,
                    // Địa chỉ: Gần Cảng Cát Lái (Khu vực nhiều chủ xe)
                    Address = new Location("1295 Nguyễn Thị Định, Cát Lái, TP.Thủ Đức, TP.HCM", 10.767895, 106.775874)
                },
                // MẪU 1
                new BaseUser
                {
                    UserId = Owner_Sample1_ID,
                    FullName = "Chủ xe Mẫu 1",
                    Email = "owner1@gmail.com",
                    RoleId = OwnerRole,
                    PasswordHash = pass,
                    PhoneNumber = "0911000001",
                    Status = UserStatus.ACTIVE,
                    CreatedAt = now,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    AvatarUrl = null,
                    // Địa chỉ: Khu vực Quận 7
                    Address = new Location("456 Nguyễn Văn Linh, Tân Phú, Quận 7, TP.HCM", 10.729355, 106.721833)
                },
                // MẪU 2
                new BaseUser
                {
                    UserId = Owner_Sample2_ID,
                    FullName = "Chủ xe Mẫu 2",
                    Email = "owner2@gmail.com",
                    RoleId = OwnerRole,
                    PasswordHash = pass,
                    PhoneNumber = "0911000002",
                    Status = UserStatus.ACTIVE,
                    CreatedAt = now,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    AvatarUrl = null,
                    // Địa chỉ: Bình Dương
                    Address = new Location("Đại lộ Bình Dương, Phú Thọ, Thủ Dầu Một, Bình Dương", 10.980645, 106.674391)
                }
            );

            // ───────────────────────────────────────────────
            // 4. PROVIDERS (Chủ hàng / Nhà cung cấp)
            // ───────────────────────────────────────────────
            modelBuilder.Entity<BaseUser>().HasData(
                // CỤ THỂ: Hoàng PM
                new BaseUser
                {
                    UserId = Provider_HoangPM_ID,
                    FullName = "Hoàng PM",
                    Email = "hoangpmse171847@fpt.edu.vn",
                    RoleId = ProviderRole,
                    PasswordHash = pass,
                    PhoneNumber = "0933444555",
                    Status = UserStatus.ACTIVE,
                    CreatedAt = now,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    AvatarUrl = null,
                    // Địa chỉ: Chợ Đầu Mối Nông Sản Thủ Đức (Phù hợp với Provider)
                    Address = new Location("QL1A, Tam Bình, TP.Thủ Đức, TP.HCM", 10.868743, 106.745421)
                },
                // MẪU 1
                new BaseUser
                {
                    UserId = Provider_Sample1_ID,
                    FullName = "Nhà cung cấp Mẫu 1",
                    Email = "provider1@gmail.com",
                    RoleId = ProviderRole,
                    PasswordHash = pass,
                    PhoneNumber = "0933000001",
                    Status = UserStatus.ACTIVE,
                    CreatedAt = now,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    AvatarUrl = null,
                    // Địa chỉ: KCN Sóng Thần
                    Address = new Location("Đại lộ Độc Lập, An Bình, Dĩ An, Bình Dương", 10.902621, 106.747388)
                },
                // MẪU 2
                new BaseUser
                {
                    UserId = Provider_Sample2_ID,
                    FullName = "Nhà cung cấp Mẫu 2",
                    Email = "provider2@gmail.com",
                    RoleId = ProviderRole,
                    PasswordHash = pass,
                    PhoneNumber = "0933000002",
                    Status = UserStatus.ACTIVE,
                    CreatedAt = now,
                    IsEmailVerified = true,
                    IsPhoneVerified = true,
                    AvatarUrl = null,
                    // Địa chỉ: Chợ Bình Điền
                    Address = new Location("Đại lộ Nguyễn Văn Linh, Khu Phố 6, Quận 8, TP.HCM", 10.716321, 106.626743)
                }
            );
        }

        private static void SeedWallets(ModelBuilder modelBuilder)
        {
            var now = DateTime.UtcNow;
            decimal defaultBalance = 500_000_000m; // 500 triệu
            decimal providerSpecialBalance = 14_900_000m; // 14 triệu 900

            var wallets = new List<Wallet>();

            // Helper function
            void AddWallet(Guid userId, decimal balance)
            {
                wallets.Add(new Wallet
                {
                    WalletId = Guid.NewGuid(),
                    UserId = userId,
                    Balance = balance,
                    Currency = "VND",
                    Status = WalletStatus.ACTIVE,
                    CreatedAt = now,
                    LastUpdatedAt = now
                });
            }

            // Add Wallets
            AddWallet(AdminID, defaultBalance);
            AddWallet(StaffID, defaultBalance);

            // Drivers
            AddWallet(Driver_DangLaiKP_ID, defaultBalance);
            AddWallet(Driver_DangLVH_ID, defaultBalance);
            AddWallet(Driver_Sample1_ID, defaultBalance);
            AddWallet(Driver_Sample2_ID, defaultBalance);

            // Owners
            AddWallet(Owner_VoLuong_ID, defaultBalance);
            AddWallet(Owner_Sample1_ID, defaultBalance);
            AddWallet(Owner_Sample2_ID, defaultBalance);

            // Providers
            AddWallet(Provider_HoangPM_ID, providerSpecialBalance); // <-- 14.900.000
            AddWallet(Provider_Sample1_ID, defaultBalance);
            AddWallet(Provider_Sample2_ID, defaultBalance);

            modelBuilder.Entity<Wallet>().HasData(wallets);
        }

        private static void SeedDriver(ModelBuilder modelBuilder)
        {
            var drivers = new List<Driver>
            {
                CreateDriverEntity(Driver_DangLaiKP_ID, "GPLX_DLKP", "C"),
                CreateDriverEntity(Driver_DangLVH_ID, "GPLX_DLVH", "C"),
                CreateDriverEntity(Driver_Sample1_ID, "GPLX_S1", "C"),
                CreateDriverEntity(Driver_Sample2_ID, "GPLX_S2", "B2")
            };
            modelBuilder.Entity<Driver>().HasData(drivers);
        }

        private static Driver CreateDriverEntity(Guid userId, string license, string cls)
        {
            return new Driver
            {
                UserId = userId,
                DateOfBirth = new DateTime(1995, 1, 1),
                LicenseNumber = license,
                LicenseClass = cls,
                LicenseExpiryDate = DateTime.UtcNow.AddYears(5),
                IsLicenseVerified = true, // Đã xác thực
                DriverStatus = DriverStatus.AVAILABLE,
                HasDeclaredInitialHistory = true
            };
        }

        private static void SeedOwner(ModelBuilder modelBuilder)
        {
            var owners = new List<Owner>
            {
                CreateOwnerEntity(Owner_VoLuong_ID, "Công ty Vận tải Võ Lương", "MST001"),
                CreateOwnerEntity(Owner_Sample1_ID, "Vận tải Mẫu 1", "MST002"),
                CreateOwnerEntity(Owner_Sample2_ID, "Vận tải Mẫu 2", "MST003")
            };
            modelBuilder.Entity<Owner>().HasData(owners);
        }

        private static Owner CreateOwnerEntity(Guid userId, string company, string tax)
        {
            return new Owner
            {
                UserId = userId,
                DateOfBirth = new DateTime(1985, 1, 1),
                CompanyName = company,
                TaxCode = tax,
                AverageRating = 5.0m
            };
        }

        private static void SeedProvider(ModelBuilder modelBuilder)
        {
            var providers = new List<Provider>
            {
                CreateProviderEntity(Provider_HoangPM_ID, "Nông sản Hoàng PM", "MST_P001"),
                CreateProviderEntity(Provider_Sample1_ID, "Kho hàng Mẫu 1", "MST_P002"),
                CreateProviderEntity(Provider_Sample2_ID, "Kho hàng Mẫu 2", "MST_P003")
            };
            modelBuilder.Entity<Provider>().HasData(providers);
        }

        private static Provider CreateProviderEntity(Guid userId, string company, string tax)
        {
            return new Provider
            {
                UserId = userId,
                DateOfBirth = new DateTime(1990, 5, 5),
                CompanyName = company,
                TaxCode = tax,
                AverageRating = 4.9m
            };
        }

        private static void SeedContractTemplates(ModelBuilder modelBuilder)
        {
            var now = DateTime.UtcNow;

            // 1. PROVIDER CONTRACT (Owner - Provider)
            modelBuilder.Entity<ContractTemplate>().HasData(
                new ContractTemplate { ContractTemplateId = ProviderContractTemplateID, ContractTemplateName = "Hợp đồng Vận chuyển Hàng hóa", Version = "2.0 VN", Type = ContractType.PROVIDER_CONTRACT, CreatedAt = now }
            );

            modelBuilder.Entity<ContractTerm>().HasData(
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = ProviderContractTemplateID, Order = 1, Content = "Điều 1: Bên A (Chủ xe) cam kết cung cấp phương tiện vận tải đúng chủng loại, tải trọng và thời gian theo yêu cầu của Bên B (Chủ hàng)." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = ProviderContractTemplateID, Order = 2, Content = "Điều 2: Bên B có trách nhiệm đóng gói hàng hóa đúng quy cách, đảm bảo an toàn trong quá trình vận chuyển và bốc dỡ." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = ProviderContractTemplateID, Order = 3, Content = "Điều 3: Cước phí vận chuyển được tính dựa trên báo giá trên hệ thống tại thời điểm chốt đơn. Bên B thanh toán 100% qua ví điện tử sau khi hoàn tất giao hàng." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = ProviderContractTemplateID, Order = 4, Content = "Điều 4: Trong trường hợp bất khả kháng (thiên tai, dịch bệnh, cấm đường), hai bên sẽ thương lượng để gia hạn thời gian hoặc hủy bỏ chuyến hàng mà không phạt vi phạm." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = ProviderContractTemplateID, Order = 5, Content = "Điều 5: Bên A chịu trách nhiệm bồi thường 100% giá trị hàng hóa nếu xảy ra mất mát, hư hỏng do lỗi chủ quan của tài xế." }
            );

            // 2. DRIVER CONTRACT (Owner - Driver)
            modelBuilder.Entity<ContractTemplate>().HasData(
                new ContractTemplate { ContractTemplateId = DriverContractTemplateID, ContractTemplateName = "Hợp đồng Hợp tác Lái xe", Version = "2.0 VN", Type = ContractType.DRIVER_CONTRACT, CreatedAt = now }
            );

            modelBuilder.Entity<ContractTerm>().HasData(
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = DriverContractTemplateID, Order = 1, Content = "Điều 1: Bên A (Chủ xe) đồng ý giao phương tiện cho Bên B (Tài xế) để khai thác dịch vụ vận tải." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = DriverContractTemplateID, Order = 2, Content = "Điều 2: Bên B cam kết có đầy đủ giấy phép lái xe hợp lệ, đủ sức khỏe và không sử dụng chất kích thích trong quá trình làm việc." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = DriverContractTemplateID, Order = 3, Content = "Điều 3: Bên B có trách nhiệm bảo quản, giữ gìn vệ sinh và bảo dưỡng xe định kỳ theo quy định của Bên A." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = DriverContractTemplateID, Order = 4, Content = "Điều 4: Thu nhập của Bên B được chia sẻ theo tỷ lệ phần trăm doanh thu chuyến xe hoặc lương cố định tùy theo thỏa thuận cụ thể trên ứng dụng." },
                new ContractTerm { ContractTermId = Guid.NewGuid(), ContractTemplateId = DriverContractTemplateID, Order = 5, Content = "Điều 5: Mọi hành vi gian lận, trộm cắp nhiên liệu hoặc hàng hóa sẽ bị chấm dứt hợp đồng ngay lập tức và truy cứu trách nhiệm hình sự." }
            );
        }

        private static void SeedDeliveryRecordTemplates(ModelBuilder modelBuilder)
        {
            var now = DateTime.UtcNow;

            // --- HÀNG HÓA ---
            modelBuilder.Entity<DeliveryRecordTemplate>().HasData(
                new DeliveryRecordTemplate { DeliveryRecordTemplateId = PickupRecordTemplateID, TemplateName = "Biên bản Giao nhận Hàng (Pickup)", Version = "1.0", Type = DeliveryRecordType.PICKUP, Status = DeliveryRecordTemplateStatus.ACTIVE, CreatedAt = now },
                new DeliveryRecordTemplate { DeliveryRecordTemplateId = DropoffRecordTemplateID, TemplateName = "Biên bản Bàn giao Hàng (Dropoff)", Version = "1.0", Type = DeliveryRecordType.DROPOFF, Status = DeliveryRecordTemplateStatus.ACTIVE, CreatedAt = now }
            );

            // Terms Hàng hóa (Giữ nguyên hoặc thêm nếm tùy ý, ở đây giữ cơ bản cho gọn)
            modelBuilder.Entity<DeliveryRecordTerm>().HasData(
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = PickupRecordTemplateID, DisplayOrder = 1, Content = "Đã kiểm tra số lượng và ngoại quan hàng hóa." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = DropoffRecordTemplateID, DisplayOrder = 1, Content = "Đã nhận đủ hàng, bao bì nguyên vẹn." }
            );

            // --- XE (HANDOVER) ---
            modelBuilder.Entity<DeliveryRecordTemplate>().HasData(
                new DeliveryRecordTemplate { DeliveryRecordTemplateId = VehiclePickupTemplateID, TemplateName = "Biên bản Giao Xe (Cho tài xế)", Version = "1.0", Type = DeliveryRecordType.HANDOVER, Status = DeliveryRecordTemplateStatus.ACTIVE, CreatedAt = now },
                new DeliveryRecordTemplate { DeliveryRecordTemplateId = VehicleDropoffTemplateID, TemplateName = "Biên bản Thu Hồi Xe", Version = "1.0", Type = DeliveryRecordType.RETURN, Status = DeliveryRecordTemplateStatus.ACTIVE, CreatedAt = now }
            );

            // Terms Xe
            modelBuilder.Entity<DeliveryRecordTerm>().HasData(
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 1, Content = "Giấy tờ xe (Đăng ký, Bảo hiểm) đầy đủ." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehiclePickupTemplateID, DisplayOrder = 2, Content = "Tình trạng lốp, đèn, còi hoạt động tốt." },
                new DeliveryRecordTerm { DeliveryRecordTermId = Guid.NewGuid(), DeliveryRecordTemplateId = VehicleDropoffTemplateID, DisplayOrder = 1, Content = "Xe sạch sẽ, không phát sinh va quẹt mới." }
            );
        }

        private static void SeedVehicleType(ModelBuilder modelBuilder)
        {
            // Chỉ tập trung vào xe tải
            modelBuilder.Entity<VehicleType>().HasData(
                new VehicleType { VehicleTypeId = VT_1T5, VehicleTypeName = "Xe tải 1.5 Tấn", Description = "Xe tải nhẹ chạy nội thành, kích thước nhỏ gọn." },
                new VehicleType { VehicleTypeId = VT_2T5, VehicleTypeName = "Xe tải 2.5 Tấn", Description = "Xe tải nhẹ, phù hợp chuyển nhà hoặc hàng tiêu dùng." },
                new VehicleType { VehicleTypeId = VT_5T, VehicleTypeName = "Xe tải 5 Tấn", Description = "Xe tải trung, thùng dài 6m." },
                new VehicleType { VehicleTypeId = VT_8T, VehicleTypeName = "Xe tải 8 Tấn", Description = "Xe tải hạng nặng 2 chân, chuyên chạy liên tỉnh." },
                new VehicleType
                {
                    VehicleTypeId = VT_Container, // (Giữ nguyên ID cũ hoặc đổi tên biến tùy bạn)
                    VehicleTypeName = "Xe đông lạnh", // Tên có chữ "lạnh" -> Hợp lệ với logic check
                    Description = "Xe tải chuyên dụng có hệ thống làm lạnh bảo quản thực phẩm tươi sống."
                }
            );
        }

        // ───────────────────────────────────────────────
        // 🔹 SEED DOCUMENTS (FULL CCCD + DRIVER DOCS)
        // ───────────────────────────────────────────────
        private static void SeedUserDocuments(ModelBuilder modelBuilder)
        {
            var docs = new List<UserDocument>();
            var now = DateTime.UtcNow;

            // List tất cả User cần tạo CCCD
            var allUsers = new List<(Guid Id, string Name)>
            {
                (Driver_DangLaiKP_ID, "DANG LAI KP"), (Driver_DangLVH_ID, "DANG LVH"),
                (Driver_Sample1_ID, "TAI XE MAU 1"), (Driver_Sample2_ID, "TAI XE MAU 2"),
                (Owner_VoLuong_ID, "VO LUONG NHUT TIEN"), (Owner_Sample1_ID, "CHU XE MAU 1"), (Owner_Sample2_ID, "CHU XE MAU 2"),
                (Provider_HoangPM_ID, "HOANG PM"), (Provider_Sample1_ID, "PROVIDER MAU 1"), (Provider_Sample2_ID, "PROVIDER MAU 2")
            };

            // 1. Tạo CCCD cho TẤT CẢ mọi người
            foreach (var user in allUsers)
            {
                docs.Add(new UserDocument
                {
                    UserDocumentId = Guid.NewGuid(),
                    UserId = user.Id,
                    DocumentType = DocumentType.CCCD,
                    Status = VerifileStatus.ACTIVE, // Đã xác thực
                    IdentityNumber = "07909" + new Random().Next(1000000, 9999999),
                    FullName = user.Name,
                    DateOfBirth = new DateTime(1990, 1, 1),
                    IssueDate = now.AddYears(-2),
                    ExpiryDate = now.AddYears(10),
                    FrontImageUrl = "https://img.vn/cccd_front.jpg",
                    BackImageUrl = "https://img.vn/cccd_back.jpg",
                    IsDocumentReal = true,
                    FaceMatchScore = 99.9,
                    VerifiedAt = now
                });
            }

            // 2. Tạo GPLX & GKSK cho TẤT CẢ DRIVER
            var drivers = new List<Guid> { Driver_DangLaiKP_ID, Driver_DangLVH_ID, Driver_Sample1_ID, Driver_Sample2_ID };

            foreach (var drvId in drivers)
            {
                // GPLX (Driver License)
                docs.Add(new UserDocument
                {
                    UserDocumentId = Guid.NewGuid(),
                    UserId = drvId,
                    DocumentType = DocumentType.DRIVER_LINCENSE,
                    Status = VerifileStatus.ACTIVE,
                    IdentityNumber = "GPLX" + new Random().Next(100000, 999999),
                    IssueDate = now.AddYears(-1),
                    ExpiryDate = now.AddYears(4),
                    FrontImageUrl = "https://img.vn/gplx_front.jpg",
                    BackImageUrl = "https://img.vn/gplx_back.jpg",
                    IsDocumentReal = true,
                    VerifiedAt = now
                });

                // GKSK (Health Check) - Giả sử Enum có HEALTH_CHECK, nếu chưa có bạn cần thêm vào Enum
                docs.Add(new UserDocument
                {
                    UserDocumentId = Guid.NewGuid(),
                    UserId = drvId,
                    DocumentType = DocumentType.HEALTH_CHECK, // Cần đảm bảo Enum có cái này
                    Status = VerifileStatus.ACTIVE,
                    IssueDate = now.AddMonths(-1), // Mới khám
                    ExpiryDate = now.AddMonths(5), // Hết hạn sau 6 tháng
                    FrontImageUrl = "https://img.vn/health_check.jpg",
                    IsDocumentReal = true,
                    VerifiedAt = now
                });
            }

            modelBuilder.Entity<UserDocument>().HasData(docs);
        }

        // ───────────────────────────────────────────────
        // 🔹 SEED OWNER-DRIVER LINKS
        // ───────────────────────────────────────────────
        private static void SeedOwnerDriverLinks(ModelBuilder modelBuilder)
        {
            var now = DateTime.UtcNow;

            modelBuilder.Entity<OwnerDriverLink>().HasData(
                // 1. Driver Mẫu 1 -> Owner VoLuong: APPROVED (Được duyệt)
                new OwnerDriverLink
                {
                    OwnerDriverLinkId = Guid.NewGuid(),
                    OwnerId = Owner_VoLuong_ID,
                    DriverId = Driver_Sample1_ID,
                    Status = FleetJoinStatus.APPROVED,
                    RequestedAt = now.AddDays(-10),
                    ApprovedAt = now.AddDays(-9)
                },

                // 2. Driver Mẫu 2 -> Owner VoLuong: PENDING (Chưa duyệt)
                new OwnerDriverLink
                {
                    OwnerDriverLinkId = Guid.NewGuid(),
                    OwnerId = Owner_VoLuong_ID,
                    DriverId = Driver_Sample2_ID,
                    Status = FleetJoinStatus.PENDING,
                    RequestedAt = now.AddHours(-2),
                    ApprovedAt = null
                }
            );
        }
    }
}