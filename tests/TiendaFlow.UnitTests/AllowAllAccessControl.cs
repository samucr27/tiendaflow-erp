using TiendaFlow.Application.Security;

namespace TiendaFlow.UnitTests;

/// <summary>
/// Grants every permission for tests that focus on business rules rather than
/// authorization. Security tests use their own restricted implementation.
/// </summary>
internal sealed class AllowAllAccessControl : IAccessControl
{
    public bool Has(string permission) => true;

    public void Demand(string permission)
    {
    }
}
