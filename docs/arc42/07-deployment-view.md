# 7. Deployment View

## 7.1 Infrastructure level 1

| Node | Technology | Content |
|---|---|---|
| Application server | .NET 9, Kestrel (`http://localhost:5212` in development) | Blazor Web App `TiendaFlow.Web` (interactive server render mode): UI, Minimal API endpoints, cookie authentication |
| Database server | SQL Server 2022 in a Docker container (`tiendaflow-sql`, port 1433) | Database `TiendaFlow_OpenCode`, created from EF Core migrations |
| Continuous integration | GitHub Actions (`.github/workflows/ci.yml`, .NET 9.0.x) | Restore, build and test with XPlat Code Coverage on every push |

## 7.2 Configuration and secrets

- The connection string and the demo user passwords live only in
  `dotnet user-secrets` (development). They are never committed.
- In Development, a hosted seeder creates the administrator and one demo user
  per role on an empty database.
- Integration tests run only when `TIENDAFLOW_TEST_CONNECTION` is set; on
  GitHub Actions (no SQL Server) they are skipped and unit and acceptance
  tests still run.

## 7.3 Deployment flow

1. Developer works on a feature branch and runs build and tests locally.
2. Push triggers CI: build and tests with coverage.
3. `dotnet ef database update` applies the migrations (one migration per phase).
4. The infrastructure automation (Terraform with remote state, plan on pull
   requests to `develop`) is the next step, see the Sprint 2 IaC task.