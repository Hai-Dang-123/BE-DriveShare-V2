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
        public DbSet<TripCompensation> TripCompensations { get; set; } = null!;

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
            modelBuilder.Entity<TripCompensation>().HasKey(e => e.TripCompensationId);

            // --- Các Bảng Hợp đồng (Templates) ---
            modelBuilder.Entity<ContractTemplate>().HasKey(e => e.ContractTemplateId);
            modelBuilder.Entity<ContractTerm>().HasKey(e => e.ContractTermId);

            // --- Các Bảng Biên bản & Vấn đề (Templates) ---
            modelBuilder.Entity<DeliveryRecordTemplate>().HasKey(e => e.DeliveryRecordTemplateId);
            modelBuilder.Entity<DeliveryRecordTerm>().HasKey(e => e.DeliveryRecordTermId);
            modelBuilder.Entity<TripDeliveryIssue>().HasKey(e => e.TripDeliveryIssueId);
            modelBuilder.Entity<TripDeliveryIssueImage>().HasKey(e => e.TripDeliveryIssueImageId);
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
        }

        // =================================================================
        // HÀM 3: CẤU HÌNH VALUE OBJECTS (LOCATION, TIMEWINDOW) & JSON
        // =================================================================
        private void ConfigureValueObjectsAndJson(ModelBuilder modelBuilder)
        {
            // --- CẤU HÌNH VALUE OBJECTS (Owned Types) ---

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

            modelBuilder.Entity<Package>()
                .Property(p => p.HandlingAttributes)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);

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
                .OnDelete(DeleteBehavior.Restrict); // Xóa Ví thì xóa Lịch sử giao dịch

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

            modelBuilder.Entity<TripCompensation>()
                .HasOne(tc => tc.Trip)
                .WithMany(t => t.Compensations)
                .HasForeignKey(tc => tc.TripId)
                .OnDelete(DeleteBehavior.Restrict);


            modelBuilder.Entity<TripDriverContract>()
                .HasOne(bc => bc.Trip)
                .WithMany(t => t.DriverContracts)
                .HasForeignKey(bc => bc.TripId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity < TripProviderContract>()
               .HasOne(bc => bc.Trip)
               .WithMany(t => t.ProviderContracts)
               .HasForeignKey(bc => bc.TripId)
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


            modelBuilder.Entity<TripCompensation>()
                .HasOne(tc => tc.Issue)
                .WithMany(tdi => tdi.Compensations)
                .HasForeignKey(tc => tc.IssueId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TripCompensation>()
                .HasOne(tc => tc.Requester)
                .WithMany()
                .HasForeignKey(tc => tc.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);
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
            modelBuilder.Entity<PostTrip>().Property(p => p.EstimatedFare).HasPrecision(18, 2);
            modelBuilder.Entity<Transaction>().Property(p => p.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<Transaction>().Property(p => p.BalanceAfter).HasPrecision(18, 2);
            modelBuilder.Entity<Transaction>().Property(p => p.BalanceBefore).HasPrecision(18, 2);
            modelBuilder.Entity<Trip>().Property(p => p.TotalFare).HasPrecision(18, 2);
            modelBuilder.Entity<TripCompensation>().Property(p => p.Amount).HasPrecision(18, 2);
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
            modelBuilder.Entity<ShippingRoute>().Property(p => p.EstimatedDistanceKm).HasPrecision(10, 2);
            modelBuilder.Entity<Trip>().Property(p => p.ActualDistanceKm).HasPrecision(10, 2);
            modelBuilder.Entity<TripRoute>().Property(p => p.DistanceKm).HasPrecision(10, 2);
            modelBuilder.Entity<TripRouteSuggestion>().Property(p => p.DistanceKm).HasPrecision(10, 2);
            modelBuilder.Entity<Vehicle>().Property(p => p.PayloadInKg).HasPrecision(10, 2);
            modelBuilder.Entity<Vehicle>().Property(p => p.VolumeInM3).HasPrecision(10, 2);

            // === BẮT ĐẦU THÊM MỚI ===


        }
    }
}