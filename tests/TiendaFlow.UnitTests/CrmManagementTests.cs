using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

/// <summary>
/// Domain rules of the new CRM modules. The ledger is immutable, a PQRS case
/// only moves forward and a campaign is editable only while it is a draft. All
/// timestamps are fixed and no test depends on the machine clock or timezone.
/// </summary>
public class CrmManagementTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 18);

    [Fact]
    public void ALoyaltyMovementKeepsItsSignedPointsAndTimestampsInUtc()
    {
        var movement = new LoyaltyMovement(
            1, 7, LoyaltyMovementType.Earned, 25, 25000m, 1, "  FACT-1  ", Now);

        Assert.Equal("FACT-1", movement.Reference);
        Assert.Equal(25, movement.SignedPoints);
        Assert.Equal(DateTimeKind.Utc, movement.CreatedAt.Kind);
        Assert.Equal(Now, movement.CreatedAt);

        var redeemed = new LoyaltyMovement(
            2, 7, LoyaltyMovementType.Redeemed, 10, 5000m, 1, "CANJE-1", Now);

        Assert.Equal(-10, redeemed.SignedPoints);
    }

    [Theory]
    [InlineData(0, 100, "FACT")]
    [InlineData(5, 0, "FACT")]
    [InlineData(5, 100, "  ")]
    public void AnInvalidLoyaltyMovementIsRejected(int points, int amount, string reference)
    {
        Assert.Throws<InvalidLoyaltyMovementException>(() => new LoyaltyMovement(
            0, 7, LoyaltyMovementType.Earned, points, amount, 1, reference, Now));
    }

    [Fact]
    public void APqrsCaseFollowsTheForwardOnlyWorkflow()
    {
        var item = NewCase();

        Assert.Equal(PqrsStatus.Open, item.Status);
        Assert.True(item.IsOverdue(Today.AddDays(21)));

        item.Start();
        Assert.Equal(PqrsStatus.InProgress, item.Status);

        item.Answer("  Respuesta enviada al cliente.  ", Now.AddHours(2));
        Assert.Equal(PqrsStatus.Answered, item.Status);
        Assert.Equal("Respuesta enviada al cliente.", item.Response);
        Assert.NotNull(item.AnsweredAt);

        item.Close();
        Assert.Equal(PqrsStatus.Closed, item.Status);
    }

    [Fact]
    public void APqrsCaseCannotCloseWithoutAnAnswerOrAnswerTwice()
    {
        var item = NewCase();

        Assert.Throws<InvalidPqrsException>(() => item.Close());
        Assert.Throws<InvalidPqrsException>(() => item.Answer("", Now));
        Assert.Equal(PqrsStatus.Open, item.Status);

        item.Answer("Respuesta", Now);
        Assert.Throws<InvalidPqrsException>(() => item.Answer("Otra", Now));
        Assert.Throws<InvalidPqrsException>(() => item.Start());
    }

    [Fact]
    public void AnOverduePqrsCaseIsOnlyOverdueBeforeItIsAnswered()
    {
        var item = NewCase();
        var later = item.DueDate.AddDays(1);

        Assert.True(item.IsOverdue(later));

        item.Answer("Respuesta", Now);
        Assert.False(item.IsOverdue(later));
    }

    [Fact]
    public void ACampaignIsCreatedAsADraftAndExposesItsSegments()
    {
        var campaign = NewCampaign();

        Assert.Equal(CampaignStatus.Draft, campaign.Status);
        Assert.Equal(["A", "B", "C", "Todos"], Campaign.Segments);
        Assert.False(campaign.IsCurrentlyActive(Today));
    }

    [Fact]
    public void ACampaignCanOnlyBeEditedWhileItIsADraft()
    {
        var campaign = NewCampaign();

        campaign.UpdateDetails("Nueva campaña", "Descripción", CampaignChannel.Sms,
            "A", Today.AddDays(1), Today.AddDays(10));

        Assert.Equal("Nueva campaña", campaign.Name);
        Assert.Equal(CampaignChannel.Sms, campaign.Channel);
        Assert.Equal("A", campaign.Segment);

        campaign.Activate(Today);
        Assert.Throws<InvalidCampaignException>(() => campaign.UpdateDetails(
            "Otro nombre", "", CampaignChannel.Email, "B", Today, Today.AddDays(5)));
    }

    [Fact]
    public void ACampaignFollowsTheActivePausedClosedWorkflow()
    {
        var campaign = NewCampaign();

        campaign.Activate(Today);
        Assert.Equal(CampaignStatus.Active, campaign.Status);
        Assert.True(campaign.IsCurrentlyActive(Today));

        campaign.Pause();
        Assert.Equal(CampaignStatus.Paused, campaign.Status);
        Assert.False(campaign.IsCurrentlyActive(Today));

        campaign.Resume();
        Assert.Equal(CampaignStatus.Active, campaign.Status);

        campaign.Close();
        Assert.Equal(CampaignStatus.Closed, campaign.Status);
        Assert.Throws<InvalidCampaignException>(() => campaign.Resume());
        Assert.Throws<InvalidCampaignException>(() => campaign.Close());
    }

    [Fact]
    public void AnExpiredCampaignCannotBeActivated()
    {
        var campaign = new Campaign(
            0, "Vencida", "", CampaignChannel.Email, "Todos",
            Today.AddDays(-10), Today.AddDays(-1), Now);

        Assert.Throws<InvalidCampaignException>(() => campaign.Activate(Today));
    }

    [Theory]
    [InlineData("", "Todos", 1, 2)]
    [InlineData("Campaña", "Z", 1, 2)]
    [InlineData("Campaña", "Todos", 2, 2)]
    public void AnInvalidCampaignIsRejected(string name, string segment, int start, int end)
    {
        Assert.Throws<InvalidCampaignException>(() => new Campaign(
            0, name, "", CampaignChannel.Email, segment,
            Today.AddDays(start), Today.AddDays(end), Now));
    }

    private static PqrsCase NewCase() => new(
        1, 7, 1, PqrsType.Complaint, "Entrega tardía", "Llegó tarde",
        Today, Today.AddDays(20), Now);

    private static Campaign NewCampaign() => new(
        1, "Bienvenida", "Campaña de prueba", CampaignChannel.Email, "Todos",
        Today, Today.AddDays(30), Now);
}
