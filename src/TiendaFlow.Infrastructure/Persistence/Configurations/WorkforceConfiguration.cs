using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("attendance_record");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(record => record.EmployeeId)
            .HasColumnName("employee_id")
            .IsRequired();

        builder.Property(record => record.StoreId)
            .HasColumnName("store_id")
            .IsRequired();

        builder.Property(record => record.WorkDate)
            .HasColumnName("work_date")
            .IsRequired();

        builder.Property(record => record.CheckIn)
            .HasColumnName("check_in");

        builder.Property(record => record.CheckOut)
            .HasColumnName("check_out");

        builder.Property(record => record.Novelty)
            .HasColumnName("novelty")
            .HasConversion<int>();

        builder.Property(record => record.NoveltyReason)
            .HasColumnName("novelty_reason")
            .HasMaxLength(AttendanceRecord.MaxReasonLength);

        builder.Property(record => record.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Ignore(record => record.HoursWorked);
        builder.Ignore(record => record.IsPresent);
        builder.Ignore(record => record.HasNovelty);

        // One attendance record per employee and day: a second check-in must
        // update the existing row, never create a duplicate day.
        builder.HasIndex(record => new { record.EmployeeId, record.WorkDate })
            .IsUnique()
            .HasDatabaseName("IX_attendance_record_employee_date");

        builder.HasIndex(record => new { record.WorkDate, record.StoreId })
            .HasDatabaseName("IX_attendance_record_date_store");

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(record => record.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(record => record.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ShiftTemplateConfiguration : IEntityTypeConfiguration<ShiftTemplate>
{
    public void Configure(EntityTypeBuilder<ShiftTemplate> builder)
    {
        builder.ToTable("shift_template");

        builder.HasKey(template => template.Id);
        builder.Property(template => template.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(template => template.Name)
            .HasColumnName("name")
            .HasMaxLength(ShiftTemplate.MaxNameLength)
            .IsRequired();

        builder.Property(template => template.StartTime)
            .HasColumnName("start_time")
            .IsRequired();

        builder.Property(template => template.EndTime)
            .HasColumnName("end_time")
            .IsRequired();

        builder.Property(template => template.Active)
            .HasColumnName("active")
            .IsRequired();

        builder.Property(template => template.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Ignore(template => template.DurationHours);

        builder.HasIndex(template => template.Name)
            .IsUnique()
            .HasDatabaseName("IX_shift_template_name");
    }
}

public class ShiftAssignmentConfiguration : IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftAssignment> builder)
    {
        builder.ToTable("shift_assignment");

        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(assignment => assignment.EmployeeId)
            .HasColumnName("employee_id")
            .IsRequired();

        builder.Property(assignment => assignment.StoreId)
            .HasColumnName("store_id")
            .IsRequired();

        builder.Property(assignment => assignment.ShiftTemplateId)
            .HasColumnName("shift_template_id")
            .IsRequired();

        builder.Property(assignment => assignment.WorkDate)
            .HasColumnName("work_date")
            .IsRequired();

        builder.Property(assignment => assignment.StartTime)
            .HasColumnName("start_time")
            .IsRequired();

        builder.Property(assignment => assignment.EndTime)
            .HasColumnName("end_time")
            .IsRequired();

        builder.Property(assignment => assignment.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Ignore(assignment => assignment.DurationHours);

        builder.HasIndex(assignment => new { assignment.EmployeeId, assignment.WorkDate })
            .HasDatabaseName("IX_shift_assignment_employee_date");

        builder.HasIndex(assignment => new { assignment.WorkDate, assignment.StoreId })
            .HasDatabaseName("IX_shift_assignment_date_store");

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(assignment => assignment.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(assignment => assignment.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ShiftTemplate>()
            .WithMany()
            .HasForeignKey(assignment => assignment.ShiftTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
