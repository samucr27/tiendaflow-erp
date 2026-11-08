using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Application;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_log");

        // AuditEntry has no identifier; the database supplies a surrogate key.
        builder.Property<long>("Id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
        builder.HasKey("Id");

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
            .HasColumnName("user_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entry => entry.Date)
            .HasColumnName("logged_at")
            .HasColumnType("datetime2")
            .HasConversion(
                value => UtcDateTimeConverter.ToUtc(value),
                value => UtcDateTimeConverter.FromUtc(value));
    }
}

internal static class UtcDateTimeConverter
{
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public static DateTime FromUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
