# TiendaFlow: ERP System for Convenience-Store Chains

Semester project of CADI: Software Engineering II, University of Cundinamarca (Instructor: Carlos Eduardo Mujica Reyes).

TiendaFlow is a modular web ERP for convenience-store chains built with Microsoft technologies (ASP.NET Core, Blazor and SQL Server 2022). It integrates six modules: Inventory, Sales and Billing, Supplier and Purchasing, Human Resources, Customer Management (CRM), and Security and Reporting.

## Documentation

1. Documentation page: https://samucr27.github.io/tiendaflow-erp/
2. Complete project document: [TiendaFlow_Project_Definition_Sprint1.pdf](TiendaFlow_Project_Definition_Sprint1.pdf)
3. Figma design file and interactive prototype: https://www.figma.com/design/3LW57RjiNETxaukic2S6JE
4. Prototype generated with Figma Make: https://early-hope-36544320.figma.site/
5. Google Colab notebook with the PlantUML diagrams: https://colab.research.google.com/drive/1VtObCkwC1xnCBrrpza1oSu0oOpki-RqI?usp=sharing
6. PlantUML sources: [docs/uml](docs/uml)

## Team

1. Sergio Steeven Moreno Forero: Project Manager and Scrum Master
2. Samuel Castellanos Rivera: Software Architect and Backend Developer
3. Juan Sebastian Rodriguez Blanco: Frontend Developer and UI/UX Designer
4. Jonathan Alejandro Yacuma Rivera: Database Administrator and QA Tester

## Requisitos

- SDK de .NET 9.
- Docker (Docker Desktop en Windows) para el contenedor de SQL Server 2022.
- PowerShell 5.1 o superior para ejecutar los comandos tal como están escritos.

## Puesta en marcha

### 1. Base de datos

Levanta SQL Server 2022 en un contenedor llamado `tiendaflow-sql` y publica el puerto 1433. Reemplaza `<tu-contraseña>` por una contraseña propia que cumpla la política de SQL Server (mayúsculas, minúsculas, dígitos y símbolos):

```powershell
docker run -d --name tiendaflow-sql `
  -e "ACCEPT_EULA=Y" `
  -e "MSSQL_SA_PASSWORD=<tu-contraseña>" `
  -e "MSSQL_PID=Developer" `
  -p 1433:1433 `
  mcr.microsoft.com/mssql/server:2022-latest
```

Verifica que el contenedor esté en ejecución:

```powershell
docker ps --filter "name=tiendaflow-sql"
```

La primera ejecución de la aplicación en el entorno `Development` aplica las migraciones de EF Core y crea la base `TiendaFlow_OpenCode` con la tienda, los roles, los usuarios demo y los datos iniciales (4 productos con stock, 3 proveedores, 2 facturas y 3 clientes). Ese sembrado solo se ejecuta si cada tabla está vacía; no se repite al reiniciar.

### 2. Secretos de desarrollo

La aplicación no contiene contraseñas ni cadenas de conexión en el repositorio. Se configuran con `dotnet user-secrets` en el proyecto Web:

| Nombre | Contenido |
|---|---|
| `ConnectionStrings:DefaultConnection` | Cadena de conexión a SQL Server |
| `Seed:AdminPassword` | Contraseña del usuario administrador demo |
| `Seed:DemoPassword` | Contraseña de los demás usuarios demo |

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" `
  "Server=localhost,1433;Database=TiendaFlow_OpenCode;User Id=sa;Password=<tu-contraseña>;Encrypt=False;TrustServerCertificate=True;" `
  --project src\TiendaFlow.Web
dotnet user-secrets set "Seed:AdminPassword" "<contraseña-admin>" --project src\TiendaFlow.Web
dotnet user-secrets set "Seed:DemoPassword" "<contraseña-demo>" --project src\TiendaFlow.Web
```

No escribas estos valores en archivos del repo ni en `appsettings*.json`.

### 3. Ejecutar la aplicación

```powershell
dotnet run --project src\TiendaFlow.Web --launch-profile http
```

Abre `http://localhost:5212`. La aplicación redirige a `/login`. El perfil `https` también está disponible en `https://localhost:7275`.

### 4. Usuarios demo

Todos usan el dominio `@tiendaflow.local`. Las contraseñas no se documentan aquí: están en los secretos indicados arriba.

| Rol | Correo | Secreto de contraseña |
|---|---|---|
| Administrador | `admin@tiendaflow.local` | `Seed:AdminPassword` |
| Gerente corporativo | `gerente@tiendaflow.local` | `Seed:DemoPassword` |
| Jefe de tienda | `jefe.tienda@tiendaflow.local` | `Seed:DemoPassword` |
| Cajero | `cajero@tiendaflow.local` | `Seed:DemoPassword` |
| Auxiliar de bodega | `bodega@tiendaflow.local` | `Seed:DemoPassword` |
| Coordinador de logística | `logistica@tiendaflow.local` | `Seed:DemoPassword` |
| Gestor de recursos humanos | `rrhh@tiendaflow.local` | `Seed:DemoPassword` |

## Rutas principales

| Módulo | Rutas | Permiso de lectura | Permiso de escritura |
|---|---|---|---|
| Inventario | `/inventario`, `/inventario/productos`, `/inventario/alertas` | `inventory.stock.view`, `inventory.products.view` | `inventory.products.manage`, `inventory.stock.*` |
| Reportes | `/reportes/inventario`, `/reportes/ventas`, `/reportes/nomina` | `inventory.reports.view`, `sales.reports.view`, `hr.payroll.manage` | — |
| Nómina y empleados | `/rrhh/empleados`, `/rrhh/nomina` | `hr.employees.view`, `hr.payroll.manage` | `hr.employees.manage`, `hr.payroll.manage` |
| Proveedores | `/proveedores`, `/proveedores/nuevo`, `/proveedores/{id}`, `/proveedores/ranking` | `purchasing.suppliers.view`, `purchasing.reports.view` | `purchasing.suppliers.manage`, `purchasing.evaluations.manage` |
| Compras | `/compras/ordenes`, `/compras/ordenes/nueva`, `/compras/ordenes/{id}` | `purchasing.orders.view` | `purchasing.orders.*` |
| Facturación | `/facturacion`, `/facturacion/nueva`, `/facturacion/{id}`, `/facturacion/{id}/xml` | `sales.invoices.view` | `sales.invoices.manage` |
| CRM clientes | `/crm/clientes`, `/crm/clientes/nuevo`, `/crm/clientes/{id}`, `/crm/segmentacion` | `crm.customers.view`, `crm.reports.view` | `crm.customers.manage` |
| Seguridad | `/seguridad/usuarios`, `/seguridad/roles`, `/seguridad/auditoria` | `security.*.view` | `security.*.manage` |

El detalle de qué rol tiene cada permiso está en `docs/arc42/10-architecture-concepts-security.md` y en `src/TiendaFlow.Application/Security/RolePermissions.cs`.

## Migraciones de EF Core

La aplicación aplica las migraciones pendientes al arrancar en `Development`. Para generarlas o aplicarlas manualmente:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'

# Nueva migración (ejemplo)
dotnet ef migrations add NombreDeLaFase `
  --project src\TiendaFlow.Infrastructure `
  --startup-project src\TiendaFlow.Web `
  --output-dir Persistence\Migrations

# Aplicar todas las migraciones pendientes
dotnet ef database update `
  --project src\TiendaFlow.Infrastructure `
  --startup-project src\TiendaFlow.Web
```

Las migraciones de esta entrega son `20261109011701_Phase3Purchasing` (proveedores y órdenes), `20261111005733_Phase4Invoicing` (facturación) y `20261111010125_Phase5Crm` (clientes).

## Pruebas y cobertura

```powershell
dotnet build TiendaFlow.slnx
dotnet test TiendaFlow.slnx
```

La cobertura se recoge con coverlet y se resume con ReportGenerator:

```powershell
dotnet test TiendaFlow.slnx --collect:"XPlat Code Coverage"
reportgenerator -reports:"tests\**\TestResults\**\coverage.cobertura.xml" `
  -targetdir:coveragereport -reporttypes:TextSummary `
  "-assemblyfilters:+TiendaFlow.Application;+TiendaFlow.Domain"
```

Si no tienes `reportgenerator`, instálalo con `dotnet tool install -g dotnet-reportgenerator-globaltool`. La carpeta `coveragereport/` está ignorada por Git. Las pruebas de integración se omiten salvo que definas la variable de entorno `TIENDAFLOW_TEST_CONNECTION` (o `ConnectionStrings__DefaultConnection`) con una cadena hacia un SQL Server accesible.

## Infraestructura como código

`infra/terraform/main.tf` crea un App Service Linux B1, Azure SQL Basic, las
reglas de firewall y un sufijo aleatorio para los nombres, usando estado remoto
del workspace `Tienda-Flow/tiendaflow-infra` en modo local. El flujo
`.github/workflows/terraform-plan.yml` es solo de validación (`fmt -check`,
`init -backend=false` y `validate`), porque GitHub no tiene credenciales de
Azure. El `apply` se ejecuta en tu máquina con `az login`; el despliegue manual
con `dotnet publish`, zip y `az webapp deploy` está descrito en
[docs/final/deploy-azure.md](docs/final/deploy-azure.md). La vía secundaria de
GitHub usa el perfil de publicación como secreto `AZURE_WEBAPP_PUBLISH_PROFILE`.

## Estructura del repositorio

```text
src/
  TiendaFlow.Domain/          Entidades, reglas y nombres de rol
  TiendaFlow.Application/     Casos de uso, permisos y motor de nómina
  TiendaFlow.Infrastructure/  EF Core, repositorios y migraciones
  TiendaFlow.Web/             Blazor Web App, Minimal API y login
tests/
  TiendaFlow.UnitTests/       Pruebas unitarias y de servicios
  TiendaFlow.IntegrationTests/ Pruebas con SQL Server real
  TiendaFlow.AcceptanceTests/ Pruebas de aceptación con Reqnroll
docs/                         Documentación del proyecto
infra/                        Infraestructura como código (Terraform)
.github/workflows/            Integración continua
```

## Documentación

- [arc42](docs/arc42): vista de bloques, despliegue y conceptos de seguridad.
- [Final](docs/final): módulos implementados, permisos, pendientes, pruebas, base de datos y guía de Azure.
- [Notion](docs/notion): versión para Notion de los mismos documentos.
- [Sprint 2](docs/sprint2): planeación, guion de demo, guion del tour de arquitectura, flujo de Terraform, revisión de código, validación de parámetros de nómina, revisión de la UI de RR. HH., prueba de humo de autorización, retrospectiva y preguntas abiertas.
- [Diagramas UML](docs/uml): secuencias de la corrida de nómina.
- [Documento del proyecto](TiendaFlow_Project_Definition_Sprint1.pdf): definición del Sprint 1.

## Flujo de ramas

```text
feature/*  ->  develop  ->  main
```

El trabajo se hace en ramas `feature/*` y se integra a `develop` mediante pull request. `main` recibe solo versiones estables. No se hace push directo a `develop` ni a `main`.
