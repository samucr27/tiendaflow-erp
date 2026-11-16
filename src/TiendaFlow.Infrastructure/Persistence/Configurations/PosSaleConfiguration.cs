using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class PosSaleConfiguration : IEntityTypeConfiguration<PosSale>
{
    public void Configure(EntityTypeBuilder<PosSale> builder)
    {
        builder.ToTable("pos_sale");

        builder.HasKey(sale => sale.Id);

        builder.Property(sale => sale.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(sale => sale.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(sale => sale.StoreId)
            .HasColumnName("store_id");

        builder.Property(sale => sale.CashierUserId)
            .HasColumnName("cashier_user_id");

        builder.Property(sale => sale.SoldAt)
            .HasColumnName("sold_at")
            .HasColumnType("datetime2");

        builder.Property(sale => sale.Total)
            .HasColumnName("total")
            .HasPrecision(18, 2);

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(sale => sale.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(sale => sale.Lines)
            .WithOne()
            .HasForeignKey(line => line.PosSaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(sale => sale.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // The dashboard and the sales report read a day or a date range.
        builder.HasIndex(sale => new { sale.SoldAt, sale.StoreId })
            .HasDatabaseName("IX_pos_sale_sold_at_store");
    }
}

public class PosSaleLineConfiguration : IEntityTypeConfiguration<PosSaleLine>
{
    public void Configure(EntityTypeBuilder<PosSaleLine> builder)
    {
        builder.ToTable("pos_sale_line");

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(line => line.PosSaleId)
            .HasColumnName("pos_sale_id");

        builder.Property(line => line.ProductSku)
            .HasColumnName("product_sku")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(line => line.ProductName)
            .HasColumnName("product_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(line => line.Quantity)
            .HasColumnName("quantity");

        builder.Property(line => line.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 2);

        builder.HasIndex(line => line.ProductSku);
    }
}
