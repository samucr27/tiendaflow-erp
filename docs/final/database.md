# Base de datos y migraciones

TiendaFlow usa EF Core con SQL Server. Las entidades y configuraciones viven en
`src/TiendaFlow.Infrastructure/Persistence`.

## Migraciones incluidas

| Migración | Contenido |
| --- | --- |
| `20261107142012_InitialCreate` | Catálogo, stock, tienda y movimientos base |
| `20261109010416_Phase2Security` | Usuarios, roles y auditoría |
| `20261109011701_Phase3Purchasing` | Proveedores y órdenes de compra |
| `20261111005733_Phase4Invoicing` | Facturas y líneas de factura |
| `20261111010125_Phase5Crm` | Clientes |

Esta entrega **no generó una migración nueva**. El panel, los reportes, el
ranking, la segmentación y el XML simulado son consultas o documentos
calculados sobre el esquema existente; no añaden tablas ni columnas. Por eso no
hay que aplicar nada nuevo para usar estas pantallas.

## Generar una migración

Se genera sin aplicarla:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'

dotnet ef migrations add NombreDeLaFase `
  --project src\TiendaFlow.Infrastructure `
  --startup-project src\TiendaFlow.Web `
  --output-dir Persistence\Migrations
```

Revisa el archivo generado y el `TiendaFlowDbContextModelSnapshot` antes de
commitear. No ejecutes `database update` contra la base de desarrollo como
parte de la revisión.

## Aplicar migraciones en desarrollo

La aplicación aplica las migraciones al arrancar **solo** cuando
`Database:MigrateOnStartup` es `true`. En `Development` viene activado; en
producción viene en `false`.

También se pueden aplicar manualmente:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'

dotnet ef database update `
  --project src\TiendaFlow.Infrastructure `
  --startup-project src\TiendaFlow.Web
```

La cadena de conexión se configura con `dotnet user-secrets` y nunca se escribe
en el repositorio. En desarrollo local el README usa `Encrypt=False` contra el
contenedor; fuera de Development la aplicación fuerza `Encrypt=True`.

## Aplicar migraciones en Azure

Producción no migra sola. La guía completa está en
[deploy-azure.md](deploy-azure.md). El resumen es:

```powershell
dotnet tool install --global dotnet-ef

dotnet ef migrations bundle `
  --project src/TiendaFlow.Infrastructure/TiendaFlow.Infrastructure.csproj `
  --startup-project src/TiendaFlow.Web/TiendaFlow.Web.csproj `
  --configuration Release `
  --runtime win-x64 `
  --output efbundle.exe `
  --force

$cs = Read-Host "Connection string de Azure SQL"
.\efbundle.exe --connection $cs
```

La cadena debe apuntar al servidor `<sql_server_fqdn>,1433`, incluir
`Encrypt=True` y usar un usuario administrador de SQL. El firewall debe permitir
la IP desde la que ejecutas el bundle (`client_ip_address` en Terraform). El
ejecutable `efbundle.exe` no se commitea.

## Sembrado de datos

`DevDataSeeder` crea la tienda, los siete roles, un usuario por rol, productos,
proveedores, facturas y clientes de demostración. Se registra siempre que
`Seed:Enabled` sea `true`, **sin importar el entorno**; no está condicionado a
`Development`. Cada tabla se llena únicamente si está vacía, así que el
sembrado es idempotente.

- Si `Seed:Enabled` es `false` o no existe, el servicio no se registra y ninguna
  ruta crea datos de ejemplo.
- Para activarlo se usan `Seed:Enabled`, `Seed:AdminPassword` y
  `Seed:DemoPassword` desde configuración o secretos locales.
- La configuración de producción lo deja desactivado. No hay usuarios ni
  contraseñas por defecto.
- Las contraseñas nunca se guardan en archivos del repositorio.

## Variables relevantes

| Clave | Valor por defecto | Uso |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | — | Cadena de SQL Server |
| `Database:MigrateOnStartup` | `false` (producción), `true` (Development) | Migrar al arrancar |
| `Seed:Enabled` | `false` (producción) | Sembrado de demostración |
| `Seed:AdminPassword` | — | Contraseña del administrador demo |
| `Seed:DemoPassword` | — | Contraseña de los demás usuarios demo |

## Nota sobre pruebas de integración

Las pruebas de `TiendaFlow.IntegrationTests` usan SQL Server real y se omiten
cuando no existe `TIENDAFLOW_TEST_CONNECTION` o
`ConnectionStrings__DefaultConnection`. No se ejecutaron contra la base de
desarrollo en esta entrega.
