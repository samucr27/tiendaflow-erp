using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("product");

        builder.HasKey(p => p.Sku);

        builder.Property(p => p.Sku).HasMaxLength(50);
        builder.Property(p => p.Barcode).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.SalePrice).HasPrecision(18, 2);

        builder.HasIndex(p => p.Barcode).IsUnique();
    }
}