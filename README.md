# TiendaFlow: ERP System for Convenience-Store Chains

Semester project of CADI: Software Engineering II, University of Cundinamarca (Instructor: Carlos Eduardo Mujica Reyes).

TiendaFlow is a modular web ERP for convenience-store chains built with Microsoft technologies (ASP.NET Core, Blazor and SQL Server 2022). It integrates six modules: Inventory, Sales and Billing, Supplier and Purchasing, Human Resources, Customer Management (CRM), and Security and Reporting.

## Estado del proyecto

El proyecto terminó con el Sprint 2 (16 de noviembre de 2026); no hay Sprint 3. La versión final está en `main`
y en la etiqueta `sprint-2`.

| Módulo | Funciones |
|---|---|
| Inventario | CRUD de productos, stock por tienda, recepción, conteos, traslados, alertas de stock crítico (HU-05) |
| Ventas y facturación | Punto de venta con varios productos, facturas que mueven inventario, devoluciones (notas crédito), XML simulado de factura electrónica |
| Proveedores y compras | CRUD de proveedores, solicitudes de cotización con aprobación, órdenes de compra, ranking de proveedores, reposición automática ante stock crítico |
| Talento humano | CRUD de empleados, nómina mensual (HU-04) con corridas guardadas, contratos, asistencia, turnos |
| CRM | CRUD de clientes, segmentación A/B/C, puntos de fidelización, PQRS, campañas por segmento |
| Seguridad y reportes | Usuarios y roles (HU-06), matriz de roles editable, tiendas, parámetros, auditoría, reportes de inventario, ventas, nómina y seguridad |

La factura electrónica real ante la DIAN (firma certificada y envío) queda fuera del alcance: el sistema genera un
XML simulado sin validez fiscal.

## Documentation

1. Documentation page: https://samucr27.github.io/tiendaflow-erp/
2. Complete project document (Sprints 0 and 1): [TiendaFlow_Project_Definition_Sprint1.pdf](TiendaFlow_Project_Definition_Sprint1.pdf)
3. Final Sprint 2 document, organized by module: [TiendaFlow_Entrega_Final_Sprint2.pdf](TiendaFlow_Entrega_Final_Sprint2.pdf)
4. Architecture tour video (arc42 in Notion, Sprint 2 task 2): https://drive.google.com/file/d/1zUALZRwRKVcWQhH2NglT0QQUR7mrlyVc/view?usp=sharing
5. Application deployed on Azure App Service: https://app-tiendaflow-ba67bx.azurewebsites.net
6. Figma design file and interactive prototype: https://www.figma.com/design/3LW57RjiNETxaukic2S6JE
7. Prototype generated with Figma Make: https://early-hope-36544320.figma.site/
8. Google Colab notebook with the PlantUML diagrams: https://colab.research.google.com/drive/1VtObCkwC1xnCBrrpza1oSu0oOpki-RqI?usp=sharing
9. PlantUML sources: [docs/uml](docs/uml)

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

La primera ejecución de la aplicación en el entorno `Development` aplica las migraciones de EF Core y crea la base `TiendaFlow_OpenCode` con la tienda, los roles, los usuarios demo y datos de demostración de todos los módulos (productos con stock, proveedores, órdenes, facturas, clientes, empleados, contratos, asistencia, turnos, PQRS y campañas). Ese sembrado solo se ejecuta si cada tabla está vacía; no se repite al reiniciar.

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

La aplicación también lee la cadena de conexión de la variable de entorno
`DB_CONNECTION_STRING`, que tiene prioridad sobre `ConnectionStrings:DefaultConnection`.
Es la que se usa en Docker, Azure App Service y AWS Elastic Beanstalk.

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
| Inventario | `/inventario`, `/inventario/productos`, `/inventario/stock`, `/inventario/recibir`, `/inventario/conteos`, `/inventario/traslados`, `/inventario/alertas` | `inventory.stock.view`, `inventory.products.view`, `inventory.alerts.view` | `inventory.products.manage`, `inventory.stock.*` |
| Punto de venta | `/inventario/ventas` | `sales.pos.use` | `sales.pos.use` |
| Facturación | `/facturacion`, `/facturacion/nueva`, `/facturacion/{id}` (con devoluciones), `/facturacion/{id}/xml` | `sales.invoices.view` | `sales.invoices.manage`, `sales.returns.authorize` |
| Proveedores | `/proveedores`, `/proveedores/nuevo`, `/proveedores/{id}`, `/proveedores/ranking` | `purchasing.suppliers.view`, `purchasing.reports.view` | `purchasing.suppliers.manage`, `purchasing.evaluations.manage` |
| Compras | `/compras/cotizaciones`, `/compras/ordenes`, `/compras/ordenes/nueva`, `/compras/ordenes/{id}`, `/compras/reposicion` | `purchasing.orders.view` | `purchasing.orders.*` (aprobar cotizaciones: `purchasing.orders.approve`) |
| Talento humano | `/rrhh/empleados`, `/rrhh/nomina`, `/rrhh/contratos`, `/rrhh/asistencia`, `/rrhh/turnos` | `hr.employees.view` | `hr.employees.manage`, `hr.payroll.manage`, `hr.contracts.manage`, `hr.attendance.manage`, `hr.shifts.manage` |
| CRM | `/crm/clientes`, `/crm/clientes/nuevo`, `/crm/clientes/{id}`, `/crm/segmentacion`, `/crm/puntos`, `/crm/pqrs`, `/crm/campanas` | `crm.customers.view`, `crm.reports.view` | `crm.customers.manage`, `crm.points.manage`, `crm.pqrs.manage`, `crm.campaigns.manage` |
| Seguridad | `/seguridad/usuarios`, `/seguridad/roles`, `/seguridad/tiendas`, `/seguridad/parametros`, `/seguridad/auditoria` | `security.*.view` | `security.*.manage` |
| Reportes | `/reportes/inventario`, `/reportes/ventas`, `/reportes/nomina`, `/reportes/seguridad` | `inventory.reports.view`, `sales.reports.view`, `hr.payroll.manage`, `security.reports.view` | — |
| API | `POST /api/nomina/calculate` | — | `hr.payroll.manage` (sin sesión responde 401 y sin permiso 403) |

La matriz por defecto vive en `src/TiendaFlow.Application/Security/RolePermissions.cs`; el administrador puede
cambiar los permisos de cada rol en `/seguridad/roles` y los cambios aplican en el siguiente inicio de sesión.
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

Las migraciones van de `InitialCreate` a `Phase13Quotations`:

| Migración | Contenido |
|---|---|
| `20261107142012_InitialCreate` | Productos, stock y auditoría |
| `20261109010416_Phase2Security` | Tiendas, roles, usuarios y empleados |
| `20261109011701_Phase3Purchasing` | Proveedores y órdenes de compra |
| `20261111005733_Phase4Invoicing` | Facturación |
| `20261111010125_Phase5Crm` | Clientes |
| `20261113012709_Phase6Stores` | Índices de tiendas |
| `20261113013940_Phase7Parameters` | Parámetros versionados |
| `20261113015239_Phase8PurchasingKpis` | Fechas de la orden y relación producto · proveedor |
| `20261114035445_Phase9InventoryDocuments` | Conteos y traslados |
| `20261116144218_Phase10StockConcurrency` | Columna `rowversion` en `stock` |
| `20261116154129_Phase11PosSalesAndInvoiceStock` | Ventas del punto de venta y tienda de la factura |
| `20261117024530_Phase12CloseoutModules` | Devoluciones, roles editables, contratos, asistencia, turnos, corridas de nómina, puntos, PQRS y campañas |
| `20261126051045_Phase13Quotations` | Solicitudes de cotización |

Antes de publicar una versión nueva en Azure hay que aplicar las migraciones pendientes en Azure SQL
(ver [docs/final/deploy-azure.md](docs/final/deploy-azure.md)).

## Pruebas y cobertura

```powershell
dotnet build TiendaFlow.slnx
dotnet test TiendaFlow.slnx
```

Resultado de la versión final: 694 pruebas en verde (635 unitarias, 6 de aceptación con Reqnroll y 53 de
integración contra SQL Server 2022) y 95,5 % de cobertura de líneas en `Application` + `Domain`. El detalle está en
[docs/final/testing.md](docs/final/testing.md).

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

## Docker

El `Dockerfile` construye la imagen en dos etapas (SDK de .NET 9 para publicar,
runtime de ASP.NET Core para ejecutar) y la aplicación escucha en el puerto 8080.
`docker-compose.yml` levanta la aplicación junto con SQL Server 2022:

```powershell
Copy-Item .env.example .env   # y escribe contraseñas propias en .env
docker compose up --build
```

Abre `http://localhost:8080`. La primera vez se crean el esquema y los datos de
demostración. Para detenerlo: `docker compose down` (con `-v` borra también la base).

## AWS Elastic Beanstalk

`deploy/aws/build-beanstalk-package.ps1` genera `artifacts/tiendaflow-beanstalk.zip`
(publicación self-contained para Linux, `Procfile` y `.ebextensions`). La guía
completa, con la base en Amazon RDS for SQL Server, está en
[docs/final/deploy-aws.md](docs/final/deploy-aws.md).

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
deploy/aws/                   Paquete y configuración de AWS Elastic Beanstalk
.github/workflows/            Integración continua
```

## Documentación

- [arc42](docs/arc42): vista de bloques, despliegue y conceptos de seguridad.
- [Final](docs/final): módulos implementados, permisos, pendientes, pruebas, base de datos y guía de Azure.
- [Notion](docs/notion): versión para Notion de los mismos documentos.
- [Sprint 2](docs/sprint2): planeación, guion de demo, guion del tour de arquitectura, flujo de Terraform, revisión de código, validación de parámetros de nómina, revisión de la UI de RR. HH., prueba de humo de autorización, retrospectiva y preguntas abiertas.
- [Diagramas UML](docs/uml): casos de uso, árboles funcionales, clases, entidad-relación, C4 y secuencias de la nómina ([imagen del cálculo de HU-04](docs/img/payroll_sequence.png)).
- [Documento del proyecto](TiendaFlow_Project_Definition_Sprint1.pdf): definición del Sprint 1.
- [Entrega final del Sprint 2](TiendaFlow_Entrega_Final_Sprint2.pdf): revisión, correcciones y verificación por módulo.
- [Video del tour de arquitectura](https://drive.google.com/file/d/1zUALZRwRKVcWQhH2NglT0QQUR7mrlyVc/view?usp=sharing): coherencia entre las secciones 5 y 10 de arc42 en Notion.

## Flujo de ramas

```text
feature/*  ->  develop  ->  main
```

El trabajo se hace en ramas `feature/*` y se integra a `develop` mediante pull request. `main` recibe solo versiones estables. No se hace push directo a `develop` ni a `main`.
