using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class SalesReturnConfiguration : IEntityTypeConfiguration<SalesReturn>
{
    public void Configure(EntityTypeBuilder<SalesReturn> builder)
    {
        builder.ToTable("sales_return");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.InvoiceId).HasColumnName("invoice_id");
        builder.Property(item => item.StoreId).HasColumnName("store_id");
        builder.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(300).IsRequired();
        builder.Property(item => item.AuthorizedByUserId).HasColumnName("authorized_by_user_id");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("datetime2");
        builder.Property(item => item.Total).HasColumnName("total").HasPrecision(18, 2);

        builder.HasOne<SalesInvoice>().WithMany().HasForeignKey(item => item.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Store>().WithMany().HasForeignKey(item => item.StoreId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(item => item.Lines).WithOne().HasForeignKey(line => line.SalesReturnId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Lines).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

public class SalesReturnLineConfiguration : IEntityTypeConfiguration<SalesReturnLine>
{
    public void Configure(EntityTypeBuilder<SalesReturnLine> builder)
    {
        builder.ToTable("sales_return_line");
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(line => line.SalesReturnId).HasColumnName("sales_return_id");
        builder.Property(line => line.ProductSku).HasColumnName("product_sku").HasMaxLength(50).IsRequired();
        builder.Property(line => line.Description).HasColumnName("description").HasMaxLength(200).IsRequired();
        builder.Property(line => line.Quantity).HasColumnName("quantity");
        builder.Property(line => line.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2);
    }
}

public class EmploymentContractConfiguration : IEntityTypeConfiguration<EmploymentContract>
{
    public void Configure(EntityTypeBuilder<EmploymentContract> builder)
    {
        builder.ToTable("employment_contract");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.EmployeeId).HasColumnName("employee_id");
        builder.Property(item => item.Type).HasColumnName("contract_type").HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.StartDate).HasColumnName("start_date");
        builder.Property(item => item.EndDate).HasColumnName("end_date");
        builder.Property(item => item.Salary).HasColumnName("salary").HasPrecision(18, 2);
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(300);
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Extensions).HasColumnName("extensions");
        builder.HasOne<Employee>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.EmployeeId, item.Status });
    }
}

public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("attendance_record");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.EmployeeId).HasColumnName("employee_id");
        builder.Property(item => item.Date).HasColumnName("work_date");
        builder.Property(item => item.Type).HasColumnName("attendance_type").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.CheckIn).HasColumnName("check_in");
        builder.Property(item => item.CheckOut).HasColumnName("check_out");
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(300);
        builder.Ignore(item => item.HoursWorked);
        builder.Ignore(item => item.IsUnpaidAbsence);
        builder.HasOne<Employee>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.EmployeeId, item.Date }).IsUnique();
    }
}

public class WorkShiftConfiguration : IEntityTypeConfiguration<WorkShift>
{
    public void Configure(EntityTypeBuilder<WorkShift> builder)
    {
        builder.ToTable("work_shift");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.StoreId).HasColumnName("store_id");
        builder.Property(item => item.EmployeeId).HasColumnName("employee_id");
        builder.Property(item => item.Date).HasColumnName("shift_date");
        builder.Property(item => item.Start).HasColumnName("start_time");
        builder.Property(item => item.End).HasColumnName("end_time");
        builder.Ignore(item => item.Hours);
        builder.HasOne<Store>().WithMany().HasForeignKey(item => item.StoreId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.StoreId, item.Date });
    }
}

public class LoyaltyPointsEntryConfiguration : IEntityTypeConfiguration<LoyaltyPointsEntry>
{
    public void Configure(EntityTypeBuilder<LoyaltyPointsEntry> builder)
    {
        builder.ToTable("loyalty_points_entry");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.CustomerId).HasColumnName("customer_id");
        builder.Property(item => item.Points).HasColumnName("points");
        builder.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(200).IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("datetime2");
        builder.Property(item => item.InvoiceId).HasColumnName("invoice_id");
        builder.HasOne<Customer>().WithMany().HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.CustomerId);
        builder.HasIndex(item => item.InvoiceId);
    }
}

public class PqrsCaseConfiguration : IEntityTypeConfiguration<PqrsCase>
{
    public void Configure(EntityTypeBuilder<PqrsCase> builder)
    {
        builder.ToTable("pqrs_case");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.Type).HasColumnName("pqrs_type").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Subject).HasColumnName("subject").HasMaxLength(200).IsRequired();
        builder.Property(item => item.Description).HasColumnName("description").HasMaxLength(2000).IsRequired();
        builder.Property(item => item.CustomerId).HasColumnName("customer_id");
        builder.Property(item => item.Contact).HasColumnName("contact").HasMaxLength(200);
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("datetime2");
        builder.Property(item => item.DueDate).HasColumnName("due_date");
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Responsible).HasColumnName("responsible").HasMaxLength(200);
        builder.Property(item => item.Response).HasColumnName("response").HasMaxLength(2000);
        builder.Property(item => item.ClosedAt).HasColumnName("closed_at").HasColumnType("datetime2");
        builder.Ignore(item => item.FilingNumber);
        builder.HasOne<Customer>().WithMany().HasForeignKey(item => item.CustomerId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.Status, item.DueDate });
    }
}

public class QuotationRequestConfiguration : IEntityTypeConfiguration<QuotationRequest>
{
    public void Configure(EntityTypeBuilder<QuotationRequest> builder)
    {
        builder.ToTable("quotation_request");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.SupplierId).HasColumnName("supplier_id");
        builder.Property(item => item.ProductSku).HasColumnName("product_sku").HasMaxLength(50).IsRequired();
        builder.Property(item => item.Quantity).HasColumnName("quantity");
        builder.Property(item => item.UnitPrice).HasColumnName("unit_price").HasPrecision(14, 2);
        builder.Property(item => item.RequestedAt).HasColumnName("requested_at").HasColumnType("datetime2");
        builder.Property(item => item.ValidUntil).HasColumnName("valid_until");
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(500);
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(item => item.DecidedByName).HasColumnName("decided_by_name").HasMaxLength(200);
        builder.Property(item => item.DecidedAt).HasColumnName("decided_at").HasColumnType("datetime2");
        builder.Property(item => item.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);
        builder.Property(item => item.PurchaseOrderId).HasColumnName("purchase_order_id");
        builder.Ignore(item => item.EstimatedAmount);
        builder.Ignore(item => item.CanChange);
        builder.Ignore(item => item.CanDelete);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(item => item.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductSku).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(item => item.PurchaseOrderId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(item => item.DecidedByUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.Status, item.RequestedAt });
    }
}

public class MarketingCampaignConfiguration : IEntityTypeConfiguration<MarketingCampaign>
{
    public void Configure(EntityTypeBuilder<MarketingCampaign> builder)
    {
        builder.ToTable("marketing_campaign");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(item => item.Segment).HasColumnName("segment").HasMaxLength(1).IsRequired();
        builder.Property(item => item.StartDate).HasColumnName("start_date");
        builder.Property(item => item.EndDate).HasColumnName("end_date");
        builder.Property(item => item.Message).HasColumnName("message").HasMaxLength(500).IsRequired();
        builder.Property(item => item.Cancelled).HasColumnName("cancelled");
    }
}

public class PayrollRunRecordConfiguration : IEntityTypeConfiguration<PayrollRunRecord>
{
    public void Configure(EntityTypeBuilder<PayrollRunRecord> builder)
    {
        builder.ToTable("payroll_run");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(item => item.Period).HasColumnName("period").HasMaxLength(7).IsRequired();
        builder.Property(item => item.DaysWorked).HasColumnName("days_worked");
        builder.Property(item => item.RiskClass).HasColumnName("risk_class");
        builder.Property(item => item.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("datetime2");
        builder.Ignore(item => item.TotalGross);
        builder.Ignore(item => item.TotalDeductions);
        builder.Ignore(item => item.TotalNet);
        builder.Ignore(item => item.TotalEmployerCost);
        builder.HasIndex(item => item.Period).IsUnique();
        builder.HasMany(item => item.Lines).WithOne().HasForeignKey(line => line.PayrollRunRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Lines).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

public class PayrollRunRecordLineConfiguration : IEntityTypeConfiguration<PayrollRunRecordLine>
{
    public void Configure(EntityTypeBuilder<PayrollRunRecordLine> builder)
    {
        builder.ToTable("payroll_run_line");
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(line => line.PayrollRunRecordId).HasColumnName("payroll_run_id");
        builder.Property(line => line.EmployeeId).HasColumnName("employee_id");
        builder.Property(line => line.EmployeeName).HasColumnName("employee_name").HasMaxLength(200).IsRequired();
        builder.Property(line => line.DaysApplied).HasColumnName("days_applied");
        builder.Property(line => line.GrossEarnings).HasColumnName("gross_earnings").HasPrecision(18, 2);
        builder.Property(line => line.Deductions).HasColumnName("deductions").HasPrecision(18, 2);
        builder.Property(line => line.NetPay).HasColumnName("net_pay").HasPrecision(18, 2);
        builder.Property(line => line.EmployerCost).HasColumnName("employer_cost").HasPrecision(18, 2);
    }
}

public class RolePermissionGrantConfiguration : IEntityTypeConfiguration<RolePermissionGrant>
{
    public void Configure(EntityTypeBuilder<RolePermissionGrant> builder)
    {
        builder.ToTable("role_permission");
        builder.HasKey(item => new { item.RoleId, item.Permission });
        builder.Property(item => item.RoleId).HasColumnName("role_id");
        builder.Property(item => item.Permission).HasColumnName("permission").HasMaxLength(60);
        builder.HasOne<Role>().WithMany().HasForeignKey(item => item.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}
