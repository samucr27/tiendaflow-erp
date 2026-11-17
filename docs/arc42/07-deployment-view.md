# 07 — Vista de despliegue

Versión final del Sprint 2 (16 de noviembre de 2026). TiendaFlow tiene dos entornos: el
de desarrollo en la máquina de cada integrante y el de producción en Azure, descrito con
Terraform.

## 7.1 Nivel 1 de infraestructura: producción en Azure

| Nodo | Tecnología | Contenido |
|---|---|---|
| Grupo de recursos | `azurerm_resource_group`, región `canadacentral` por defecto | Agrupa todos los recursos del ERP con las mismas etiquetas. |
| Servidor de aplicación | Azure App Service Linux, plan `B1`, .NET 9 (`azurerm_service_plan`, `azurerm_linux_web_app`) | Blazor Web App `TiendaFlow.Web` con renderizado interactivo en servidor. Solo HTTPS, WebSockets activos (los usa Blazor Server), *Always On*, FTP deshabilitado y sonda de salud en `/healthz`. URL actual: `app-tiendaflow-ba67bx.azurewebsites.net`. |
| Base de datos | Azure SQL, servidor `azurerm_mssql_server` (TLS 1.2 mínimo) y base `TiendaFlow` nivel `Basic` | Esquema creado con las migraciones de EF Core, de `InitialCreate` a `Phase11PosSalesAndInvoiceStock`. |
| Firewall de Azure SQL | `AllowAzureServices` y, opcional, `AllowClientIp` | El App Service entra por la regla de servicios de Azure; la IP del operador solo se abre para ejecutar migraciones desde su equipo. |
| Estado de Terraform | HCP Terraform, organización `Tienda-Flow`, workspace `tiendaflow-infra`, modo de ejecución *Local* | HCP guarda y bloquea el state compartido; `plan` y `apply` corren en la máquina del operador con `az login`. |
| Integración continua | GitHub Actions: `ci.yml`, `terraform-plan.yml` (Terraform Validate) y `azure-deploy.yml` (manual) | Build y pruebas en cada push; validación de Terraform en PR a `develop`; publicación manual con perfil de publicación. |

## 7.2 Nivel 1 de infraestructura: desarrollo

| Nodo | Tecnología | Contenido |
|---|---|---|
| Servidor de aplicación | .NET 9, Kestrel (`http://localhost:5212`) | La misma aplicación en entorno `Development`. |
| Base de datos | SQL Server 2022 en Docker (contenedor `tiendaflow-sql`, puerto 1433) | Migraciones aplicadas al arrancar (`Database:MigrateOnStartup = true`) y datos de demostración (`Seed:Enabled = true`). |
| Pruebas de integración | La misma instancia de SQL Server | Cada corrida crea una base única, aplica todas las migraciones y la borra al terminar. |

## 7.3 Configuración y secretos

| Ajuste | Desarrollo | Producción (App Service) |
|---|---|---|
| Cadena de conexión | `dotnet user-secrets` | `ConnectionStrings__DefaultConnection`, generada por Terraform; se fuerza `Encrypt=True` fuera de `Development` |
| Migraciones al arrancar | `Database:MigrateOnStartup = true` | `false`: el esquema se actualiza con el bundle de EF Core |
| Datos de demostración | `Seed:Enabled = true` con `Seed:AdminPassword` y `Seed:DemoPassword` en user-secrets | `Seed__Enabled` según la variable `seed_enabled` (por defecto `false`) |
| Claves de Data Protection | Almacén por defecto de ASP.NET Core en el perfil del usuario | `/home/.tiendaflow/keys`, la ruta persistente de App Service Linux, para que las cookies sobrevivan a reinicios |
| Encabezados reenviados | No aplica | `ASPNETCORE_FORWARDEDHEADERS_ENABLED = true`: App Service termina TLS y reenvía protocolo e IP del cliente |

Las contraseñas y cadenas de conexión nunca se versionan. En Terraform, la contraseña del
administrador de Azure SQL y las de demostración son variables sensibles del workspace.

## 7.4 Flujo de despliegue

1. El equipo trabaja en una rama de feature; el push ejecuta `ci.yml` (restore, build y
   pruebas con cobertura).
2. El pull request a `develop` ejecuta además **Terraform Validate** cuando cambian
   archivos `.tf`.
3. `develop` se lleva a `main` con un pull request de entrega.
4. **Infraestructura:** el operador ejecuta `terraform plan` y `terraform apply` desde su
   máquina contra el workspace de HCP Terraform.
5. **Esquema:** se genera el bundle (`dotnet ef migrations bundle`) y se ejecuta contra
   Azure SQL desde la IP autorizada.
6. **Aplicación:** `dotnet publish` y `az webapp deploy`, o el workflow manual
   `azure-deploy.yml`. Al terminar se comprueba que `/healthz` responda `200`.

El procedimiento completo, con comandos y solución de problemas, está en
`docs/final/deploy-azure.md`.

## 7.5 Integración continua

| Paso | Comando o acción | Resultado esperado |
|---|---|---|
| Checkout | `actions/checkout@v4` | Código de la rama |
| SDK | `actions/setup-dotnet@v4` con .NET 9.0.x | SDK disponible |
| Restore | `dotnet restore` | Paquetes restaurados |
| Build | `dotnet build --no-restore` | 0 advertencias y 0 errores |
| Test | `dotnet test --no-build --collect:"XPlat Code Coverage"` | Pruebas unitarias y de aceptación; las de integración se omiten porque el runner no tiene SQL Server y se ejecutan en local |

## 7.6 Infraestructura como código

- `infra/terraform/main.tf` declara el backend `cloud` de HCP Terraform y los recursos de
  la sección 7.1; `variables.tf` define región, nombres, SKU y variables sensibles, y
  `outputs.tf` expone la URL de la aplicación y el servidor de base de datos.
- El workflow de pull request pasó por dos etapas: primero ejecutó `terraform plan`
  automático sobre un recurso de ejemplo (`random_pet`) y quedó en verde; con la
  infraestructura real necesita credenciales de Azure, que Azure for Students no permite
  entregar a GitHub Actions, así que el CI ejecuta `fmt`, `init -backend=false` y
  `validate`. El detalle está en `docs/sprint2/terraform-plan-workflow.md`.

## 7.7 Limitaciones y notas de operación

- No hay entorno de *staging*: los cambios se prueban en local y se publican en
  producción.
- El despliegue automático no ejecuta las migraciones; si se publica una versión con
  migraciones nuevas sin aplicarlas antes, las pantallas que usan tablas nuevas fallan
  aunque `/healthz` responda `200`.
- La corrida de nómina no se persiste; no hay tablas de corrida ni de detalle.
