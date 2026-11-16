using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class ProductSupplierConfiguration : IEntityTypeConfiguration<ProductSupplier>
{
    public void Configure(EntityTypeBuilder<ProductSupplier> builder)
    {
        builder.ToTable("product_supplier");

        builder.HasKey(relation => new { relation.ProductSku, relation.SupplierId });

        builder.Property(relation => relation.ProductSku)
            .HasColumnName("product_sku")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(relation => relation.SupplierId)
            .HasColumnName("supplier_id");

        builder.Property(relation => relation.LastCost)
            .HasColumnName("last_cost")
            .HasPrecision(18, 2);

        builder.Property(relation => relation.IsDefault)
            .HasColumnName("is_default");

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(relation => relation.ProductSku)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(relation => relation.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        // A product has one default supplier at most. The filtered unique
        // index enforces it in SQL Server even if two screens race.
        builder.HasIndex(relation => relation.ProductSku)
            .IsUnique()
            .HasFilter("[is_default] = 1")
            .HasDatabaseName("IX_product_supplier_default");
    }
}
