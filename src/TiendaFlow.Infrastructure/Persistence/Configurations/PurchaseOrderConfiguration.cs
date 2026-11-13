using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_order");

        builder.HasKey(order => order.Id);

        builder.Property(order => order.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(order => order.SupplierId)
            .HasColumnName("supplier_id");

        builder.Property(order => order.StoreId)
            .HasColumnName("store_id");

        builder.Property(order => order.OrderDate)
            .HasColumnName("order_date")
            .HasColumnType("datetime2");

        builder.Property(order => order.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(order => order.Total)
            .HasColumnName("total")
            .HasPrecision(18, 2);

        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(order => order.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(order => order.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        // Supports the store deactivation guard: open orders of a store.
        builder.HasIndex(order => new { order.StoreId, order.Status })
            .HasDatabaseName("IX_purchase_order_store_status");

        builder.HasMany(order => order.Lines)
            .WithOne()
            .HasForeignKey(line => line.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(order => order.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("purchase_order_line");

        builder.HasKey(line => new { line.OrderId, line.ProductSku });

        builder.Property(line => line.OrderId)
            .HasColumnName("order_id");

        builder.Property(line => line.ProductSku)
            .HasColumnName("product_sku")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(line => line.Quantity)
            .HasColumnName("quantity");

        builder.Property(line => line.UnitCost)
            .HasColumnName("unit_cost")
            .HasPrecision(18, 2);

        builder.Property(line => line.ReceivedQuantity)
            .HasColumnName("received_qty");

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(line => line.ProductSku)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
