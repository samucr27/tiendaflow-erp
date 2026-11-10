using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("stock");

        // A stock row is identified by the product SKU within a store.
        builder.HasKey(item => new { item.Sku, item.StoreId });

        builder.Property(item => item.Sku)
            .HasColumnName("sku")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(item => item.StoreId)
            .HasColumnName("store_id");

        builder.Property(item => item.QuantityAvailable)
            .HasColumnName("quantity");

        builder.Property(item => item.Location)
            .HasColumnName("location")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(item => item.MinStock)
            .HasColumnName("min_stock");

        // The domain only keeps the latest movement, so it is stored as an
        // owned snapshot in the stock row instead of a separate history table.
        builder.OwnsOne(item => item.LastMovement, movement =>
        {
            movement.Property(value => value.Type)
                .HasColumnName("last_movement_type")
                .HasConversion<string>()
                .HasMaxLength(20);

            movement.Property(value => value.Quantity)
                .HasColumnName("last_movement_quantity");

            movement.Property(value => value.Date)
                .HasColumnName("last_movement_date");
        });

        builder.Navigation(item => item.LastMovement).IsRequired(false);
    }
}
