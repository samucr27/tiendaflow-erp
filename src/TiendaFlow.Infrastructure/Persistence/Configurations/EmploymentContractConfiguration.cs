using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class EmploymentContractConfiguration : IEntityTypeConfiguration<EmploymentContract>
{
    public void Configure(EntityTypeBuilder<EmploymentContract> builder)
    {
        builder.ToTable("employee_contract");

        builder.HasKey(contract => contract.Id);
        builder.Property(contract => contract.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(contract => contract.EmployeeId)
            .HasColumnName("employee_id")
            .IsRequired();

        builder.Property(contract => contract.ContractType)
            .HasColumnName("contract_type")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(contract => contract.StartDate)
            .HasColumnName("start_date")
            .IsRequired();

        builder.Property(contract => contract.EndDate)
            .HasColumnName("end_date");

        builder.Property(contract => contract.BaseSalary)
            .HasColumnName("base_salary")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(contract => contract.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(contract => contract.PreviousContractId)
            .HasColumnName("previous_contract_id");

        builder.Property(contract => contract.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(contract => contract.TerminatedAt)
            .HasColumnName("terminated_at");

        builder.HasIndex(contract => new { contract.EmployeeId, contract.Status })
            .HasDatabaseName("IX_employee_contract_employee_status");

        builder.HasIndex(contract => contract.EndDate)
            .HasDatabaseName("IX_employee_contract_end_date");

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(contract => contract.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EmploymentContract>()
            .WithMany()
            .HasForeignKey(contract => contract.PreviousContractId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ContractAttachmentConfiguration : IEntityTypeConfiguration<ContractAttachment>
{
    public void Configure(EntityTypeBuilder<ContractAttachment> builder)
    {
        builder.ToTable("contract_attachment");

        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(attachment => attachment.ContractId)
            .HasColumnName("contract_id")
            .IsRequired();

        builder.Property(attachment => attachment.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(attachment => attachment.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(attachment => attachment.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(attachment => attachment.UploadedByUserId)
            .HasColumnName("uploaded_by_user_id");

        builder.Property(attachment => attachment.UploadedAt)
            .HasColumnName("uploaded_at")
            .IsRequired();

        builder.Ignore(attachment => attachment.SizeBytes);

        builder.HasIndex(attachment => attachment.ContractId)
            .HasDatabaseName("IX_contract_attachment_contract");

        builder.HasOne<EmploymentContract>()
            .WithMany()
            .HasForeignKey(attachment => attachment.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(attachment => attachment.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
