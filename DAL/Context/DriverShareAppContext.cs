using Common.ValueObjects;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Linq;
using System.Text.Json;

namespace DAL.Context
{
    public class DriverShareAppContext : DbContext
    {
        public DriverShareAppContext(DbContextOptions<DriverShareAppContext> options) : base(options) { }

        // --- Bảng Người dùng & Vai trò ---
        public DbSet<BaseUser> BaseUsers { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Driver> Drivers { get; set; } = null!;
        public DbSet<Owner> Owners { get; set; } = null!;
        public DbSet<Provider> Providers { get; set; } = null!;
        public DbSet<PackageHandlingDetail> PackageHandlingDetails { get; set; }

        // --- Bảng Liên quan đến Người dùng ---
        public DbSet<UserDocument> UserDocuments { get; set; } = null!;
        public DbSet<UserToken> UserTokens { get; set; } = null!;
        public DbSet<UserViolation> UserViolations { get; set; } = null!;
        public DbSet<DriverActivityLog> DriverActivityLogs { get; set; } = null!;
        public DbSet<OwnerDriverLink> OwnerDriverLinks { get; set; } = null!;

        // --- Bảng Tài chính ---
        public DbSet<Wallet> Wallets { get; set; } = null!;
        public DbSet<Transaction> Transactions { get; set; } = null!;

        // --- Bảng Xe cộ (Vehicle) ---
        public DbSet<Vehicle> Vehicles { get; set; } = null!;
        public DbSet<VehicleType> VehicleTypes { get; set; } = null!;
        public DbSet<VehicleDocument> VehicleDocuments { get; set; } = null!;
        public DbSet<VehicleImage> VehicleImages { get; set; } = null!;

        // --- Bảng Hàng hóa & Đóng gói (Item/Package) ---
        public DbSet<Item> Items { get; set; } = null!;
        public DbSet<ItemImage> ItemImages { get; set; } = null!;
        public DbSet<Package> Packages { get; set; } = null!;
        public DbSet<PackageImage> PackageImages { get; set; } = null!;

        // --- Bảng Đăng tin (Post) ---
        public DbSet<PostPackage> PostPackages { get; set; } = null!;
        public DbSet<PostTrip> PostTrips { get; set; } = null!;

        // --- Bảng Chuyến đi & Lộ trình (Trip/Route) ---
        public DbSet<Trip> Trips { get; set; } = null!;
        public DbSet<ShippingRoute> ShippingRoutes { get; set; } = null!;
        public DbSet<TripRoute> TripRoutes { get; set; } = null!;
        public DbSet<TripRouteSuggestion> TripRouteSuggestions { get; set; } = null!;
        public DbSet<TripContact> TripContacts { get; set; } = null!;
        public DbSet<TripDriverAssignment> TripDriverAssignments { get; set; } = null!;

        // --- Bảng Hợp đồng (Contract) ---
        public DbSet<BaseContract> BaseContracts { get; set; } = null!;
        public DbSet<TripDriverContract> TripDriverContracts { get; set; } = null!;
        public DbSet<TripProviderContract> TripProviderContracts { get; set; } = null!;
        public DbSet<ContractTemplate> ContractTemplates { get; set; } = null!;
        public DbSet<ContractTerm> ContractTerms { get; set; } = null!;

        // --- Bảng Biên bản & Vấn đề (Delivery Record/Issue) ---
        public DbSet<DeliveryRecord> DeliveryRecords { get; set; } = null!;
        public DbSet<TripDeliveryRecord> TripDeliveryRecords { get; set; } = null!;
        public DbSet<DeliveryRecordTemplate> DeliveryRecordTemplates { get; set; } = null!;
        public DbSet<DeliveryRecordTerm> DeliveryRecordTerms { get; set; } = null!;
        public DbSet<TripDeliveryIssue> TripDeliveryIssues { get; set; } = null!;

        // === SỬA LỖI 1 ===
        public DbSet<TripDeliveryIssueImage> DeliveryIssueImages { get; set; } = null!; // Đổi tên class

        public DbSet<PostContact> PostContacts { get; set; } = null!;

        public DbSet<PostTripDetail> PostTripDetails { get; set; } = null!;
        public DbSet<ContactToken> ContactTokens { get; set; } = null!;

        public DbSet<TripVehicleHandoverRecord> TripVehicleHandoverRecords { get; set; }
        public DbSet<TripVehicleHandoverTermResult> TripVehicleHandoverTermResults { get; set; }
        public DbSet<TripVehicleHandoverIssue> TripVehicleHandoverIssues { get; set; }
        public DbSet<TripVehicleHandoverIssueImage> TripVehicleHandoverIssueImages { get; set; }
        public DbSet<DriverWorkSession> DriverWorkSessions { get; set; } = null!;
        public DbSet<InspectionHistory> InspectionHistories { get; set; } = null!;

        public DbSet<TruckRestriction> TruckRestrictions { get; set; }

        public DbSet<UserDeviceToken> UserDeviceTokens { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Tách ra 4 hàm cấu hình riêng biệt cho sạch sẽ
            ConfigurePrimaryKeys(modelBuilder);
            ConfigureInheritance(modelBuilder);
            ConfigureValueObjectsAndJson(modelBuilder);
            ConfigureRelationships(modelBuilder);
            ConfigureDecimalPrecision(modelBuilder);

            DbSeeder.Seed(modelBuilder);
        }

        // =================================================================
        // HÀM 1: CẤU HÌNH KHÓA CHÍNH (PRIMARY KEY)
        // =================================================================
        private void ConfigurePrimaryKeys(ModelBuilder modelBuilder)
        {
            // --- Các Bảng Kế thừa (Base Entities) ---
            modelBuilder.Entity<BaseUser>().HasKey(e => e.UserId);
            modelBuilder.Entity<BaseContract>().HasKey(e => e.ContractId);
            modelBuilder.Entity<DeliveryRecord>().HasKey(e => e.DeliveryRecordId);

            // --- Các Bảng Người dùng & Vai trò ---
            modelBuilder.Entity<Role>().HasKey(e => e.RoleId);
            modelBuilder.Entity<UserDocument>().HasKey(e => e.UserDocumentId);
            modelBuilder.Entity<UserToken>().HasKey(e => e.UserTokenId);
            modelBuilder.Entity<ContactToken>().HasKey(e => e.ContactTokenId);
            modelBuilder.Entity<UserViolation>().HasKey(e => e.UserViolationId);
            modelBuilder.Entity<DriverActivityLog>().HasKey(e => e.DriverActivityLogId);
            modelBuilder.Entity<OwnerDriverLink>().HasKey(e => e.OwnerDriverLinkId);

            // --- Các Bảng Tài chính ---
            modelBuilder.Entity<Wallet>().HasKey(e => e.WalletId);
            modelBuilder.Entity<Transaction>().HasKey(e => e.TransactionId);

            // --- Các Bảng Xe cộ ---
            modelBuilder.Entity<Vehicle>().HasKey(e => e.VehicleId);
            modelBuilder.Entity<VehicleType>().HasKey(e => e.VehicleTypeId);
            modelBuilder.Entity<VehicleDocument>().HasKey(e => e.VehicleDocumentId);
            modelBuilder.Entity<VehicleImage>().HasKey(e => e.VehicleImageId);

            // --- Các Bảng Hàng hóa & Đóng gói ---
            modelBuilder.Entity<Item>().HasKey(e => e.ItemId);
            modelBuilder.Entity<ItemImage>().HasKey(e => e.ItemImageId);
            modelBuilder.Entity<Package>().HasKey(e => e.PackageId);
            modelBuilder.Entity<PackageImage>().HasKey(e => e.PackageImageId);

            // --- Các Bảng Đăng tin ---
            modelBuilder.Entity<PostPackage>().HasKey(e => e.PostPackageId);
            modelBuilder.Entity<PostTrip>().HasKey(e => e.PostTripId);

            // --- Các Bảng Chuyến đi & Lộ trình ---
            modelBuilder.Entity<Trip>().HasKey(e => e.TripId);
            modelBuilder.Entity<ShippingRoute>().HasKey(e => e.ShippingRouteId);
            modelBuilder.Entity<TripRoute>().HasKey(e => e.TripRouteId);
            modelBuilder.Entity<TripRouteSuggestion>().HasKey(e => e.TripRouteSuggestionId);
            modelBuilder.Entity<TripContact>().HasKey(e => e.TripContactId);
            modelBuilder.Entity<TripDriverAssignment>().HasKey(e => e.TripDriverAssignmentId);
           

            // --- Các Bảng Hợp đồng (Templates) ---
            modelBuilder.Entity<ContractTemplate>().HasKey(e => e.ContractTemplateId);
            modelBuilder.Entity<ContractTerm>().HasKey(e => e.ContractTermId);

            // --- Các Bảng Biên bản & Vấn đề (Templates) ---
            modelBuilder.Entity<DeliveryRecordTemplate>().HasKey(e => e.DeliveryRecordTemplateId);
            modelBuilder.Entity<DeliveryRecordTerm>().HasKey(e => e.DeliveryRecordTermId);
            modelBuilder.Entity<TripDeliveryIssue>().HasKey(e => e.TripDeliveryIssueId);
            modelBuilder.Entity<TripDeliveryIssueImage>().HasKey(e => e.TripDeliveryIssueImageId);

            modelBuilder.Entity<PostContact>().HasKey(p => p.PostContactId);
            modelBuilder.Entity<PostTripDetail>().HasKey(ptd => ptd.PostTripDetailId);

            //modelBuilder.Entity<TripVehicleHandoverRecord>().HasKey(tvr => tvr.DeliveryRecordId);
            modelBuilder.Entity<TripVehicleHandoverTermResult>().HasKey(tvtr => tvtr.TripVehicleHandoverTermResultId);
            modelBuilder.Entity<TripVehicleHandoverIssue>().HasKey(tvi => tvi.TripVehicleHandoverIssueId);
            modelBuilder.Entity<TripVehicleHandoverIssueImage>().HasKey(tvii => tvii.TripVehicleHandoverIssueImageId);
            modelBuilder.Entity<DriverWorkSession>().HasKey(dws => dws.DriverWorkSessionId);
            modelBuilder.Entity<InspectionHistory>().HasKey(ih => ih.InspectionHistoryId);
            modelBuilder.Entity<TruckRestriction>().HasKey(tr => tr.TruckRestrictionId);
            modelBuilder.Entity<UserDeviceToken>().HasKey(udt => udt.UserDeviceTokenId);

        }

        // =================================================================
        // HÀM 2: CẤU HÌNH KẾ THỪA (TPT)
        // =================================================================
        private void ConfigureInheritance(ModelBuilder modelBuilder)
        {
            // === SỬA LỖI 2: Cấu hình BaseUser TPT ===
            // 1. BaseUser (TPT)
            modelBuilder.Entity<BaseUser>().ToTable("BaseUsers"); // Bảng cha
            modelBuilder.Entity<Driver>().ToTable("Drivers");
            modelBuilder.Entity<Owner>().ToTable("Owners");
            modelBuilder.Entity<Provider>().ToTable("Providers");
            // (Không cấu hình Admin/Staff ở đây, đó là Role)

            // 2. BaseContract (TPT)
            modelBuilder.Entity<BaseContract>().ToTable("BaseContracts");
            modelBuilder.Entity<TripDriverContract>().ToTable("TripDriverContracts");
            modelBuilder.Entity<TripProviderContract>().ToTable("TripProviderContracts");

            // 3. DeliveryRecord (TPT)
            modelBuilder.Entity<DeliveryRecord>().ToTable("DeliveryRecords");
            modelBuilder.Entity<TripDeliveryRecord>().ToTable("TripDeliveryRecords");
            modelBuilder.Entity<TripVehicleHandoverRecord>().ToTable("TripVehicleHandoverRecords");
        }

        // =================================================================
        // HÀM 3: CẤU HÌNH VALUE OBJECTS (LOCATION, TIMEWINDOW) & JSON
        // =================================================================
        private void ConfigureValueObjectsAndJson(ModelBuilder modelBuilder)
        {
            // --- CẤU HÌNH VALUE OBJECTS (Owned Types) ---

            // Cấu hình quan hệ 1-1 sử dụng Shared Primary Key
            modelBuilder.Entity<Package>()
                .HasOne(p => p.HandlingDetail)      // Package có 1 HandlingDetail
                .WithOne(d => d.Package)            // HandlingDetail thuộc về 1 Package
                .HasForeignKey<PackageHandlingDetail>(d => d.PackageId); // Khóa ngoại nằm ở bảng con và chính là PK

            // 1. Cấu hình cho BaseUser.Address
            modelBuilder.Entity<BaseUser>().OwnsOne(u => u.Address, loc =>
            {
                loc.Property(p => p.Address).HasMaxLength(500).IsRequired(false);
                loc.Property(p => p.Latitude).IsRequired(false);
                loc.Property(p => p.Longitude).IsRequired(false);
            });
            modelBuilder.Entity<BaseUser>()
                .Navigation(u => u.Address)
                .IsRequired(false); // 🔥 Thêm dòng này

            // 2. Cấu hình cho Owner.BusinessAddress
            modelBuilder.Entity<Owner>().OwnsOne(o => o.BusinessAddress, loc =>
            {
                loc.Property(p => p.Address).HasMaxLength(500).IsRequired(false);
                loc.Property(p => p.Latitude).IsRequired(false);
                loc.Property(p => p.Longitude).IsRequired(false);
            });
            modelBuilder.Entity<Owner>()
                .Navigation(o => o.BusinessAddress)
                .IsRequired(false); // 🔥 Thêm dòng này

            // 3. Cấu hình cho Provider.BusinessAddress
            modelBuilder.Entity<Provider>().OwnsOne(p => p.BusinessAddress, loc =>
            {
                loc.Property(l => l.Address).HasMaxLength(500).IsRequired(false);
                loc.Property(l => l.Latitude).IsRequired(false);
                loc.Property(l => l.Longitude).IsRequired(false);
            });
            modelBuilder.Entity<Provider>()
                .Navigation(p => p.BusinessAddress)
                .IsRequired(false); // 🔥 Thêm dòng này

            // 4. Cấu hình cho Vehicle.CurrentAddress
            // === SỬA LỖI 3: Xóa 1 khối lặp ===
            modelBuilder.Entity<Vehicle>().OwnsOne(v => v.CurrentAddress, loc =>
            {
                loc.Property(l => l.Address).HasMaxLength(500).IsRequired(false);
                loc.Property(l => l.Latitude).IsRequired(false);
                loc.Property(l => l.Longitude).IsRequired(false);
            });

            modelBuilder.Entity<Vehicle>()
    .Navigation(v => v.CurrentAddress)
    .IsRequired(false);



            // 5. Cấu hình cho ShippingRoute.StartLocation
            modelBuilder.Entity<ShippingRoute>().OwnsOne(sr => sr.StartLocation, loc =>
            {
                loc.Property(p => p.Address).HasMaxLength(500).IsRequired(false);
                loc.Property(p => p.Latitude).IsRequired(false);
                loc.Property(p => p.Longitude).IsRequired(false);
            });
            modelBuilder.Entity<ShippingRoute>()
    .Navigation(sr => sr.StartLocation)
    .IsRequired(false);

            // 6. Cấu hình cho ShippingRoute.EndLocation
            modelBuilder.Entity<ShippingRoute>().OwnsOne(sr => sr.EndLocation, loc =>
            {
                loc.Property(p => p.Address).HasMaxLength(500).IsRequired(false);
                loc.Property(p => p.Latitude).IsRequired(false);
                loc.Property(p => p.Longitude).IsRequired(false);
            });
            modelBuilder.Entity<ShippingRoute>()
    .Navigation(sr => sr.EndLocation)
    .IsRequired(false);

            // 7. Cấu hình cho TripDriverAssignment.StartLocation
            modelBuilder.Entity<TripDriverAssignment>().OwnsOne(tda => tda.StartLocation, loc =>
            {
                loc.Property(p => p.Address).HasMaxLength(500).IsRequired(false);
                loc.Property(p => p.Latitude).IsRequired(false);
                loc.Property(p => p.Longitude).IsRequired(false);
            });
            modelBuilder.Entity<TripDriverAssignment>()
    .Navigation(tda => tda.StartLocation)
    .IsRequired(false);

            // 8. Cấu hình cho TripDriverAssignment.EndLocation
            modelBuilder.Entity<TripDriverAssignment>().OwnsOne(tda => tda.EndLocation, loc =>
            {
                loc.Property(p => p.Address).HasMaxLength(500).IsRequired(false);
                loc.Property(p => p.Latitude).IsRequired(false);
                loc.Property(p => p.Longitude).IsRequired(false);
            });
            modelBuilder.Entity<TripDriverAssignment>()
    .Navigation(tda => tda.EndLocation)
    .IsRequired(false);

            // 9. Cấu hình cho TripDeliveryRecord.SignLocation
            //modelBuilder.Entity<TripDeliveryRecord>().OwnsOne(tdr => tdr.SignLocation, loc =>
            //{
            //    loc.Property(p => p.Address).HasMaxLength(500);
            //    loc.Property(p => p.Latitude);
            //    loc.Property(p => p.Longitude);
            //});

            // 10. Cấu hình cho ShippingRoute.PickupTimeWindow
            modelBuilder.Entity<ShippingRoute>().OwnsOne(sr => sr.PickupTimeWindow, tw =>
            {
                tw.Property(p => p.StartTime).IsRequired(false);
                tw.Property(p => p.EndTime).IsRequired(false);
            });

            // 11. Cấu hình cho ShippingRoute.DeliveryTimeWindow
            modelBuilder.Entity<ShippingRoute>().OwnsOne(sr => sr.DeliveryTimeWindow, tw =>
            {
                tw.Property(p => p.StartTime).IsRequired(false);
                tw.Property(p => p.EndTime).IsRequired(false);
            });


            // --- CẤU HÌNH TRƯỜNG LIST<STRING> (Dùng JSON) ---
            var stringListConverter = new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null) ?? new List<string>()
            );
            var stringListComparer = new ValueComparer<List<string>>(
                (c1, c2) => c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()
            );

           

            modelBuilder.Entity<Vehicle>()
                .Property(p => p.Features)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);
        }

        // =================================================================
        // HÀM 4: CẤU HÌNH CÁC MỐI QUAN HỆ (RELATIONSHIP)
        // =================================================================
        private void ConfigureRelationships(ModelBuilder modelBuilder)
        {
            // BaseUser & Role (1-n)
            modelBuilder.Entity<BaseUser>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Cấu hình quan hệ 1-N (1 User có nhiều Token)
            modelBuilder.Entity<UserDeviceToken>()
                .HasOne(t => t.User)
                .WithMany() // Hoặc .WithMany(u => u.DeviceTokens) nếu bên User có list
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade); // Xóa User thì xóa luôn Token

            // BaseUser & Wallet (1-1)
            modelBuilder.Entity<BaseUser>()
                .HasOne(u => u.Wallet)
                .WithOne(w => w.User)
                .HasForeignKey<Wallet>(w => w.UserId)
                .OnDelete(DeleteBehavior.Restrict); // Xóa user thì xóa ví

            // BaseUser 1-n (Tokens, Documents, Violations)
            modelBuilder.Entity<UserToken>()
                .HasOne(ut => ut.User)
                .WithMany(u => u.UserTokens)
                .HasForeignKey(ut => ut.UserId)
                .OnDelete(DeleteBehavior.Restrict); // Xóa user thì xóa token

            modelBuilder.Entity<ContactToken>()
                .HasOne(ct => ct.TripContact)
                .WithMany(tc => tc.ContactTokens)
                .HasForeignKey(ct => ct.TripContactId)
                .OnDelete(DeleteBehavior.Cascade); // Xóa TripContact thì xóa ContactToken

            modelBuilder.Entity<UserDocument>()
                .HasOne(ud => ud.User)
                .WithMany(u => u.UserDocuments)
                .HasForeignKey(ud => ud.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserViolation>()
                .HasOne(uv => uv.User)
                .WithMany(u => u.UserViolations)
                .HasForeignKey(uv => uv.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserViolation>()
                .HasOne(uv => uv.Processor)
                .WithMany() // Một Admin có thể xử lý nhiều vi phạm
                .HasForeignKey(uv => uv.ProcessorId)
                .OnDelete(DeleteBehavior.Restrict); // Admin bị xóa thì vẫn giữ log vi phạm

            // Wallet & Transaction (1-n)
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Wallet)
                .WithMany(w => w.Transactions)
                .HasForeignKey(t => t.WalletId)
                .OnDelete(DeleteBehavior.Cascade);

            // Owner & Driver (N-N qua OwnerDriverLink)
            // .HasKey() đã được chuyển lên ConfigurePrimaryKeys
            modelBuilder.Entity<OwnerDriverLink>()
                .HasOne(odl => odl.Owner)
                .WithMany(o => o.OwnerDriverLinks)
                .HasForeignKey(odl => odl.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<OwnerDriverLink>()
                .HasOne(odl => odl.Driver)
                .WithMany(d => d.OwnerDriverLinks)
                .HasForeignKey(odl => odl.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            // Owner 1-n (Vehicle, PostTrip, Trip, Item, Package, BaseContract)
            modelBuilder.Entity<Vehicle>()
                .HasOne(v => v.Owner)
                .WithMany(o => o.Vehicles)
                .HasForeignKey(v => v.OwnerId)
                .OnDelete(DeleteBehavior.Restrict); // Không cho xóa Owner nếu còn xe

            modelBuilder.Entity<PostTrip>()
                .HasOne(pt => pt.Owner)
                .WithMany(o => o.PostTrips)
                .HasForeignKey(pt => pt.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Trip>()
                .HasOne(t => t.Owner)
                .WithMany(o => o.Trips)
                .HasForeignKey(t => t.OwnerId)
                .OnDelete(DeleteBehavior.Restrict); // Không cho xóa Owner nếu còn Trip

            modelBuilder.Entity<Item>()
                .HasOne(i => i.Owner)
                .WithMany(o => o.Items)
                .HasForeignKey(i => i.OwnerId)
                .OnDelete(DeleteBehavior.Restrict); // Hàng hóa có thể do Owner/Provider tạo

            modelBuilder.Entity<Package>()
                .HasOne(p => p.Owner)
                .WithMany(o => o.Packages)
                .HasForeignKey(p => p.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BaseContract>()
                .HasOne(bc => bc.Owner)
                .WithMany(o => o.Contracts)
                .HasForeignKey(bc => bc.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            
            // Provider 1-n (PostPackage, Item, Package)
            modelBuilder.Entity<PostPackage>()
                .HasOne(pp => pp.Provider)
                .WithMany(p => p.PostPackages)
                .HasForeignKey(pp => pp.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Item>()
                .HasOne(i => i.Provider)
                .WithMany(p => p.Items)
                .HasForeignKey(i => i.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Package>()
                .HasOne(p => p.Provider)
                .WithMany(p => p.Packages)
                .HasForeignKey(p => p.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            // DriverContract & Counterparty
            modelBuilder.Entity<TripDriverContract>()
                .HasOne(bc => bc.Counterparty)
                .WithMany(d => d.TripDriverContracts) // BaseUser không cần ICollection<BaseContract>
                .HasForeignKey(bc => bc.CounterpartyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Provider Contract & Counterparty (
            modelBuilder.Entity<TripProviderContract>()
                .HasOne(bc => bc.Counterparty)
                .WithMany(p => p.TripProviderContracts) 
                .HasForeignKey(bc => bc.CounterpartyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Driver 1-n (Logs, Assignments, Records)
            modelBuilder.Entity<DriverActivityLog>()
                .HasOne(dal => dal.Driver)
                .WithMany(d => d.ActivityLogs)
                .HasForeignKey(dal => dal.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TripDriverAssignment>()
                .HasOne(tda => tda.Driver)
                .WithMany(d => d.TripDriverAssignments)
                .HasForeignKey(tda => tda.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TripDeliveryRecord>()
                .HasOne(tdr => tdr.Driver)
                .WithMany(d => d.TripDeliveryRecords)
                .HasForeignKey(tdr => tdr.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            // Vehicle 1-n (Images, Documents)
            modelBuilder.Entity<VehicleImage>()
                .HasOne(vi => vi.Vehicle)
                .WithMany(v => v.VehicleImages)
                .HasForeignKey(vi => vi.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<VehicleDocument>()
                .HasOne(vd => vd.Vehicle)
                .WithMany(v => v.VehicleDocuments)
                .HasForeignKey(vd => vd.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Vehicle>()
                .HasOne(v => v.VehicleType)
                .WithMany(vt => vt.Vehicles)
                .HasForeignKey(v => v.VehicleTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Item & Package (1-1)
            modelBuilder.Entity<Item>()
                .HasOne(i => i.Package)
                .WithOne(p => p.Item)
                .HasForeignKey<Package>(p => p.ItemId)
                .OnDelete(DeleteBehavior.Restrict); // Xóa Item thì xóa Package
            modelBuilder.Entity<ItemImage>()
                .HasOne(ii => ii.Item)
                .WithMany(i => i.ItemImages)
                .HasForeignKey(ii => ii.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PackageImage>()
                .HasOne(pi => pi.Package)
                .WithMany(p => p.PackageImages)
                .HasForeignKey(pi => pi.PackageId)
                .OnDelete(DeleteBehavior.Restrict);

            // Quan hệ 1-n (Package - PostPackage - Trip)
            modelBuilder.Entity<Package>()
                .HasOne(pp => pp.PostPackage)
                .WithMany(p => p.Packages)
                .HasForeignKey(p => p.PostPackageId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Package>()
                .HasOne(t => t.Trip)
                .WithMany(p => p.Packages)
                .HasForeignKey(p => p.TripId)
                .OnDelete(DeleteBehavior.Restrict); 

            // 1 - n Vehicle - trip
            modelBuilder.Entity<Trip>()
                .HasOne(t => t.Vehicle)
                .WithMany(v => v.Trips)
                .HasForeignKey(t => t.VehicleId)  
                .OnDelete(DeleteBehavior.Restrict); 

            modelBuilder.Entity<DriverWorkSession>()
                .HasOne(ds => ds.Driver)
                .WithMany(d => d.DriverWorkSessions)
                .HasForeignKey(ds => ds.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DriverWorkSession>()
                .HasOne(ds => ds.Trip)
                .WithMany(t => t.DriverWorkSessions)
                .HasForeignKey(ds => ds.TripId)
                .OnDelete(DeleteBehavior.Restrict);


            // Quan hệ 1-1 (ShippingRoute - PostPackage - PostTrip - Trip)
            modelBuilder.Entity<PostPackage>()
                .HasOne(pp => pp.ShippingRoute)
                .WithOne(sr => sr.PostPackage)
                .HasForeignKey<PostPackage>(pp => pp.ShippingRouteId)
                .OnDelete(DeleteBehavior.Restrict); // Giữ lại ShippingRoute để tái sử dụng
            //modelBuilder.Entity<PostTrip>()
            //    .HasOne(pt => pt.ShippingRoute)
            //    .WithOne(sr => sr.PostTrip)
            //    .HasForeignKey<PostTrip>(pt => pt.ShippingRouteId)
            //    .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Trip>()
                .HasOne(t => t.ShippingRoute)
                .WithOne(sr => sr.Trip)
                .HasForeignKey<Trip>(t => t.ShippingRouteId)
                .OnDelete(DeleteBehavior.Restrict);

            // Trip & TripRoute (1-1)
            modelBuilder.Entity<Trip>()
                .HasOne(t => t.TripRoute)
                .WithOne(tr => tr.Trip)
                .HasForeignKey<Trip>(t => t.TripRouteId)
                .OnDelete(DeleteBehavior.Restrict);

            // Trip 1-n (Contacts, Assignments, Suggestions, Records, Issues, Compensations)
            modelBuilder.Entity<TripContact>()
                .HasOne(tc => tc.Trip)
                .WithMany(t => t.TripContacts)
                .HasForeignKey(tc => tc.TripId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TripDriverAssignment>()
                .HasOne(tda => tda.Trip)
                .WithMany(t => t.DriverAssignments)
                .HasForeignKey(tda => tda.TripId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TripRouteSuggestion>()
                .HasOne(trs => trs.Trip)
                .WithMany(t => t.TripRouteSuggestions)
                .HasForeignKey(trs => trs.TripId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TripDeliveryRecord>()
                .HasOne(tdr => tdr.Trip)
                .WithMany(t => t.TripDeliveryRecords)
                .HasForeignKey(tdr => tdr.TripId)
                .OnDelete(DeleteBehavior.Restrict);

            // LƯU Ý: Đảm bảo class 'Trip' có 'public virtual ICollection<TripDeliveryIssue> DeliveryIssues'
            modelBuilder.Entity<TripDeliveryIssue>()
                .HasOne(tdi => tdi.Trip)
                .WithMany(t => t.DeliveryIssues)
                .HasForeignKey(tdi => tdi.TripId)
                .OnDelete(DeleteBehavior.Restrict);

         

            modelBuilder.Entity<TripDriverContract>()
                .HasOne(bc => bc.Trip)
                .WithMany(t => t.DriverContracts)
                .HasForeignKey(bc => bc.TripId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Trip>()
                  .HasOne(trip => trip.TripProviderContract) // 1. Trip có một (hoặc không) TripProviderContract
                  .WithOne(contract => contract.Trip)       // 2. TripProviderContract đó liên kết với một Trip
                  .HasForeignKey<TripProviderContract>(contract => contract.TripId) // 3. Khóa ngoại là 'TripId' nằm trên entity 'TripProviderContract'
                  .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Transaction>()
                .HasOne(tr => tr.Trip)
                .WithMany(t => t.Transactions)
                .HasForeignKey(tr => tr.TripId)
                .OnDelete(DeleteBehavior.Restrict); // Giữ lại giao dịch dù Trip bị xóa

            // Contract Template (1-n)
            modelBuilder.Entity<ContractTerm>()
                .HasOne(ct => ct.ContractTemplate)
                .WithMany(ctm => ctm.ContractTerms)
                .HasForeignKey(ct => ct.ContractTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<BaseContract>()
                .HasOne(bc => bc.ContractTemplate)
                .WithMany(ctm => ctm.BaseContracts)
                .HasForeignKey(bc => bc.ContractTemplateId)
                .OnDelete(DeleteBehavior.Restrict); // Giữ hợp đồng dù Template bị xóa

            // Delivery Record Template (1-n)
            modelBuilder.Entity<DeliveryRecordTerm>()
                .HasOne(drt => drt.DeliveryRecordTemplate)
                .WithMany(drtm => drtm.DeliveryRecordTerms)
                .HasForeignKey(drt => drt.DeliveryRecordTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<DeliveryRecord>()
                .HasOne(dr => dr.DeliveryRecordTemplate)
                .WithMany(drtm => drtm.DeliveryRecords)
                .HasForeignKey(dr => dr.DeliveryRecordTemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            // Issues & Compensations & Images
            modelBuilder.Entity<TripDeliveryIssue>()
                .HasOne(tdi => tdi.TripDeliveryRecord)
                .WithMany(tdr => tdr.Issues)
                .HasForeignKey(tdi => tdi.DeliveryRecordId)
                .OnDelete(DeleteBehavior.Restrict); // Vấn đề có thể không gắn với biên bản

            // === SỬA LỖI 1 (tiếp theo) ===
            modelBuilder.Entity<TripDeliveryIssueImage>() // Đổi tên class
                .HasOne(dii => dii.TripDeliveryIssue)
                .WithMany(tdi => tdi.DeliveryIssueImages)
                .HasForeignKey(dii => dii.TripDeliveryIssueId) // Dùng đúng FK
                .OnDelete(DeleteBehavior.Restrict);


            //modelBuilder.Entity<TripCompensation>()
            //    .HasOne(tc => tc.Issue)
            //    .WithMany(tdi => tdi.Compensations)
            //    .HasForeignKey(tc => tc.IssueId)
            //    .OnDelete(DeleteBehavior.Restrict);
           

            modelBuilder.Entity<PostContact>()
                .HasOne(pc => pc.PostPackage)
                .WithMany(pp => pp.PostContacts)
                .HasForeignKey(pc => pc.PostPackageId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PostTripDetail>()
                .HasOne(ptd => ptd.PostTrip)
                .WithMany(pt => pt.PostTripDetails)
                .HasForeignKey(ptd => ptd.PostTripId)
                .OnDelete(DeleteBehavior.Restrict);

            // 1. Quan hệ với Vehicle (1-N)
            modelBuilder.Entity<InspectionHistory>()
                .HasOne(h => h.Vehicle)             // Lịch sử có 1 Xe
                .WithMany(v => v.InspectionHistories) // Xe có nhiều Lịch sử
                .HasForeignKey(h => h.VehicleId)    // Khóa ngoại là VehicleId
                .OnDelete(DeleteBehavior.Cascade);  // QUAN TRỌNG: Xóa Xe -> Xóa luôn Lịch sử đăng kiểm

            // 2. Quan hệ với VehicleDocument (1-0..1)
            modelBuilder.Entity<InspectionHistory>()
                .HasOne(h => h.Document)            // Lịch sử có 1 Giấy tờ (hoặc null)
                .WithMany()                         // Giấy tờ không cần list lịch sử (Unidirectional)
                .HasForeignKey(h => h.VehicleDocumentId)
                .OnDelete(DeleteBehavior.SetNull);  // Nếu Xóa Giấy tờ -> Lịch sử vẫn giữ lại (nhưng link = null)


            // ============================================================
            // 1. CẤU HÌNH BIÊN BẢN GIAO NHẬN XE (TripVehicleHandoverRecord)
            // ============================================================
            modelBuilder.Entity<TripVehicleHandoverRecord>(entity =>
            {
                // Kế thừa từ DeliveryRecord (TPH - Table Per Hierarchy)
                // Nếu bạn muốn tách bảng riêng thì dùng .ToTable("TripVehicleHandoverRecords");

                // --- QUAN HỆ VỚI TRIP ---
                // Xóa Trip -> Xóa luôn biên bản (Cascade)
                entity.HasOne(e => e.Trip)
                      .WithMany(t => t.TripVehicleHandoverRecords) // Nếu bên Trip có collection thì điền vào: t => t.VehicleHandoverRecords
                      .HasForeignKey(e => e.TripId)
                      .OnDelete(DeleteBehavior.Restrict);

                // --- QUAN HỆ VỚI XE (VEHICLE) ---
                // Không cho phép xóa Xe nếu đang có biên bản gắn với nó (Restrict)
                entity.HasOne(e => e.Vehicle) // 1. Khai báo thuộc tính điều hướng trên TripVehicleHandoverRecord
                     .WithMany(v => v.TripVehicleHandoverRecords) // 2. Khai báo thuộc tính điều hướng trên Vehicle
                     .HasForeignKey(e => e.VehicleId) // 3. Chỉ định Khóa ngoại rõ ràng
                     .OnDelete(DeleteBehavior.Restrict);

                // --- QUAN HỆ VỚI USER (QUAN TRỌNG) ---
                // Một biên bản có 2 người: Người Giao (HandoverUser) và Người Nhận (ReceiverUser)
                // BẮT BUỘC dùng .OnDelete(DeleteBehavior.Restrict) để tránh lỗi vòng lặp SQL (Cycles)

                // Cấu hình Owner - CHẶN XÓA (Restrict)
                entity.HasOne(e => e.Owner)
                      .WithMany(o => o.TripVehicleHandoverRecords)
                      .HasForeignKey(e => e.OwnerId) // Phải khớp với tên property ở Bước 1
                      .OnDelete(DeleteBehavior.Restrict);

                // Cấu hình Driver - CHẶN XÓA (Restrict)
                entity.HasOne(e => e.Driver)
                      .WithMany(d => d.TripVehicleHandoverRecords)
                      .HasForeignKey(e => e.DriverId) // Phải khớp với tên property ở Bước 1
                      .OnDelete(DeleteBehavior.Restrict);

                // Cấu hình ValueObject cho Location (nếu dùng Owned Type)
                // entity.OwnsOne(e => e.ActualLocation);
            });

            // ============================================================
            // 2. CẤU HÌNH KẾT QUẢ CHECKLIST (TermResults)
            // ============================================================
            modelBuilder.Entity<TripVehicleHandoverTermResult>(entity =>
            {
                // Quan hệ với Biên bản: Biên bản xóa -> Kết quả check xóa theo (Cascade)
                entity.HasOne(e => e.TripVehicleHandoverRecord)
                      .WithMany(r => r.TermResults)
                      .HasForeignKey(e => e.TripVehicleHandoverRecordId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Quan hệ với Câu hỏi mẫu (Term): Không cho xóa câu hỏi nếu đã có record dùng nó (Restrict)
                entity.HasOne(e => e.DeliveryRecordTerm)
                      .WithMany()
                      .HasForeignKey(e => e.DeliveryRecordTermId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ============================================================
            // 3. CẤU HÌNH SỰ CỐ/HƯ HỎNG (Issues)
            // ============================================================
            modelBuilder.Entity<TripVehicleHandoverIssue>(entity =>
            {
                // Quan hệ với Biên bản: Biên bản xóa -> Sự cố xóa theo (Cascade)
                entity.HasOne(e => e.TripVehicleHandoverRecord)
                      .WithMany(r => r.Issues)
                      .HasForeignKey(e => e.TripVehicleHandoverRecordId)
                      .OnDelete(DeleteBehavior.Cascade);

                // (Tùy chọn) Lưu Enum dưới dạng String trong DB cho dễ đọc
                // entity.Property(e => e.IssueType).HasConversion<string>();
                // entity.Property(e => e.Status).HasConversion<string>();
            });

            // ============================================================
            // 4. CẤU HÌNH ẢNH SỰ CỐ (IssueImages)
            // ============================================================
            modelBuilder.Entity<TripVehicleHandoverIssueImage>(entity =>
            {
                // Sự cố xóa -> Ảnh xóa theo (Cascade)
                entity.HasOne(e => e.TripVehicleHandoverIssue)
                      .WithMany(i => i.Images)
                      .HasForeignKey(e => e.TripVehicleHandoverIssueId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<TripSurcharge>(entity =>
            {
                // 1. Quan hệ với Trip (SỬA LẠI)
                entity.HasOne(s => s.Trip)
                      .WithMany(t => t.Surcharges) // <--- QUAN TRỌNG: Phải trỏ đúng vào Collection trong Trip để mất cột TripId1
                      .HasForeignKey(s => s.TripId)
                      .OnDelete(DeleteBehavior.Restrict); // <--- QUAN TRỌNG: Đổi từ Cascade sang Restrict

                // 2. Quan hệ với Sự cố Xe (Giữ nguyên)
                entity.HasOne(s => s.TripVehicleHandoverIssue)
                      .WithMany(i => i.Surcharges)
                      .HasForeignKey(s => s.TripVehicleHandoverIssueId)
                      .OnDelete(DeleteBehavior.SetNull);

                // 3. Quan hệ với Sự cố Giao hàng (Giữ nguyên)
                entity.HasOne(s => s.TripDeliveryIssue)
                      .WithMany(i => i.Surcharges)
                      .HasForeignKey(s => s.TripDeliveryIssueId)
                      .OnDelete(DeleteBehavior.SetNull);

                // Cấu hình tiền tệ
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");

               
            });


            // CẤU HÌNH CHO TRIP
            modelBuilder.Entity<Trip>(entity =>
            {
                // Chuyển đổi TimeSpan <-> Int64 (Ticks)
                entity.Property(e => e.ActualDuration)
                    .HasConversion(
                        v => v.Ticks,                // Khi lưu vào DB: Đổi TimeSpan thành số Ticks (long)
                        v => new TimeSpan(v)         // Khi đọc từ DB: Đổi số Ticks thành TimeSpan
                    )
                    .HasColumnType("bigint");        // Bắt buộc cột trong SQL phải là bigint
            });

            // Nếu TripRoute cũng bị, làm tương tự:
            modelBuilder.Entity<TripRoute>(entity =>
            {
                entity.Property(e => e.Duration)
                    .HasConversion(v => v.Ticks, v => new TimeSpan(v))
                    .HasColumnType("bigint");
            });

        }




        // =================================================================
        // HÀM 5: CẤU HÌNH ĐỘ CHÍNH XÁC CHO DECIMAL
        // =================================================================
        private void ConfigureDecimalPrecision(ModelBuilder modelBuilder)
        {
            // --- Tiền tệ (Money): (18, 2) - Hỗ trợ đến 999 nghìn tỷ
            modelBuilder.Entity<BaseContract>().Property(p => p.ContractValue).HasPrecision(18, 2);
            modelBuilder.Entity<Item>().Property(p => p.DeclaredValue).HasPrecision(18, 2);
            modelBuilder.Entity<PostPackage>().Property(p => p.OfferedPrice).HasPrecision(18, 2);
            //modelBuilder.Entity<PostTrip>().Property(p => p.EstimatedFare).HasPrecision(18, 2);
            modelBuilder.Entity<PostTripDetail>().Property(p => p.PricePerPerson).HasPrecision(18, 2);
            modelBuilder.Entity<Transaction>().Property(p => p.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<Transaction>().Property(p => p.BalanceAfter).HasPrecision(18, 2);
            modelBuilder.Entity<Transaction>().Property(p => p.BalanceBefore).HasPrecision(18, 2);
            modelBuilder.Entity<Trip>().Property(p => p.TotalFare).HasPrecision(18, 2);
          
            modelBuilder.Entity<TripDriverAssignment>().Property(p => p.BaseAmount).HasPrecision(18, 2);
            modelBuilder.Entity<TripDriverAssignment>().Property(p => p.BonusAmount).HasPrecision(18, 2);
            modelBuilder.Entity<Wallet>().Property(p => p.Balance).HasPrecision(18, 2);
            modelBuilder.Entity<Wallet>().Property(p => p.FrozenBalance).HasPrecision(18, 2);

            // --- Đánh giá (Rating): (3, 2) - Hỗ trợ 4.75 điểm
            //modelBuilder.Entity<Driver>().Property(p => p.AverageRating).HasPrecision(3, 2);
            modelBuilder.Entity<Owner>().Property(p => p.AverageRating).HasPrecision(3, 2);
            modelBuilder.Entity<Provider>().Property(p => p.AverageRating).HasPrecision(3, 2);

            // --- Đo lường (Measurements): (10, 2) - Hỗ trợ 99,999,999.99 kg/km/m3
            modelBuilder.Entity<Package>().Property(p => p.VolumeM3).HasPrecision(10, 2);
            modelBuilder.Entity<Package>().Property(p => p.WeightKg).HasPrecision(10, 2);
            modelBuilder.Entity<PostTrip>().Property(p => p.RequiredPayloadInKg).HasPrecision(10, 2);
            modelBuilder.Entity<Trip>().Property(p => p.ActualDistanceKm).HasPrecision(10, 2);
            modelBuilder.Entity<TripRoute>().Property(p => p.DistanceKm).HasPrecision(10, 2);
            modelBuilder.Entity<TripRouteSuggestion>().Property(p => p.DistanceKm).HasPrecision(10, 2);
            modelBuilder.Entity<Vehicle>().Property(p => p.PayloadInKg).HasPrecision(10, 2);
            modelBuilder.Entity<Vehicle>().Property(p => p.VolumeInM3).HasPrecision(10, 2);

            // === BẮT ĐẦU THÊM MỚI ===


        }
    }
}