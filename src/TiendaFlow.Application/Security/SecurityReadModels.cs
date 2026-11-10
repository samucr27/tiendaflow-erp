namespace TiendaFlow.Application.Security;

public record UserListItem(
    int Id,
    string Email,
    string FullName,
    string IdNumber,
    int? StoreId,
    string StoreName,
    int RoleId,
    string RoleName,
    bool Active)
{
    public string RoleLabel => SystemRoles.Label(RoleName);
}

public record UserDetail(
    int Id,
    string Email,
    string FullName,
    string IdNumber,
    string Position,
    int? StoreId,
    string StoreName,
    int RoleId,
    string RoleName,
    bool Active)
{
    public string RoleLabel => SystemRoles.Label(RoleName);
}

public interface IUserDirectoryQuery
{
    IReadOnlyList<UserListItem> List();

    UserDetail? Get(int id);
}

public record AuditTrailRow(
    long Id,
    string Action,
    string Module,
    string EntityId,
    string Detail,
    int? UserId,
    string? UserName,
    string? Ip,
    DateTime Date);

public interface IAuditTrailQuery
{
    IReadOnlyList<AuditTrailRow> Recent(int take);
}

public record RoleMatrix(
    IReadOnlyList<string> Roles,
    IReadOnlyList<RoleMatrixRow> Rows);

public record RoleMatrixRow(
    string Permission,
    IReadOnlyDictionary<string, bool> AllowedByRole);
