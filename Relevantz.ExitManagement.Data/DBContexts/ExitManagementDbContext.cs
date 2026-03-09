using Microsoft.EntityFrameworkCore;
using Relevantz.ExitManagement.Common.Entities;

namespace Relevantz.ExitManagement.Data.DBContexts;

public class ExitManagementDbContext : DbContext
{
    public ExitManagementDbContext(DbContextOptions<ExitManagementDbContext> options)
        : base(options) { }

    public DbSet<Employee>         Employees         => Set<Employee>();
    public DbSet<Role>             Roles             => Set<Role>();
    public DbSet<ExitRequest>      ExitRequests      => Set<ExitRequest>();
    public DbSet<ExitApproval>     ExitApprovals     => Set<ExitApproval>();
    public DbSet<ClearanceItem>    ClearanceItems    => Set<ClearanceItem>();
    public DbSet<AuditLog>         AuditLogs         => Set<AuditLog>();
    public DbSet<Notification>     Notifications     { get; set; }
    public DbSet<AssetDeclaration> AssetDeclarations => Set<AssetDeclaration>();
    public DbSet<KtTask>           KtTasks           => Set<KtTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Employee ────────────────────────────────────────────────
        modelBuilder.Entity<Employee>(e =>
        {
            // EmployeeCode must be unique
            e.HasIndex(x => x.EmployeeCode)
             .IsUnique()
             .HasDatabaseName("UX_Employee_EmployeeCode");

            // Email must be unique
            e.HasIndex(x => x.Email)
             .IsUnique()
             .HasDatabaseName("UX_Employee_Email");

            // Self-referencing FK — L1 Manager
            e.HasOne<Employee>()
             .WithMany()
             .HasForeignKey(x => x.L1ManagerId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);

            // Self-referencing FK — L2 Manager
            e.HasOne<Employee>()
             .WithMany()
             .HasForeignKey(x => x.L2ManagerId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);

            e.HasMany(x => x.ExitRequests)
             .WithOne(er => er.Employee)
             .HasForeignKey(er => er.EmployeeId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Role ────────────────────────────────────────────────────
        modelBuilder.Entity<Role>(r =>
        {
            r.HasIndex(x => x.Name)
             .IsUnique()
             .HasDatabaseName("UX_Role_Name");
        });

        // ── ExitRequest ─────────────────────────────────────────────
        modelBuilder.Entity<ExitRequest>(er =>
        {
            er.HasMany(x => x.Approvals)
              .WithOne(a => a.ExitRequest)
              .HasForeignKey(a => a.ExitRequestId)
              .OnDelete(DeleteBehavior.Cascade);

            er.HasMany(x => x.AssetDeclarations)
              .WithOne(a => a.ExitRequest)
              .HasForeignKey(a => a.ExitRequestId)
              .OnDelete(DeleteBehavior.Cascade);

            er.HasMany(x => x.KtTasks)
              .WithOne(k => k.ExitRequest)
              .HasForeignKey(k => k.ExitRequestId)
              .OnDelete(DeleteBehavior.Cascade);

            // Enum → int conversions
            er.Property(x => x.Status)
              .HasConversion<int>();

            er.Property(x => x.ReasonType)
              .HasConversion<int>();

            er.Property(x => x.RiskLevel)
              .HasConversion<int>();

            er.Property(x => x.RehireDecision)
              .HasConversion<int?>();

            // Decimal precision
            er.Property(x => x.RiskScore)
              .HasDefaultValue(0);

            // Successor — no cascade (employee may not exist)
            er.HasOne<Employee>()
              .WithMany()
              .HasForeignKey(x => x.SuccessorEmployeeId)
              .OnDelete(DeleteBehavior.SetNull)
              .IsRequired(false);

            // Handover buddy — no cascade
            er.HasOne<Employee>()
              .WithMany()
              .HasForeignKey(x => x.HandoverBuddyId)
              .OnDelete(DeleteBehavior.SetNull)
              .IsRequired(false);

            // One active exit per employee at a time
            er.HasIndex(x => new { x.EmployeeId, x.Status })
              .HasDatabaseName("IX_ExitRequest_Employee_Status");
        });

        // ── ExitApproval ────────────────────────────────────────────
        modelBuilder.Entity<ExitApproval>(ea =>
        {
            ea.Property(x => x.Status)
              .HasConversion<int>();

            // One approval record per approver per exit request
            ea.HasIndex(x => new { x.ExitRequestId, x.ApproverId })
              .IsUnique()
              .HasDatabaseName("UX_ExitApproval_Request_Approver");
        });

        // ── ClearanceItem ───────────────────────────────────────────
        modelBuilder.Entity<ClearanceItem>(ci =>
        {
            ci.Property(x => x.Status)
              .HasConversion<int>();

            ci.Property(x => x.PendingDueAmount)
              .HasColumnType("decimal(18,2)");

            // One item per name per department per exit request
            ci.HasIndex(x => new { x.ExitRequestId, x.DepartmentName, x.ItemName })
              .IsUnique()
              .HasDatabaseName("UX_ClearanceItem_Request_Dept_Item");
        });

        // ── AssetDeclaration ─────────────────────────────────────────
        modelBuilder.Entity<AssetDeclaration>(ad =>
        {
            // Unique asset code per exit request (when code is provided)
            ad.HasIndex(x => new { x.ExitRequestId, x.AssetCode })
              .HasDatabaseName("IX_AssetDeclaration_Request_Code");
        });

        // ── KtTask ───────────────────────────────────────────────────
        modelBuilder.Entity<KtTask>(kt =>
        {
            // Unique title per exit request
            kt.HasIndex(x => new { x.ExitRequestId, x.Title })
              .IsUnique()
              .HasDatabaseName("UX_KtTask_Request_Title");
        });

        // ── AuditLog ─────────────────────────────────────────────────
        modelBuilder.Entity<AuditLog>(al =>
        {
            al.HasIndex(x => x.Timestamp)
              .HasDatabaseName("IX_AuditLog_Timestamp");
        });

        // ── Notification ──────────────────────────────────────────────
        modelBuilder.Entity<Notification>(n =>
        {
            n.HasIndex(x => new { x.RecipientEmployeeId, x.IsRead })
              .HasDatabaseName("IX_Notification_Recipient_Read");
        });
    }
}
