using Moq;
using TiendaFlow.Application.Payroll;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class PayrollRunServiceTests
{
    private static readonly PayrollParameters TestParameters = new()
    {
        MinimumWage = 1_000_000m,
        TransportSubsidy = 100_000m,
        Uvt = 50_000m
    };

    private readonly InMemoryEmployeeRepository _employees = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly FakeCurrentUser _user = new(userId: 9);

    private static IPayrollEngine CreateEngine() =>
        new PayrollEngine(
            new DefaultPayrollParametersProvider(TestParameters),
            new IPayrollStrategy[]
            {
                new EmploymentPayrollStrategy(new WithholdingCalculator()),
                new ServiceProvisionPayrollStrategy()
            });

    private PayrollRunService CreateService(
        IPayrollEngine? engine = null,
        IAccessControl? access = null) =>
        new(
            _employees,
            engine ?? CreateEngine(),
            access ?? new AllowAllAccessControl(),
            _audit,
            _user);

    private Employee AddEmployee(
        string name,
        string contract,
        decimal salary,
        bool active = true,
        string? idNumber = null)
    {
        var employee = new Employee(
            0,
            1,
            idNumber ?? $"{_employees.List().Count + 1}00",
            name,
            "Cajero",
            contract,
            salary,
            active);

        _employees.Add(employee);
        return employee;
    }

    [Fact]
    public void Calculate_IgnoresInactiveEmployees()
    {
        var active = AddEmployee("Ana", "Indefinido", 1_000_000m);
        AddEmployee("Beto", "Indefinido", 1_000_000m, active: false);

        var run = CreateService().Calculate(30, 1);

        var line = Assert.Single(run.Lines);
        Assert.Equal(active.Id, line.EmployeeId);
    }

    [Fact]
    public void Calculate_MixesValidEmployeesWithMissingSalaryAndUnknownContract()
    {
        var valid = AddEmployee("Ana", "Indefinido", 1_000_000m);
        var validService = AddEmployee("Beto", "Prestación de servicios", 5_000_000m);
        var missingSalary = AddEmployee("Carla", "Indefinido", 0m);
        var unknownContract = AddEmployee("Dora", "Contrato raro", 2_000_000m);

        var run = CreateService().Calculate(30, 1);

        Assert.Equal(4, run.Lines.Count);
        Assert.Equal(2, run.CalculatedCount);
        Assert.Equal(2, run.ProblemCount);

        var validLine = run.Lines.Single(line => line.EmployeeId == valid.Id);
        Assert.NotNull(validLine.Result);
        Assert.Equal("Indefinido", validLine.ContractLabel);
        Assert.Equal(1_100_000m, validLine.Result!.GrossEarnings);

        var serviceLine = run.Lines.Single(line => line.EmployeeId == validService.Id);
        Assert.NotNull(serviceLine.Result);
        Assert.Equal("Prestación de servicios", serviceLine.ContractLabel);

        var missingLine = run.Lines.Single(line => line.EmployeeId == missingSalary.Id);
        Assert.Null(missingLine.Result);
        Assert.Contains("salario", missingLine.Problem);

        var unknownLine = run.Lines.Single(line => line.EmployeeId == unknownContract.Id);
        Assert.Null(unknownLine.Result);
        Assert.Contains("contrato", unknownLine.Problem);

        Assert.Equal(6_100_000m, run.TotalGrossEarnings);
        Assert.Equal(280_000m, run.TotalDeductions);
        Assert.Equal(5_820_000m, run.TotalNetPay);
        Assert.Equal(6_501_180m, run.TotalEmployerCost);
    }

    [Fact]
    public void Calculate_WithNoActiveEmployees_ReturnsEmptyTotals()
    {
        AddEmployee("Ana", "Indefinido", 1_000_000m, active: false);

        var run = CreateService().Calculate(30, 1);

        Assert.Empty(run.Lines);
        Assert.Equal(0m, run.TotalGrossEarnings);
        Assert.Equal(0m, run.TotalDeductions);
        Assert.Equal(0m, run.TotalNetPay);
        Assert.Equal(0m, run.TotalEmployerCost);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(-1)]
    public void Calculate_WithInvalidDays_ThrowsAndDoesNotAudit(int daysWorked)
    {
        AddEmployee("Ana", "Indefinido", 1_000_000m);

        Assert.Throws<InvalidPayrollInputException>(
            () => CreateService().Calculate(daysWorked, 1));

        Assert.Empty(_audit.Entries);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Calculate_WithInvalidRiskClass_Throws(int riskClass)
    {
        AddEmployee("Ana", "Indefinido", 1_000_000m);

        Assert.Throws<InvalidPayrollInputException>(
            () => CreateService().Calculate(30, riskClass));
    }

    [Fact]
    public void Calculate_AuditsOnlyTheEmployeeCountAndTheDays()
    {
        AddEmployee("Ana María", "Indefinido", 1_000_000m);
        AddEmployee("Beto", "Indefinido", 2_000_000m);

        CreateService().Calculate(30, 1);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("PayrollCalculated", entry.Action);
        Assert.Equal("hr", entry.Module);
        Assert.Equal(9, entry.UserId);
        Assert.Contains("2 empleados", entry.Detail);
        Assert.Contains("30 días", entry.Detail);
        Assert.DoesNotContain("Ana", entry.Detail);
        Assert.DoesNotContain("1.000.000", entry.Detail);
    }

    [Fact]
    public void Calculate_WhenTheEngineRejectsAnEmployee_FlagsTheLineAndKeepsTheRun()
    {
        var valid = AddEmployee("Ana", "Indefinido", 1_000_000m);
        var invalid = AddEmployee("Beto", "Indefinido", 1_000_000m);
        var unsupported = AddEmployee("Carla", "Indefinido", 1_000_000m);

        var engine = new Mock<IPayrollEngine>();
        engine.Setup(e => e.Calculate(It.Is<PayrollInput>(input => input.EmployeeId == valid.Id)))
            .Returns(new PayrollResult { EmployeeId = valid.Id, GrossEarnings = 100m });
        engine.Setup(e => e.Calculate(It.Is<PayrollInput>(input => input.EmployeeId == invalid.Id)))
            .Throws(new InvalidPayrollInputException("bad input"));
        engine.Setup(e => e.Calculate(It.Is<PayrollInput>(input => input.EmployeeId == unsupported.Id)))
            .Throws(new UnsupportedContractTypeException(ContractType.Indefinite));

        var run = CreateService(engine.Object).Calculate(30, 1);

        Assert.Equal(3, run.Lines.Count);
        Assert.NotNull(run.Lines.Single(line => line.EmployeeId == valid.Id).Result);

        var invalidLine = run.Lines.Single(line => line.EmployeeId == invalid.Id);
        Assert.Null(invalidLine.Result);
        Assert.Contains("datos", invalidLine.Problem);

        var unsupportedLine = run.Lines.Single(line => line.EmployeeId == unsupported.Id);
        Assert.Null(unsupportedLine.Result);
        Assert.Contains("regla", unsupportedLine.Problem);
    }

    [Fact]
    public void Calculate_WithoutPermission_ThrowsAndAuditsTheDenial()
    {
        var service = CreateService(access: new AccessControl(
            new FakeCurrentUser(userId: 9, permissions: new HashSet<string>()),
            _audit));

        var exception = Assert.Throws<AccessDeniedException>(() => service.Calculate(30, 1));

        Assert.Equal(Permissions.HrPayrollManage, exception.Permission);
        Assert.Equal("AccessDenied", Assert.Single(_audit.Entries).Action);
    }
}
