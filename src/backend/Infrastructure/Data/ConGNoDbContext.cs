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
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportStagingRow> ImportStagingRows => Set<ImportStagingRow>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceReductionApplication> InvoiceReductionApplications => Set<InvoiceReductionApplication>();
    public DbSet<Advance> Advances => Set<Advance>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptAllocation> ReceiptAllocations => Set<ReceiptAllocation>();
    public DbSet<ReceiptHeldCredit> ReceiptHeldCredits => Set<ReceiptHeldCredit>();
    public DbSet<PeriodLock> PeriodLocks => Set<PeriodLock>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RiskRule> RiskRules => Set<RiskRule>();
    public DbSet<ReminderSetting> ReminderSettings => Set<ReminderSetting>();
    public DbSet<ReminderLog> ReminderLogs => Set<ReminderLog>();
    public DbSet<ReminderResponseState> ReminderResponseStates => Set<ReminderResponseState>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<RiskMlModel> RiskMlModels => Set<RiskMlModel>();
    public DbSet<RiskMlTrainingRun> RiskMlTrainingRuns => Set<RiskMlTrainingRun>();
    public DbSet<ZaloLinkToken> ZaloLinkTokens => Set<ZaloLinkToken>();
    public DbSet<BackupSettings> BackupSettings => Set<BackupSettings>();
    public DbSet<BackupJob> BackupJobs => Set<BackupJob>();
    public DbSet<BackupAudit> BackupAudits => Set<BackupAudit>();
    public DbSet<BackupUpload> BackupUploads => Set<BackupUpload>();
    public DbSet<ErpIntegrationSetting> ErpIntegrationSettings => Set<ErpIntegrationSetting>();
    public DbSet<ReportDeliverySchedule> ReportDeliverySchedules => Set<ReportDeliverySchedule>();
    public DbSet<ReportDeliveryRun> ReportDeliveryRuns => Set<ReportDeliveryRun>();
    public DbSet<RiskScoreSnapshot> RiskScoreSnapshots => Set<RiskScoreSnapshot>();
    public DbSet<RiskDeltaAlert> RiskDeltaAlerts => Set<RiskDeltaAlert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("congno");

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FailedLoginCount).HasDefaultValue(0);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(x => new { x.UserId, x.RoleId });
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasKey(x => new { x.RoleId, x.PermissionId });
            entity.HasIndex(x => x.PermissionId);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.AbsoluteExpiresAt);
            entity.Property(x => x.DeviceFingerprintHash).HasMaxLength(64);
            entity.Property(x => x.IpPrefix).HasMaxLength(64);
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
            entity.ToTable("invoices", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_invoice_type",
                    "invoice_type IN ('NORMAL','REPLACE','ADJUST','ADJUSTMENT_REDUCTION')");
                tableBuilder.HasCheckConstraint(
                    "ck_invoice_status",
                    "status IN ('OPEN','PARTIAL','PAID','VOID','DISPUTE')");
                tableBuilder.HasCheckConstraint(
                    "ck_invoice_adjust_negative",
                    "invoice_type NOT IN ('ADJUST','ADJUSTMENT_REDUCTION') OR (revenue_excl_vat <= 0 AND vat_amount <= 0 AND total_amount <= 0)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.InvoiceType).HasMaxLength(32).HasDefaultValue("NORMAL");
            entity.Property(x => x.Status).HasMaxLength(16).HasDefaultValue("OPEN");
            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(x => x.CustomerTaxCode)
                .HasPrincipalKey(x => x.TaxCode)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("invoices_customer_tax_code_fkey");
        });

        modelBuilder.Entity<InvoiceReductionApplication>(entity =>
        {
            entity.ToTable("invoice_reduction_applications");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ReductionInvoiceId);
            entity.HasIndex(x => x.AppliedInvoiceId);
            entity.HasOne<Invoice>()
                .WithMany()
                .HasForeignKey(x => x.ReductionInvoiceId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("invoice_reduction_applications_reduction_invoice_id_fkey");
            entity.HasOne<Invoice>()
                .WithMany()
                .HasForeignKey(x => x.AppliedInvoiceId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("invoice_reduction_applications_applied_invoice_id_fkey");
        });

        modelBuilder.Entity<Advance>(entity =>
        {
            entity.ToTable("advances");
            entity.HasKey(x => x.Id);
            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(x => x.CustomerTaxCode)
                .HasPrincipalKey(x => x.TaxCode)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("advances_customer_tax_code_fkey");
        });

        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.ToTable("receipts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AllocationTargets).HasColumnType("jsonb");
            entity.Property(x => x.AutoAllocateEnabled).HasDefaultValue(true);
            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(x => x.CustomerTaxCode)
                .HasPrincipalKey(x => x.TaxCode)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("receipts_customer_tax_code_fkey");
        });

        modelBuilder.Entity<ReceiptAllocation>(entity =>
        {
            entity.ToTable("receipt_allocations");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<ReceiptHeldCredit>(entity =>
        {
            entity.ToTable("receipt_held_credits");
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
            entity.Property(x => x.MatchMode).HasMaxLength(8).HasDefaultValue("ANY");
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

        modelBuilder.Entity<ReminderResponseState>(entity =>
        {
            entity.ToTable("reminder_response_states");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CustomerTaxCode, x.Channel }).IsUnique();
            entity.HasIndex(x => x.ResponseStatus);
            entity.HasIndex(x => x.LatestResponseAt);
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

        modelBuilder.Entity<RiskMlModel>(entity =>
        {
            entity.ToTable("risk_ml_models");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ModelKey, x.Version }).IsUnique();
            entity.Property(x => x.FeatureSchema).HasColumnType("jsonb");
            entity.Property(x => x.Parameters).HasColumnType("jsonb");
            entity.Property(x => x.Metrics).HasColumnType("jsonb");
            entity.Property(x => x.PositiveRatio).HasPrecision(8, 6);
        });

        modelBuilder.Entity<RiskMlTrainingRun>(entity =>
        {
            entity.ToTable("risk_ml_training_runs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.StartedAt);
            entity.Property(x => x.Metrics).HasColumnType("jsonb");
            entity.Property(x => x.PositiveRatio).HasPrecision(8, 6);
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

        modelBuilder.Entity<ErpIntegrationSetting>(entity =>
        {
            entity.ToTable("erp_integration_settings");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UpdatedAt);
        });

        modelBuilder.Entity<ReportDeliverySchedule>(entity =>
        {
            entity.ToTable("report_delivery_schedules");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => new { x.Enabled, x.NextRunAt });
            entity.Property(x => x.Recipients).HasColumnType("jsonb");
            entity.Property(x => x.FilterPayload).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ReportDeliveryRun>(entity =>
        {
            entity.ToTable("report_delivery_runs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ScheduleId, x.StartedAt });
            entity.Property(x => x.ArtifactMeta).HasColumnType("jsonb");
        });

        modelBuilder.Entity<RiskScoreSnapshot>(entity =>
        {
            entity.ToTable("risk_score_snapshots");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.AsOfDate);
            entity.HasIndex(x => new { x.CustomerTaxCode, x.CreatedAt });
            entity.HasIndex(x => new { x.CustomerTaxCode, x.AsOfDate }).IsUnique();
            entity.Property(x => x.Score).HasPrecision(10, 4);
        });

        modelBuilder.Entity<RiskDeltaAlert>(entity =>
        {
            entity.ToTable("risk_delta_alerts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Status, x.DetectedAt });
            entity.HasIndex(x => new { x.CustomerTaxCode, x.CreatedAt });
            entity.HasIndex(x => new { x.CustomerTaxCode, x.AsOfDate }).IsUnique();
            entity.Property(x => x.PrevScore).HasPrecision(10, 4);
            entity.Property(x => x.CurrScore).HasPrecision(10, 4);
            entity.Property(x => x.Delta).HasPrecision(10, 4);
            entity.Property(x => x.Threshold).HasPrecision(10, 4);
        });
    }
}
