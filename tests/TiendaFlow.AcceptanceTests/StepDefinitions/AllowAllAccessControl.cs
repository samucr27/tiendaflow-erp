using TiendaFlow.Application.Security;

namespace TiendaFlow.AcceptanceTests.StepDefinitions;

/// <summary>
/// The acceptance scenarios describe catalog and stock behavior, not security,
/// so every service call is allowed.
/// </summary>
internal sealed class AllowAllAccessControl : IAccessControl
{
    public bool Has(string permission) => true;

    public void Demand(string permission)
    {
    }
}
