namespace TiendaFlow.Application;

public record CustomerRow(
    int Id,
    string DocumentNumber,
    string FullName,
    string? Email,
    string? Phone,
    string? Address,
    bool Active)
{
    public string Contact => string.Join(
        " · ",
        new[] { Email, Phone }.Where(value => !string.IsNullOrWhiteSpace(value)));
}
