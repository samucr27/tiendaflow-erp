using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customer");

        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(customer => customer.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(customer => customer.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(customer => customer.Email)
            .HasColumnName("email")
            .HasMaxLength(200);

        builder.Property(customer => customer.Phone)
            .HasColumnName("phone")
            .HasMaxLength(50);

        builder.Property(customer => customer.Address)
            .HasColumnName("address")
            .HasMaxLength(200);

        builder.Property(customer => customer.Active)
            .HasColumnName("active");

        builder.HasIndex(customer => customer.DocumentNumber).IsUnique();
    }
}
