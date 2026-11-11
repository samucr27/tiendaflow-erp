using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.ToTable("sales_invoice");

        builder.HasKey(invoice => invoice.Id);

        builder.Property(invoice => invoice.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(invoice => invoice.CustomerName)
            .HasColumnName("customer_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(invoice => invoice.CustomerDocument)
            .HasColumnName("customer_document")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(invoice => invoice.IssueDate)
            .HasColumnName("issue_date")
            .HasColumnType("datetime2");

        builder.Property(invoice => invoice.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(invoice => invoice.Total)
            .HasColumnName("total")
            .HasPrecision(18, 2);

        builder.HasMany(invoice => invoice.Lines)
            .WithOne()
            .HasForeignKey(line => line.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(invoice => invoice.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(invoice => new { invoice.CustomerDocument, invoice.IssueDate });
    }
}

public class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("sales_invoice_line");

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(line => line.InvoiceId)
            .HasColumnName("invoice_id");

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

        builder.HasIndex(line => new { line.InvoiceId, line.ProductSku });
    }
}
