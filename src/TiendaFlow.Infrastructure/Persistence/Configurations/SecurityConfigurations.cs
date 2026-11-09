using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("store");

        builder.HasKey(store => store.Id);
        builder.Property(store => store.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(store => store.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(store => store.Address)
            .HasColumnName("address")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(store => store.Active)
            .HasColumnName("active")
            .IsRequired();

        builder.HasIndex(store => store.Name).IsUnique();
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("role");

        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(role => role.Name)
            .HasColumnName("name")
            .HasMaxLength(80)
            .IsRequired();

        builder.HasIndex(role => role.Name).IsUnique();
    }
}

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employee");

        builder.HasKey(employee => employee.Id);
        builder.Property(employee => employee.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(employee => employee.StoreId)
            .HasColumnName("store_id")
            .IsRequired();

        builder.Property(employee => employee.IdNumber)
            .HasColumnName("id_number")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(employee => employee.Name)
            .HasColumnName("name")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(employee => employee.Position)
            .HasColumnName("position")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(employee => employee.ContractType)
            .HasColumnName("contract_type")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(employee => employee.Salary)
            .HasColumnName("salary")
            .HasPrecision(18, 2);

        builder.Property(employee => employee.Active)
            .HasColumnName("active")
            .IsRequired();

        builder.HasIndex(employee => employee.IdNumber).IsUnique();

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(employee => employee.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_user");

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(user => user.EmployeeId)
            .HasColumnName("employee_id");

        builder.Property(user => user.RoleId)
            .HasColumnName("role_id")
            .IsRequired();

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(user => user.TwoFactor)
            .HasColumnName("two_factor")
            .IsRequired();

        builder.Property(user => user.Active)
            .HasColumnName("active")
            .IsRequired();

        builder.HasIndex(user => user.Email).IsUnique();

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(user => user.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(user => user.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
