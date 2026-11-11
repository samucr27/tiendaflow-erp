# 7. Deployment View

## 7.1 Infrastructure level 1

| Node | Technology | Content |
|---|---|---|
| Application server | .NET 9, Kestrel (`http://localhost:5212` in development) | Blazor Web App `TiendaFlow.Web` (interactive server render mode): UI, Minimal API endpoints, cookie authentication |
| Database server | SQL Server 2022 in a Docker container (`tiendaflow-sql`, port 1433) | Database `TiendaFlow_OpenCode`, created from EF Core migrations |
| Continuous integration | GitHub Actions (`.github/workflows/ci.yml`, .NET 9.0.x) | Restore, build and test with XPlat Code Coverage on every push |
| Infrastructure as code | Terraform with remote state in HCP Terraform (`.github/workflows/terraform-plan.yml`, `infra/terraform/main.tf`) | `terraform init`, `validate` and `plan` on pull requests to `develop`; the plan ran green against the remote backend. The current resource is a placeholder and there was no `apply` |

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
4. The infrastructure automation (Terraform with remote state) runs `init`,
   `validate` and `plan` on pull requests to `develop`; the plan was verified in
   green. There is no `apply` yet, so no real infrastructure is created.

## 7.4 Continuous integration

| Step | Command or action | Expected result |
|---|---|---|
| Checkout | `actions/checkout@v4` | Branch code |
| SDK | `actions/setup-dotnet@v4` with .NET 9.0.x | SDK available |
| Restore | `dotnet restore` | Packages restored |
| Build | `dotnet build --no-restore` | 0 warnings and 0 errors |
| Test | `dotnet test --no-build --collect:"XPlat Code Coverage"` | Unit and acceptance tests; integration tests skipped without SQL Server |

## 7.5 Infrastructure as code

- `infra/terraform/main.tf` declares remote state in organization `Tienda-Flow`,
  workspace `tiendaflow-infra`.
- The `random` provider and the `random_pet` resource demonstrate the remote
  state flow and the automatic plan without needing cloud credentials.
- The Terraform workflow runs only on pull requests to `develop` and only when
  `.tf` files change.
- The HCP Terraform token comes from the `TF_API_TOKEN` secret; it is never
  written to the repository.
- The `Terraform Plan` workflow ran successfully in the pull request to
  `develop` (job `plan`, about 20 seconds) against the remote state. It only
  planned the example `random_pet`; there was no `apply` and no real
  infrastructure was created.
- When the real deployment target is chosen, the example resource is replaced by
  the database and the application hosting.

## 7.6 Limitations and operational notes

- The current deployment is **local development**: there is no automated staging
  or production environment.
- Terraform is limited to `plan`: the workspace and remote state work, but no
  `apply` has run and the repository does not define real ERP infrastructure yet.
- The development database is recreated by the seeder; salaries return to `0`
  and the payroll run must be recalculated.
- The payroll run is not persisted; there are no run or detail tables in this
  delivery.
- The `tiendaflow-sql` container is the only environment used by the demo. Do
  not delete volumes or containers from other environments.