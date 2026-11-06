using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("StockItems");

        // Clave primaria compuesta (Sku, StoreId)
        builder.HasKey(s => new { s.Sku, s.StoreId });

        builder.Property(s => s.Sku)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.StoreId)
            .IsRequired();

        builder.Property(s => s.QuantityAvailable)
            .IsRequired();

        builder.Property(s => s.Location)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.MinStock)
            .IsRequired();

        // Mapeo del objeto de valor / Owned Entity simple para LastMovement
        builder.OwnsOne(s => s.LastMovement, m =>
        {
            m.Property(x => x.Type)
                .HasConversion<string>()
                .HasColumnName("LastMovementType");

            m.Property(x => x.Quantity)
                .HasColumnName("LastMovementQuantity");

            m.Property(x => x.Date)
                .HasColumnName("LastMovementDate");
        });
    }
}