using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class SalesReturnConfiguration : IEntityTypeConfiguration<SalesReturn>
{
    public void Configure(EntityTypeBuilder<SalesReturn> builder)
    {
        builder.ToTable("sales_return");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(item => item.InvoiceId)
            .HasColumnName("invoice_id");

        builder.Property(item => item.StoreId)
            .HasColumnName("store_id");

        builder.Property(item => item.CustomerName)
            .HasColumnName("customer_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(item => item.CustomerDocument)
            .HasColumnName("customer_document")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(item => item.Reason)
            .HasColumnName("reason")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(item => item.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(item => item.CreditNoteNumber)
            .HasColumnName("credit_note_number")
            .HasMaxLength(30);

        builder.Property(item => item.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2");

        builder.Property(item => item.AuthorizedAt)
            .HasColumnName("authorized_at")
            .HasColumnType("datetime2");

        builder.Property(item => item.RejectedAt)
            .HasColumnName("rejected_at")
            .HasColumnType("datetime2");

        builder.Property(item => item.Total)
            .HasColumnName("total")
            .HasPrecision(18, 2);

        builder.HasOne<SalesInvoice>()
            .WithMany()
            .HasForeignKey(item => item.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(item => item.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(item => item.Lines)
            .WithOne()
            .HasForeignKey(line => line.ReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.StoreId, item.Status })
            .HasDatabaseName("IX_sales_return_store_status");

        builder.HasIndex(item => new { item.InvoiceId, item.Status })
            .HasDatabaseName("IX_sales_return_invoice_status");

        builder.HasIndex(item => item.CreditNoteNumber)
            .IsUnique()
            .HasFilter("[credit_note_number] IS NOT NULL")
            .HasDatabaseName("IX_sales_return_credit_note");
    }
}

public class SalesReturnLineConfiguration : IEntityTypeConfiguration<SalesReturnLine>
{
    public void Configure(EntityTypeBuilder<SalesReturnLine> builder)
    {
        builder.ToTable("sales_return_line");

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(line => line.ReturnId)
            .HasColumnName("return_id");

        builder.Property(line => line.ProductSku)
            .HasColumnName("product_sku")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(line => line.Description)
            .HasColumnName("description")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(line => line.Quantity)
            .HasColumnName("quantity");

        builder.Property(line => line.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 2);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(line => line.ProductSku)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(line => new { line.ReturnId, line.ProductSku })
            .IsUnique()
            .HasDatabaseName("IX_sales_return_line_return_sku");
    }
}
