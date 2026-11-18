using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class EmploymentContractTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc);

    private static EmploymentContract Create(
        ContractType type = ContractType.Indefinite,
        DateOnly? start = null,
        DateOnly? end = null,
        decimal salary = 2_000_000m,
        int employeeId = 1) =>
        new(
            id: 0,
            employeeId,
            type,
            start ?? new DateOnly(2026, 1, 1),
            end,
            salary,
            Now);

    [Fact]
    public void AnIndefiniteContractMayOmitTheEndDate()
    {
        var contract = Create();

        Assert.Null(contract.EndDate);
        Assert.Equal(EmploymentContractStatus.Active, contract.Status);
        Assert.Equal(Now, contract.CreatedAt);
    }

    [Theory]
    [InlineData(ContractType.FixedTerm)]
    [InlineData(ContractType.WorkOrService)]
    [InlineData(ContractType.ServiceProvision)]
    public void ATimeBoundContractRequiresAnEndDate(ContractType type)
    {
        Assert.Throws<InvalidEmploymentContractException>(() => Create(type));
    }

    [Fact]
    public void TheEndDateCannotBeBeforeTheStartDate()
    {
        Assert.Throws<InvalidEmploymentContractException>(() => Create(
            ContractType.FixedTerm,
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 4, 30)));
    }

    [Theory]
    [InlineData(0, 2_000_000)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void TheEmployeeAndTheSalaryMustBeValid(int employeeId, decimal salary)
    {
        Assert.Throws<InvalidEmploymentContractException>(() => Create(
            salary: salary,
            employeeId: employeeId));
    }

    [Fact]
    public void AnUnknownContractTypeIsRejected()
    {
        Assert.Throws<InvalidEmploymentContractException>(() => Create(
            (ContractType)99,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1)));
    }

    [Fact]
    public void RenewCreatesANewActiveContractLinkedToThePreviousOne()
    {
        var contract = Create(ContractType.FixedTerm, end: new DateOnly(2026, 6, 30));
        TestIds.Assign(contract, 7);

        var renewed = contract.Renew(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 12, 31),
            2_300_000m,
            Now);

        Assert.Equal(7, renewed.PreviousContractId);
        Assert.Equal(EmploymentContractStatus.Active, renewed.Status);
        Assert.Equal(new DateOnly(2026, 7, 1), renewed.StartDate);
        Assert.Equal(new DateOnly(2026, 12, 31), renewed.EndDate);
        Assert.Equal(2_300_000m, renewed.BaseSalary);
        Assert.Equal(EmploymentContractStatus.Active, contract.Status);
    }

    [Fact]
    public void ARenewalCannotStartOnOrBeforeTheCurrentStart()
    {
        var contract = Create(ContractType.FixedTerm, end: new DateOnly(2026, 6, 30));

        Assert.Throws<InvalidEmploymentContractException>(() => contract.Renew(
            new DateOnly(2026, 1, 1),
            null,
            2_000_000m,
            Now));
    }

    [Fact]
    public void ARenewalCannotStartBeforeTheCurrentEnd()
    {
        var contract = Create(ContractType.FixedTerm, end: new DateOnly(2026, 6, 30));

        Assert.Throws<InvalidEmploymentContractException>(() => contract.Renew(
            new DateOnly(2026, 6, 30),
            null,
            2_000_000m,
            Now));
    }

    [Fact]
    public void OnlyAnActiveContractCanBeExtendedRenewedOrTerminated()
    {
        var contract = Create();
        contract.MarkExtended();

        Assert.Equal(EmploymentContractStatus.Extended, contract.Status);
        Assert.Throws<InvalidEmploymentContractException>(() => contract.MarkExtended());
        Assert.Throws<InvalidEmploymentContractException>(() => contract.Terminate(Now));
        Assert.Throws<InvalidEmploymentContractException>(() => contract.Renew(
            new DateOnly(2027, 1, 1),
            null,
            2_000_000m,
            Now));
    }

    [Fact]
    public void TerminateRecordsTheStatusAndTheUtcInstant()
    {
        var contract = Create();

        contract.Terminate(new DateTime(2026, 11, 16, 20, 30, 0, DateTimeKind.Local));

        Assert.Equal(EmploymentContractStatus.Terminated, contract.Status);
        Assert.Equal(DateTimeKind.Utc, contract.TerminatedAt!.Value.Kind);
    }

    [Fact]
    public void IsExpiringOnlyLooksAtActiveContractsInsideTheWindow()
    {
        var today = new DateOnly(2026, 11, 16);
        var inside = Create(
            ContractType.FixedTerm,
            end: today.AddDays(10));
        var boundary = Create(
            ContractType.FixedTerm,
            end: today.AddDays(30));
        var outside = Create(
            ContractType.FixedTerm,
            end: today.AddDays(31));
        var expired = Create(
            ContractType.FixedTerm,
            start: today.AddDays(-90),
            end: today.AddDays(-1));

        Assert.True(inside.IsExpiring(today, 30));
        Assert.True(boundary.IsExpiring(today, 30));
        Assert.False(outside.IsExpiring(today, 30));
        Assert.False(expired.IsExpiring(today, 30));
        Assert.False(Create().IsExpiring(today, 30));
    }

    [Theory]
    [InlineData("contrato.pdf", "application/pdf")]
    [InlineData("contrato.PNG", "image/png")]
    [InlineData("contrato.jpeg", "image/jpeg")]
    public void AttachmentsAcceptPdfPngAndJpg(string fileName, string contentType)
    {
        var attachment = new ContractAttachment(
            id: 0,
            contractId: 1,
            fileName,
            contentType,
            new byte[] { 1, 2, 3 },
            uploadedByUserId: 5,
            Now);

        Assert.Equal(3, attachment.SizeBytes);
        Assert.Equal(contentType, attachment.ContentType);
    }

    [Theory]
    [InlineData(@"C:\contratos\..\..\secreto.pdf", "secreto.pdf")]
    [InlineData("/contratos/../../secreto.pdf", "secreto.pdf")]
    [InlineData("contratos/secreto.pdf", "secreto.pdf")]
    [InlineData("  secreto.pdf  ", "secreto.pdf")]
    public void AnAttachmentNameIsReducedToTheFileName(string fileName, string expected)
    {
        var attachment = new ContractAttachment(
            0,
            1,
            fileName,
            "application/pdf",
            new byte[] { 1 },
            5,
            Now);

        Assert.Equal(expected, attachment.FileName);
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData(@"C:\contratos\..")]
    [InlineData("/contratos/")]
    [InlineData("   ")]
    public void AttachmentsRejectNamesWithoutAFinalFileName(string fileName)
    {
        Assert.Throws<InvalidContractAttachmentException>(() => new ContractAttachment(
            0,
            1,
            fileName,
            "application/pdf",
            new byte[] { 1 },
            5,
            Now));
    }

    [Theory]
    [InlineData("contrato.exe", "application/pdf")]
    [InlineData("contrato.pdf", "text/plain")]
    [InlineData("contrato.pdf", "image/gif")]
    public void AttachmentsRejectUnsupportedFiles(string fileName, string contentType)
    {
        Assert.Throws<InvalidContractAttachmentException>(() => new ContractAttachment(
            0,
            1,
            fileName,
            contentType,
            new byte[] { 1 },
            5,
            Now));
    }

    [Fact]
    public void AttachmentsRejectEmptyAndOversizedFiles()
    {
        Assert.Throws<InvalidContractAttachmentException>(() => new ContractAttachment(
            0,
            1,
            "contrato.pdf",
            "application/pdf",
            Array.Empty<byte>(),
            5,
            Now));

        Assert.Throws<InvalidContractAttachmentException>(() => new ContractAttachment(
            0,
            1,
            "contrato.pdf",
            "application/pdf",
            new byte[ContractAttachment.MaxSizeBytes + 1],
            5,
            Now));
    }

    [Fact]
    public void AnAttachmentRequiresAContractAndAFileName()
    {
        Assert.Throws<InvalidContractAttachmentException>(() => new ContractAttachment(
            0,
            0,
            "contrato.pdf",
            "application/pdf",
            new byte[] { 1 },
            5,
            Now));

        Assert.Throws<InvalidContractAttachmentException>(() => new ContractAttachment(
            0,
            1,
            " ",
            "application/pdf",
            new byte[] { 1 },
            5,
            Now));
    }
}
