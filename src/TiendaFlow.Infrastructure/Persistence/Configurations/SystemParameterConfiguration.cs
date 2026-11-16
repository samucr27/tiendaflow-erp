using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class SystemParameterConfiguration : IEntityTypeConfiguration<SystemParameter>
{
    public void Configure(EntityTypeBuilder<SystemParameter> builder)
    {
        builder.ToTable("system_parameter");

        builder.HasKey(parameter => parameter.Id);
        builder.Property(parameter => parameter.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(parameter => parameter.Key)
            .HasColumnName("parameter_key")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(parameter => parameter.ValueType)
            .HasColumnName("value_type")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(parameter => parameter.Scope)
            .HasColumnName("scope")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(parameter => parameter.StoreId)
            .HasColumnName("store_id");

        builder.Property(parameter => parameter.Value)
            .HasColumnName("value")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(parameter => parameter.EffectiveFrom)
            .HasColumnName("effective_from")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(parameter => parameter.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(parameter => parameter.CreatedByUserId)
            .HasColumnName("created_by_user_id");

        builder.Property(parameter => parameter.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2")
            .HasConversion(
                value => UtcDateTimeConverter.ToUtc(value),
                value => UtcDateTimeConverter.FromUtc(value))
            .IsRequired();

        // SQL Server only keeps NULL values out of a filtered unique index, and
        // EF adds that filter automatically for nullable columns. A persisted
        // computed key maps "global" (NULL store) to 0 so the uniqueness rules
        // cover global and per-store versions with the same indexes.
        builder.Property<int>("StoreScopeKey")
            .HasColumnName("store_scope_key")
            .HasComputedColumnSql("ISNULL([store_id], 0)", stored: true);

        // One version number per key and scope; the store participates so a
        // store override keeps its own sequence.
        builder.HasIndex("Key", "Scope", "StoreScopeKey", "Version")
            .IsUnique()
            .HasDatabaseName("IX_system_parameter_key_scope_store_version");

        // One effective version per key and scope and date, so the latest row
        // for a given day is unambiguous.
        builder.HasIndex("Key", "Scope", "StoreScopeKey", "EffectiveFrom")
            .IsUnique()
            .HasDatabaseName("IX_system_parameter_key_scope_store_effective");

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(parameter => parameter.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(parameter => parameter.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
