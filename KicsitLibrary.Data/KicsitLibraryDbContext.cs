using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Data
{
    public class KicsitLibraryDbContext : DbContext
    {
        public KicsitLibraryDbContext(DbContextOptions<KicsitLibraryDbContext> options) : base(options)
        {
        }

        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<User> Users => Set<User>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();
        public DbSet<Student> Students => Set<Student>();
        public DbSet<FacultyStaff> FacultyStaff => Set<FacultyStaff>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<DepartmentCategory> DepartmentCategories => Set<DepartmentCategory>();
        public DbSet<LiteratureCategory> LiteratureCategories => Set<LiteratureCategory>();
        public DbSet<Author> Authors => Set<Author>();
        public DbSet<Publisher> Publishers => Set<Publisher>();
        public DbSet<BookMaster> BookMasters => Set<BookMaster>();
        public DbSet<BookAuthor> BookAuthors => Set<BookAuthor>();
        public DbSet<Rack> Racks => Set<Rack>();
        public DbSet<Shelf> Shelves => Set<Shelf>();
        public DbSet<BookLocation> BookLocations => Set<BookLocation>();
        public DbSet<BookCopy> BookCopies => Set<BookCopy>();
        public DbSet<IssueRecord> IssueRecords => Set<IssueRecord>();
        public DbSet<ReceiveRecord> ReceiveRecords => Set<ReceiveRecord>();
        public DbSet<Reservation> Reservations => Set<Reservation>();
        public DbSet<Fine> Fines => Set<Fine>();
        public DbSet<NotificationRecord> NotificationRecords => Set<NotificationRecord>();
        public DbSet<VisitRecord> VisitRecords => Set<VisitRecord>();
        public DbSet<VisitorFeedback> VisitorFeedbacks => Set<VisitorFeedback>();
        public DbSet<VisitFile> VisitFiles => Set<VisitFile>();
        public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
        public DbSet<AuditFile> AuditFiles => Set<AuditFile>();
        public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
        public DbSet<NewArrival> NewArrivals => Set<NewArrival>();
        public DbSet<DocumentUpload> DocumentUploads => Set<DocumentUpload>();
        public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
        public DbSet<ImportError> ImportErrors => Set<ImportError>();
        public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
        public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
        public DbSet<DeletedRecordArchive> DeletedRecordArchives => Set<DeletedRecordArchive>();
        public DbSet<StockVerificationSessionRecord> StockVerificationSessions => Set<StockVerificationSessionRecord>();
        public DbSet<StockVerificationEntry> StockVerificationEntries => Set<StockVerificationEntry>();
        public DbSet<BackupHistory> BackupHistories => Set<BackupHistory>();
        public DbSet<RestoreHistory> RestoreHistories => Set<RestoreHistory>();
        public DbSet<DatabaseRelocationHistory> DatabaseRelocationHistories => Set<DatabaseRelocationHistory>();
        public DbSet<NotificationQueue> NotificationQueues => Set<NotificationQueue>();

        public string CurrentTenantId { get; set; } = "default";

        // 4. Global Query Filters for Soft Delete and Multi-Tenancy
        private LambdaExpression GetFilterExpression(Type type)
        {
            var parameter = Expression.Parameter(type, "e");

            // e.IsDeleted == false
            var isDeletedProp = Expression.Property(parameter, nameof(EntityBase.IsDeleted));
            var falseConstant = Expression.Constant(false);
            var isDeletedExpr = Expression.Equal(isDeletedProp, falseConstant);

            // e.TenantId == this.CurrentTenantId
            var thisConst = Expression.Constant(this);
            var currentTenantProp = Expression.Property(thisConst, nameof(CurrentTenantId));
            var tenantIdProp = Expression.Property(parameter, nameof(EntityBase.TenantId));
            var tenantIdExpr = Expression.Equal(tenantIdProp, currentTenantProp);

            var body = Expression.AndAlso(isDeletedExpr, tenantIdExpr);
            return Expression.Lambda(body, parameter);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Composite Keys
            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            modelBuilder.Entity<BookAuthor>()
                .HasKey(ba => new { ba.BookMasterId, ba.AuthorId });

            // 2. Indexes
            modelBuilder.Entity<BookCopy>().HasIndex(bc => bc.AccessionNumber).IsUnique();
            modelBuilder.Entity<BookCopy>().HasIndex(bc => bc.Barcode).IsUnique();
            modelBuilder.Entity<BookMaster>().HasIndex(bm => bm.Title);
            modelBuilder.Entity<BookMaster>().HasIndex(bm => bm.ISBN);
            modelBuilder.Entity<BookMaster>().HasIndex(bm => bm.ISSN);
            modelBuilder.Entity<BookMaster>().HasIndex(bm => bm.UniqueTitleNumber).IsUnique();

            modelBuilder.Entity<Student>().HasIndex(s => s.RegistrationNumber).IsUnique();
            modelBuilder.Entity<Student>().HasIndex(s => s.AdmissionNumber);
            modelBuilder.Entity<Student>().HasIndex(s => s.PageNumber);
            modelBuilder.Entity<Student>().HasIndex(s => s.RegisterNumber);
            modelBuilder.Entity<Student>().HasIndex(s => s.ClearanceStatus);

            modelBuilder.Entity<FacultyStaff>().HasIndex(fs => fs.PersonnelNumber).IsUnique();
            modelBuilder.Entity<FacultyStaff>().HasIndex(fs => fs.ClearanceStatus);

            modelBuilder.Entity<IssueRecord>().HasIndex(ir => ir.AccessionNumber);
            modelBuilder.Entity<IssueRecord>().HasIndex(ir => ir.IssueDate);
            modelBuilder.Entity<IssueRecord>().HasIndex(ir => ir.ExpectedReturnDate);
            modelBuilder.Entity<NotificationRecord>().HasIndex(nr => nr.IssueRecordId);
            modelBuilder.Entity<NotificationRecord>().HasIndex(nr => nr.CreatedAt);
            modelBuilder.Entity<NotificationRecord>()
                .HasIndex(nr => nr.DeduplicationKey)
                .IsUnique();

            modelBuilder.Entity<ReceiveRecord>().HasIndex(rr => rr.ReceiveDate);

            modelBuilder.Entity<VisitRecord>().HasIndex(vr => vr.OrganizationName);
            modelBuilder.Entity<VisitRecord>().HasIndex(vr => vr.VisitDate);

            modelBuilder.Entity<AuditRecord>().HasIndex(ar => ar.AuditDate);

            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<SystemSettings>().HasIndex(ss => ss.Key).IsUnique();

            // Extra Lookups Indexes
            modelBuilder.Entity<Author>().HasIndex(a => a.Name);
            modelBuilder.Entity<Publisher>().HasIndex(p => p.Name);
            modelBuilder.Entity<BookMaster>().HasIndex(bm => bm.Subject);
            modelBuilder.Entity<InventoryItem>().HasIndex(ii => ii.ItemType);
            modelBuilder.Entity<StockVerificationSessionRecord>().HasIndex(item => item.SessionNumber).IsUnique();
            modelBuilder.Entity<StockVerificationEntry>().HasIndex(item => new { item.SessionId, item.BookCopyId }).IsUnique();
            modelBuilder.Entity<BackupHistory>().HasIndex(item => item.CreatedAt);
            modelBuilder.Entity<BackupHistory>().HasIndex(item => item.Status);
            modelBuilder.Entity<BackupHistory>().HasIndex(item => item.CreatedByUserName);
            modelBuilder.Entity<RestoreHistory>().HasIndex(item => item.StartedAt);
            modelBuilder.Entity<RestoreHistory>().HasIndex(item => item.Status);
            modelBuilder.Entity<RestoreHistory>().HasIndex(item => item.RequestedByUserName);
            modelBuilder.Entity<DatabaseRelocationHistory>().HasIndex(item => item.StartedAt);
            modelBuilder.Entity<DatabaseRelocationHistory>().HasIndex(item => item.Status);
            modelBuilder.Entity<DatabaseRelocationHistory>().HasIndex(item => item.RequestedByUserName);
            modelBuilder.Entity<DocumentUpload>().HasIndex(item => item.DocumentType);
            modelBuilder.Entity<DocumentUpload>().HasIndex(item => item.UploadDate);
            modelBuilder.Entity<DocumentUpload>().HasIndex(item => item.UploadedBy);
            modelBuilder.Entity<DocumentUpload>().HasIndex(item => new { item.RelatedEntityType, item.RelatedEntityId });
            modelBuilder.Entity<DocumentUpload>().HasIndex(item => item.FileSha256);

            // 2b. Enum to String Conversions
            modelBuilder.Entity<BookCopy>()
                .Property(bc => bc.AvailabilityStatus)
                .HasConversion<string>();

            modelBuilder.Entity<BookMaster>()
                .Property(bm => bm.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Student>()
                .Property(s => s.ClearanceStatus)
                .HasConversion<string>();

            modelBuilder.Entity<FacultyStaff>()
                .Property(fs => fs.FacultyType)
                .HasConversion<string>();
            modelBuilder.Entity<FacultyStaff>()
                .Property(fs => fs.ClearanceStatus)
                .HasConversion<string>();

            modelBuilder.Entity<Fine>()
                .Property(f => f.PaymentStatus)
                .HasConversion<string>();
            modelBuilder.Entity<Fine>()
                .Property(f => f.MemberType)
                .HasConversion<string>();

            modelBuilder.Entity<Reservation>()
                .Property(r => r.Status)
                .HasConversion<string>();
            modelBuilder.Entity<Reservation>()
                .Property(r => r.MemberType)
                .HasConversion<string>();

            modelBuilder.Entity<AuditRecord>()
                .Property(ar => ar.Status)
                .HasConversion<string>();

            modelBuilder.Entity<NotificationRecord>()
                .Property(nr => nr.Status)
                .HasConversion<string>();
            modelBuilder.Entity<NotificationRecord>()
                .Property(nr => nr.NotificationType)
                .HasConversion<string>();
            modelBuilder.Entity<NotificationRecord>()
                .Property(nr => nr.MemberType)
                .HasConversion<string>();

            modelBuilder.Entity<NotificationQueue>()
                .Property(nq => nq.Status)
                .HasConversion<string>();
            modelBuilder.Entity<NotificationQueue>()
                .Property(nq => nq.Channel)
                .HasConversion<string>();

            modelBuilder.Entity<InventoryItem>()
                .Property(ii => ii.ItemType)
                .HasConversion<string>();

            modelBuilder.Entity<IssueRecord>()
                .Property(ir => ir.MemberType)
                .HasConversion<string>();
            modelBuilder.Entity<StockVerificationEntry>()
                .Property(item => item.ExpectedStatus)
                .HasConversion<string>();
            modelBuilder.Entity<StockVerificationEntry>()
                .Property(item => item.ActualStatus)
                .HasConversion<string>();

            // 3. Relationships Configurations
            modelBuilder.Entity<BookCopy>()
                .HasOne(bc => bc.BookMaster)
                .WithMany(bm => bm.BookCopies)
                .HasForeignKey(bc => bc.BookMasterId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IssueRecord>()
                .HasOne(ir => ir.BookCopy)
                .WithMany(bc => bc.IssueRecords)
                .HasForeignKey(ir => ir.BookCopyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IssueRecord>()
                .HasOne(ir => ir.Student)
                .WithMany(s => s.IssueRecords)
                .HasForeignKey(ir => ir.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IssueRecord>()
                .HasOne(ir => ir.FacultyStaff)
                .WithMany(fs => fs.IssueRecords)
                .HasForeignKey(ir => ir.FacultyStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReceiveRecord>()
                .HasOne(rr => rr.IssueRecord)
                .WithOne(ir => ir.ReceiveRecord)
                .HasForeignKey<ReceiveRecord>(rr => rr.IssueRecordId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Fine>()
                .HasOne(f => f.IssueRecord)
                .WithOne(ir => ir.Fine)
                .HasForeignKey<Fine>(f => f.IssueRecordId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Fine>()
                .HasOne(f => f.Student)
                .WithMany(s => s.Fines)
                .HasForeignKey(f => f.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Fine>()
                .HasOne(f => f.FacultyStaff)
                .WithMany(fs => fs.Fines)
                .HasForeignKey(f => f.FacultyStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.BookMaster)
                .WithMany(bm => bm.Reservations)
                .HasForeignKey(r => r.BookMasterId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Student)
                .WithMany(s => s.Reservations)
                .HasForeignKey(r => r.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.FacultyStaff)
                .WithMany(fs => fs.Reservations)
                .HasForeignKey(r => r.FacultyStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<NotificationRecord>()
                .HasOne(nr => nr.IssueRecord)
                .WithMany(ir => ir.NotificationRecords)
                .HasForeignKey(nr => nr.IssueRecordId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<NotificationRecord>()
                .HasOne(nr => nr.Student)
                .WithMany(s => s.NotificationRecords)
                .HasForeignKey(nr => nr.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<NotificationRecord>()
                .HasOne(nr => nr.FacultyStaff)
                .WithMany(fs => fs.NotificationRecords)
                .HasForeignKey(nr => nr.FacultyStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockVerificationEntry>()
                .HasOne(item => item.Session)
                .WithMany(session => session.Items)
                .HasForeignKey(item => item.SessionId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StockVerificationEntry>()
                .HasOne(item => item.BookCopy)
                .WithMany()
                .HasForeignKey(item => item.BookCopyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DocumentUpload>()
                .HasOne(item => item.UploadedByUser)
                .WithMany()
                .HasForeignKey(item => item.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // 4. Global Query Filters for Soft Delete & Multi-Tenancy
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(EntityBase).IsAssignableFrom(entityType.ClrType))
                {
                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(GetFilterExpression(entityType.ClrType));
                }
            }
        }

        // 5. Intercept SaveChanges for Auditing, Soft Delete, and Multi-Tenancy
        public override int SaveChanges()
        {
            ApplyAuditSoftDeleteAndTenant();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyAuditSoftDeleteAndTenant();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void ApplyAuditSoftDeleteAndTenant()
        {
            var entries = ChangeTracker.Entries();
            foreach (var entry in entries)
            {
                if (entry.Entity is EntityBase entity)
                {
                    switch (entry.State)
                    {
                        case EntityState.Added:
                            entity.CreatedAt = DateTime.UtcNow;
                            entity.IsDeleted = false;
                            if (string.IsNullOrEmpty(entity.TenantId) || entity.TenantId == "default")
                            {
                                entity.TenantId = CurrentTenantId;
                            }
                            break;
                        case EntityState.Modified:
                            entity.UpdatedAt = DateTime.UtcNow;
                            break;
                        case EntityState.Deleted:
                            entry.State = EntityState.Modified;
                            entity.IsDeleted = true;
                            entity.DeletedAt = DateTime.UtcNow;
                            break;
                    }
                }
            }
        }

        public async Task EnsureDatabaseSchemaAsync()
        {
            var conn = Database.GetDbConnection();
            var wasClosed = conn.State == System.Data.ConnectionState.Closed;
            if (wasClosed)
            {
                await conn.OpenAsync();
            }

            try
            {
                var providerName = Database.ProviderName;
                var tables = new[] { "Users", "Roles", "Students", "FacultyStaff", "Categories", "BookMasters", "BookCopies", "IssueRecords", "ReceiveRecords", "Reservations", "Fines", "NotificationRecords", "VisitRecords", "AuditRecords", "InventoryItems", "DocumentUploads", "SystemSettings", "ActivityLogs", "NotificationQueues" };

                if (providerName != null && providerName.Contains("Sqlite"))
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='NotificationQueues';";
                    var hasQueueTable = await cmd.ExecuteScalarAsync() != null;
                    if (!hasQueueTable)
                    {
                        using var createCmd = conn.CreateCommand();
                        createCmd.CommandText = @"
                            CREATE TABLE NotificationQueues (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                TenantId TEXT NOT NULL DEFAULT 'default',
                                CreatedAt DATETIME NOT NULL,
                                UpdatedAt DATETIME NULL,
                                IsDeleted INTEGER NOT NULL DEFAULT 0,
                                DeletedAt DATETIME NULL,
                                DeletedReason TEXT NULL,
                                DeletedByUserId INTEGER NULL,
                                Recipient TEXT NOT NULL,
                                Message TEXT NOT NULL,
                                Channel TEXT NOT NULL,
                                Status TEXT NOT NULL,
                                ErrorMessage TEXT NULL,
                                AttemptCount INTEGER NOT NULL DEFAULT 0,
                                SentAt DATETIME NULL
                            );";
                        await createCmd.ExecuteNonQueryAsync();
                    }

                    foreach (var table in tables)
                    {
                        cmd.CommandText = $"PRAGMA table_info({table});";
                        var hasTenantId = false;
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                if (reader.GetString(1).Equals("TenantId", StringComparison.OrdinalIgnoreCase))
                                {
                                    hasTenantId = true;
                                    break;
                                }
                            }
                        }
                        if (!hasTenantId)
                        {
                            using var alterCmd = conn.CreateCommand();
                            alterCmd.CommandText = $"ALTER TABLE {table} ADD COLUMN TenantId TEXT NOT NULL DEFAULT 'default';";
                            await alterCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
                else if (providerName != null && providerName.Contains("SqlServer"))
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'NotificationQueues';";
                    var hasQueueTable = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0) > 0;
                    if (!hasQueueTable)
                    {
                        using var createCmd = conn.CreateCommand();
                        createCmd.CommandText = @"
                            CREATE TABLE NotificationQueues (
                                Id INT IDENTITY(1,1) PRIMARY KEY,
                                TenantId NVARCHAR(50) NOT NULL DEFAULT 'default',
                                CreatedAt DATETIME NOT NULL,
                                UpdatedAt DATETIME NULL,
                                IsDeleted BIT NOT NULL DEFAULT 0,
                                DeletedAt DATETIME NULL,
                                DeletedReason NVARCHAR(MAX) NULL,
                                DeletedByUserId INT NULL,
                                Recipient NVARCHAR(MAX) NOT NULL,
                                Message NVARCHAR(MAX) NOT NULL,
                                Channel NVARCHAR(50) NOT NULL,
                                Status NVARCHAR(50) NOT NULL,
                                ErrorMessage NVARCHAR(MAX) NULL,
                                AttemptCount INT NOT NULL DEFAULT 0,
                                SentAt DATETIME NULL
                            );";
                        await createCmd.ExecuteNonQueryAsync();
                    }

                    foreach (var table in tables)
                    {
                        cmd.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = 'TenantId';";
                        var hasTenantId = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0) > 0;
                        if (!hasTenantId)
                        {
                            using var alterCmd = conn.CreateCommand();
                            alterCmd.CommandText = $"ALTER TABLE {table} ADD TenantId NVARCHAR(50) NOT NULL DEFAULT 'default';";
                            await alterCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
                else if (providerName != null && (providerName.Contains("Npgsql") || providerName.Contains("PostgreSQL")))
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'notificationqueues';";
                    var hasQueueTable = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0) > 0;
                    if (!hasQueueTable)
                    {
                        using var createCmd = conn.CreateCommand();
                        createCmd.CommandText = @"
                            CREATE TABLE ""NotificationQueues"" (
                                ""Id"" SERIAL PRIMARY KEY,
                                ""TenantId"" VARCHAR(50) NOT NULL DEFAULT 'default',
                                ""CreatedAt"" TIMESTAMP NOT NULL,
                                ""UpdatedAt"" TIMESTAMP NULL,
                                ""IsDeleted"" BOOLEAN NOT NULL DEFAULT FALSE,
                                ""DeletedAt"" TIMESTAMP NULL,
                                ""DeletedReason"" TEXT NULL,
                                ""DeletedByUserId"" INTEGER NULL,
                                ""Recipient"" TEXT NOT NULL,
                                ""Message"" TEXT NOT NULL,
                                ""Channel"" VARCHAR(50) NOT NULL,
                                ""Status"" VARCHAR(50) NOT NULL,
                                ""ErrorMessage"" TEXT NULL,
                                ""AttemptCount"" INTEGER NOT NULL DEFAULT 0,
                                ""SentAt"" TIMESTAMP NULL
                            );";
                        await createCmd.ExecuteNonQueryAsync();
                    }

                    foreach (var table in tables)
                    {
                        cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.columns WHERE table_name = '{table.ToLower()}' AND column_name = 'tenantid';";
                        var hasTenantId = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0) > 0;
                        if (!hasTenantId)
                        {
                            using var alterCmd = conn.CreateCommand();
                            alterCmd.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"TenantId\" VARCHAR(50) NOT NULL DEFAULT 'default';";
                            await alterCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            finally
            {
                if (wasClosed)
                {
                    await conn.CloseAsync();
                }
            }
        }
    }
}
