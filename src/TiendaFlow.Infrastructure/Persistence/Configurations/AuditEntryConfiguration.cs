using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Application;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_log");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(entry => entry.Action)
            .HasColumnName("action")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entry => entry.EntityId)
            .HasColumnName("entity_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(entry => entry.Detail)
            .HasColumnName("detail")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(entry => entry.UserId)
            .HasColumnName("user_id");

        builder.Property(entry => entry.Module)
            .HasColumnName("module")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(entry => entry.Ip)
            .HasColumnName("ip")
            .HasMaxLength(64);

        builder.Property(entry => entry.Date)
            .HasColumnName("logged_at")
            .HasColumnType("datetime2")
            .HasConversion(
                value => UtcDateTimeConverter.ToUtc(value),
                value => UtcDateTimeConverter.FromUtc(value));

        builder.HasOne<Domain.AppUser>()
            .WithMany()
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal static class UtcDateTimeConverter
{
    public static DateTime ToUtc(DateTime value) => Domain.UtcTimestamp.Normalize(value);

    public static DateTime FromUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
