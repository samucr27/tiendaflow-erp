namespace TiendaFlow.Domain;

/// <summary>
/// Employment record behind an application user. The employee supplies the
/// display name and the store a user works at.
/// </summary>
public class Employee
{
    public Employee(
        int id,
        int storeId,
        string idNumber,
        string name,
        string position,
        string contractType,
        decimal salary,
        bool active = true)
    {
        if (id < 0)
            throw new ArgumentOutOfRangeException(nameof(id), "Employee id cannot be negative.");
        if (storeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(storeId), "Store id must be positive.");
        RequireText(idNumber, "Id number");
        RequireText(name, "Employee name");
        RequireText(position, "Position");
        RequireText(contractType, "Contract type");
        if (salary < 0)
            throw new ArgumentOutOfRangeException(nameof(salary), "Salary cannot be negative.");

        Id = id;
        StoreId = storeId;
        IdNumber = idNumber;
        Name = name;
        Position = position;
        ContractType = contractType;
        Salary = salary;
        Active = active;
    }

    public int Id { get; }

    public int StoreId { get; private set; }

    public string IdNumber { get; private set; }

    public string Name { get; private set; }

    public string Position { get; private set; }

    public string ContractType { get; private set; }

    public decimal Salary { get; private set; }

    public bool Active { get; private set; }

    public void Update(
        int storeId,
        string idNumber,
        string name,
        string position,
        string contractType,
        decimal salary)
    {
        if (storeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(storeId), "Store id must be positive.");
        RequireText(idNumber, "Id number");
        RequireText(name, "Employee name");
        RequireText(position, "Position");
        RequireText(contractType, "Contract type");
        if (salary < 0)
            throw new ArgumentOutOfRangeException(nameof(salary), "Salary cannot be negative.");

        StoreId = storeId;
        IdNumber = idNumber;
        Name = name;
        Position = position;
        ContractType = contractType;
        Salary = salary;
    }

    public void Deactivate() => Active = false;

    public void Activate() => Active = true;

    private static void RequireText(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{field} is required.", nameof(value));
    }
}
