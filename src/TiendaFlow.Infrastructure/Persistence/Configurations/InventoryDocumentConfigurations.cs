using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class InventoryCountConfiguration : IEntityTypeConfiguration<InventoryCount>
{
    public void Configure(EntityTypeBuilder<InventoryCount> builder)
    {
        builder.ToTable("inventory_count");

        builder.HasKey(count => count.Id);

        builder.Property(count => count.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(count => count.StoreId)
            .HasColumnName("store_id");

        builder.Property(count => count.Location)
            .HasColumnName("location")
            .HasMaxLength(100);

        builder.Property(count => count.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2");

        builder.Property(count => count.ClosedAt)
            .HasColumnName("closed_at")
            .HasColumnType("datetime2");

        builder.Property(count => count.AdjustedAt)
            .HasColumnName("adjusted_at")
            .HasColumnType("datetime2");

        builder.Property(count => count.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(count => count.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(count => count.Lines)
            .WithOne()
            .HasForeignKey(line => line.CountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(count => new { count.StoreId, count.Status })
            .HasDatabaseName("IX_inventory_count_store_status");

        builder.HasIndex(count => count.CreatedAt)
            .HasDatabaseName("IX_inventory_count_created_at");
    }
}

public class InventoryCountLineConfiguration : IEntityTypeConfiguration<InventoryCountLine>
{
    public void Configure(EntityTypeBuilder<InventoryCountLine> builder)
    {
        builder.ToTable("inventory_count_line");

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(line => line.CountId)
            .HasColumnName("count_id");

        builder.Property(line => line.ProductSku)
            .HasColumnName("product_sku")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(line => line.SystemQuantity)
            .HasColumnName("system_quantity");

        builder.Property(line => line.CountedQuantity)
            .HasColumnName("counted_quantity");

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(line => line.ProductSku)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(line => new { line.CountId, line.ProductSku })
            .IsUnique()
            .HasDatabaseName("IX_inventory_count_line_count_sku");
    }
}

public class InventoryTransferConfiguration : IEntityTypeConfiguration<InventoryTransfer>
{
    public void Configure(EntityTypeBuilder<InventoryTransfer> builder)
    {
        builder.ToTable("inventory_transfer");

        builder.HasKey(transfer => transfer.Id);

        builder.Property(transfer => transfer.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(transfer => transfer.OriginStoreId)
            .HasColumnName("origin_store_id");

        builder.Property(transfer => transfer.DestinationStoreId)
            .HasColumnName("destination_store_id");

        builder.Property(transfer => transfer.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2");

        builder.Property(transfer => transfer.SentAt)
            .HasColumnName("sent_at")
            .HasColumnType("datetime2");

        builder.Property(transfer => transfer.ReceivedAt)
            .HasColumnName("received_at")
            .HasColumnType("datetime2");

        builder.Property(transfer => transfer.CancelledAt)
            .HasColumnName("cancelled_at")
            .HasColumnType("datetime2");

        builder.Property(transfer => transfer.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(transfer => transfer.OriginStoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(transfer => transfer.DestinationStoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(transfer => transfer.Lines)
            .WithOne()
            .HasForeignKey(line => line.TransferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(transfer => new { transfer.OriginStoreId, transfer.Status })
            .HasDatabaseName("IX_inventory_transfer_origin_status");

        builder.HasIndex(transfer => new { transfer.DestinationStoreId, transfer.Status })
            .HasDatabaseName("IX_inventory_transfer_destination_status");
    }
}

public class InventoryTransferLineConfiguration : IEntityTypeConfiguration<InventoryTransferLine>
{
    public void Configure(EntityTypeBuilder<InventoryTransferLine> builder)
    {
        builder.ToTable("inventory_transfer_line");

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(line => line.TransferId)
            .HasColumnName("transfer_id");

        builder.Property(line => line.ProductSku)
            .HasColumnName("product_sku")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(line => line.Quantity)
            .HasColumnName("quantity");

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(line => line.ProductSku)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(line => new { line.TransferId, line.ProductSku })
            .IsUnique()
            .HasDatabaseName("IX_inventory_transfer_line_transfer_sku");
    }
}
