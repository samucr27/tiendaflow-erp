using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence.Configurations;

public class LoyaltyMovementConfiguration : IEntityTypeConfiguration<LoyaltyMovement>
{
    public void Configure(EntityTypeBuilder<LoyaltyMovement> builder)
    {
        builder.ToTable("loyalty_movement");

        builder.HasKey(movement => movement.Id);
        builder.Property(movement => movement.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(movement => movement.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(movement => movement.Type)
            .HasColumnName("type")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(movement => movement.Points)
            .HasColumnName("points")
            .IsRequired();

        builder.Property(movement => movement.AmountCop)
            .HasColumnName("amount_cop")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(movement => movement.StoreId)
            .HasColumnName("store_id")
            .IsRequired();

        builder.Property(movement => movement.Reference)
            .HasColumnName("reference")
            .HasMaxLength(LoyaltyMovement.MaxReferenceLength)
            .IsRequired();

        builder.Property(movement => movement.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Ignore(movement => movement.SignedPoints);

        // A sale or a discount must never be entered twice, even if the screen
        // is submitted twice or two cashiers use the same receipt number.
        builder.HasIndex(movement => new { movement.Type, movement.Reference })
            .IsUnique()
            .HasDatabaseName("IX_loyalty_movement_type_reference");

        builder.HasIndex(movement => new { movement.CustomerId, movement.CreatedAt })
            .HasDatabaseName("IX_loyalty_movement_customer_date");

        builder.HasIndex(movement => new { movement.CreatedAt, movement.StoreId })
            .HasDatabaseName("IX_loyalty_movement_date_store");

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(movement => movement.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(movement => movement.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PqrsCaseConfiguration : IEntityTypeConfiguration<PqrsCase>
{
    public void Configure(EntityTypeBuilder<PqrsCase> builder)
    {
        builder.ToTable("pqrs_case");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(item => item.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(item => item.StoreId)
            .HasColumnName("store_id")
            .IsRequired();

        builder.Property(item => item.Type)
            .HasColumnName("type")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(item => item.Subject)
            .HasColumnName("subject")
            .HasMaxLength(PqrsCase.MaxSubjectLength)
            .IsRequired();

        builder.Property(item => item.Description)
            .HasColumnName("description")
            .HasMaxLength(PqrsCase.MaxDescriptionLength)
            .IsRequired();

        builder.Property(item => item.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(item => item.CreatedDate)
            .HasColumnName("created_date")
            .IsRequired();

        builder.Property(item => item.DueDate)
            .HasColumnName("due_date")
            .IsRequired();

        builder.Property(item => item.Response)
            .HasColumnName("response")
            .HasMaxLength(PqrsCase.MaxResponseLength);

        builder.Property(item => item.AnsweredAt)
            .HasColumnName("answered_at");

        builder.Property(item => item.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Ignore("IsOverdue");

        builder.HasIndex(item => new { item.CreatedDate, item.StoreId })
            .HasDatabaseName("IX_pqrs_case_date_store");

        builder.HasIndex(item => new { item.Status, item.DueDate })
            .HasDatabaseName("IX_pqrs_case_status_due");

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(item => item.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(item => item.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("campaign");

        builder.HasKey(campaign => campaign.Id);
        builder.Property(campaign => campaign.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(campaign => campaign.Name)
            .HasColumnName("name")
            .HasMaxLength(Campaign.MaxNameLength)
            .IsRequired();

        builder.Property(campaign => campaign.Description)
            .HasColumnName("description")
            .HasMaxLength(Campaign.MaxDescriptionLength);

        builder.Property(campaign => campaign.Channel)
            .HasColumnName("channel")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(campaign => campaign.Segment)
            .HasColumnName("segment")
            .HasMaxLength(Campaign.MaxSegmentLength)
            .IsRequired();

        builder.Property(campaign => campaign.StartDate)
            .HasColumnName("start_date")
            .IsRequired();

        builder.Property(campaign => campaign.EndDate)
            .HasColumnName("end_date")
            .IsRequired();

        builder.Property(campaign => campaign.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(campaign => campaign.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Ignore("IsCurrentlyActive");

        builder.HasIndex(campaign => campaign.Name)
            .IsUnique()
            .HasDatabaseName("IX_campaign_name");

        builder.HasIndex(campaign => new { campaign.StartDate, campaign.EndDate })
            .HasDatabaseName("IX_campaign_dates");
    }
}
