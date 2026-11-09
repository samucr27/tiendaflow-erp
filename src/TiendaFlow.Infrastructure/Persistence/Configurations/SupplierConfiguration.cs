using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("supplier");

        builder.HasKey(supplier => supplier.Id);

        builder.Property(supplier => supplier.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(supplier => supplier.Nit)
            .HasColumnName("nit")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(supplier => supplier.BusinessName)
            .HasColumnName("business_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(supplier => supplier.Address)
            .HasColumnName("address")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(supplier => supplier.Rating)
            .HasColumnName("rating")
            .HasPrecision(5, 2);

        builder.Property(supplier => supplier.Active)
            .HasColumnName("active");

        builder.HasIndex(supplier => supplier.Nit).IsUnique();
    }
}
