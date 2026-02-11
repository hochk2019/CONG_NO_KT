using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CongNoGolden.Infrastructure.Data;

public sealed class ConGNoDbContext : DbContext
{
    public ConGNoDbContext(DbContextOptions<ConGNoDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportStagingRow> ImportStagingRows => Set<ImportStagingRow>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Advance> Advances => Set<Advance>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptAllocation> ReceiptAllocations => Set<ReceiptAllocation>();
    public DbSet<PeriodLock> PeriodLocks => Set<PeriodLock>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RiskRule> RiskRules => Set<RiskRule>();
    public DbSet<ReminderSetting> ReminderSettings => Set<ReminderSetting>();
    public DbSet<ReminderLog> ReminderLogs => Set<ReminderLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<ZaloLinkToken> ZaloLinkTokens => Set<ZaloLinkToken>();
    public DbSet<BackupSettings> BackupSettings => Set<BackupSettings>();
    public DbSet<BackupJob> BackupJobs => Set<BackupJob>();
    public DbSet<BackupAudit> BackupAudits => Set<BackupAudit>();
    public DbSet<BackupUpload> BackupUploads => Set<BackupUpload>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("congno");

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(x => new { x.UserId, x.RoleId });
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<Seller>(entity =>
        {
            entity.ToTable("sellers");
            entity.HasKey(x => x.SellerTaxCode);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(x => x.TaxCode);
            var nameSearch = entity.Property<string>("NameSearch");
            nameSearch.HasColumnName("name_search");
            nameSearch.ValueGeneratedOnAddOrUpdate();
            nameSearch.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            nameSearch.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        });

        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.ToTable("import_batches");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SummaryData).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ImportStagingRow>(entity =>
        {
            entity.ToTable("import_staging_rows");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RawData).HasColumnType("jsonb");
            entity.Property(x => x.ValidationMessages).HasColumnType("jsonb");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("invoices");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Advance>(entity =>
        {
            entity.ToTable("advances");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.ToTable("receipts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AllocationTargets).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ReceiptAllocation>(entity =>
        {
            entity.ToTable("receipt_allocations");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<PeriodLock>(entity =>
        {
            entity.ToTable("period_locks");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BeforeData).HasColumnType("jsonb");
            entity.Property(x => x.AfterData).HasColumnType("jsonb");
        });

        modelBuilder.Entity<RiskRule>(entity =>
        {
            entity.ToTable("risk_rules");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<ReminderSetting>(entity =>
        {
            entity.ToTable("reminder_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Channels).HasColumnType("jsonb");
            entity.Property(x => x.TargetLevels).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ReminderLog>(entity =>
        {
            entity.ToTable("reminder_logs");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Metadata).HasColumnType("jsonb");
        });

        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.ToTable("notification_preferences");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.PopupSeverities).HasColumnType("jsonb");
            entity.Property(x => x.PopupSources).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ZaloLinkToken>(entity =>
        {
            entity.ToTable("zalo_link_tokens");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<BackupSettings>(entity =>
        {
            entity.ToTable("backup_settings");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<BackupJob>(entity =>
        {
            entity.ToTable("backup_jobs");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<BackupAudit>(entity =>
        {
            entity.ToTable("backup_audit");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Details).HasColumnType("jsonb");
        });

        modelBuilder.Entity<BackupUpload>(entity =>
        {
            entity.ToTable("backup_uploads");
            entity.HasKey(x => x.Id);
        });
    }
}
