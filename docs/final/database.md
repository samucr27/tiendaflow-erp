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
| `20261113012709_Phase6Stores` | Índices de apoyo para la administración de tiendas |
| `20261113013940_Phase7Parameters` | Tabla `system_parameter`, versionado por fecha y alcance |
| `20261113015239_Phase8PurchasingKpis` | Fechas prometida/recibida de la orden y tabla `product_supplier` |
| `20261114035445_Phase9InventoryDocuments` | Conteos físicos, traslados entre tiendas y sus líneas |
| `20261114042149_AddSalesReturns` | Devoluciones, líneas y nota crédito |
| `20261116144218_Phase10StockConcurrency` | `row_version` en `stock` para el control de concurrencia |
| `20261116154129_Phase11PosSalesAndInvoiceStock` | Ventas del POS y tienda de la factura |
| `20261117044933_Phase12HrContracts` | Contratos laborales y adjuntos |
| `20261118005708_Phase13HrAttendanceAndShifts` | Asistencia, novedades, plantillas de turno y asignaciones |
| `20261118014039_Phase14CrmLoyaltyPqrsCampaigns` | Libro de puntos, solicitudes PQRS y campañas |
| `20261118021007_Phase15SecurityReportIndexes` | Índice de fecha sobre `audit_log` para el reporte de seguridad |

La migración `Phase6Stores` no añade tablas ni columnas: la entidad `Store` ya
existía. Añade tres índices que sostienen las consultas de la pantalla y del
bloqueo de desactivación:

- `IX_stock_store_quantity` sobre `stock (store_id, quantity)` para saber si una
  tienda todavía tiene unidades.
- `IX_purchase_order_store_status` sobre `purchase_order (store_id, status)`
  para contar órdenes abiertas.
- `IX_employee_store_active` sobre `employee (store_id, active)` para localizar
  empleados y usuarios activos de la tienda.

EF Core reemplaza los índices de una sola columna `IX_employee_store_id` e
`IX_purchase_order_store_id` por estos compuestos, que cubren la misma clave
foránea. Las migraciones anteriores no se editaron.

`Phase7Parameters` crea la tabla `system_parameter`. Guarda una fila por versión
y nunca actualiza la anterior. `effective_from` es `date` (fecha calendario) y
`created_at` es `datetime2` en UTC. La columna calculada persistida
`store_scope_key` convierte la tienda nula en `0` para que los índices únicos
cubran también las versiones globales:

- `IX_system_parameter_key_scope_store_version` sobre clave, alcance, tienda
  calculada y versión.
- `IX_system_parameter_key_scope_store_effective` sobre clave, alcance, tienda
  calculada y fecha de vigencia.
- Claves foráneas a `store` y `app_user` con borrado restringido.

Un intento de duplicar la misma fecha o versión falla en la base, no solo en la
validación de la aplicación.

`Phase8PurchasingKpis` añade dos columnas `date` a `purchase_order`:

- `promised_date`: fecha prometida por el proveedor, opcional.
- `received_date`: fecha de la última recepción, opcional.

También crea `product_supplier`, la relación entre producto y proveedor:

- Clave primaria `(product_sku, supplier_id)` y claves foráneas a producto y
  proveedor con borrado restringido.
- `last_cost` con precisión `decimal(18,2)` e `is_default`.
- `IX_product_supplier_default` es un índice único filtrado que permite un solo
  proveedor predeterminado por producto; la regla se cumple en la base y no solo
  en el servicio.

`Phase9InventoryDocuments` crea los dos documentos de inventario:

- `inventory_count` y `inventory_count_line`: la cabecera guarda tienda,
  ubicación opcional, fechas y estado (`Open`, `Closed`, `Adjusted`); cada línea
  congela `system_quantity` y guarda `counted_quantity` nula hasta que se cuenta.
- `inventory_transfer` y `inventory_transfer_line`: origen, destino, fechas de
  envío, recepción y anulación, estado (`Draft`, `Sent`, `Received`,
  `Cancelled`) y las unidades por producto.

Las claves foráneas a `store`, las líneas con borrado en cascada desde su
cabecera y los índices `IX_inventory_count_store_status`,
`IX_inventory_count_created_at`, `IX_inventory_transfer_origin_status`,
`IX_inventory_transfer_destination_status`, `IX_inventory_count_line_count_sku`
y `IX_inventory_transfer_line_transfer_sku` sostienen las pantallas, el filtro
por tienda y las líneas de cada documento.

La fase de CRM restante ya añadió su migración propia (`Phase14`); la única
función que sigue pendiente es la edición de roles (`security.roles.manage`),
que no requiere tablas nuevas porque la matriz vive en código.

`AddSalesReturns` crea `sales_return` y `sales_return_line`:

- La cabecera guarda la factura de origen, la tienda que recibe, cliente y
  documento copiados de la factura, motivo, estado (`Pending`, `Authorized`,
  `Rejected`), número de nota crédito, fechas de creación, autorización y
  rechazo, y el total.
- Cada línea congela SKU, descripción, cantidad y precio unitario de la factura;
  no se puede repetir el SKU dentro de la devolución.
- Claves foráneas a `sales_invoice` y `store` con borrado restringido, líneas en
  cascada desde su cabecera e índices por tienda/estado, factura/estado y un
  índice único filtrado sobre `credit_note_number` para que una nota crédito no
  se repita.
- La migración se aplica con el mismo `efbundle.exe` descrito abajo; no requiere
  pasos adicionales ni datos iniciales.

`Phase10StockConcurrency` añade `row_version` (`rowversion`) a `stock`. EF Core
lo usa como token de concurrencia: dos despachos simultáneos de la misma fila no
pueden descontar dos veces el mismo saldo; el segundo recibe
`DbUpdateConcurrencyException` y se traduce a un mensaje de negocio.

`Phase11PosSalesAndInvoiceStock` crea `pos_sale` y `pos_sale_line` (venta de
mostrador con tienda, vendedor, fecha y líneas) y añade `store_id` a
`sales_invoice` con su clave foránea e índice. Las facturas nuevas guardan la
tienda que las emitió; las devoluciones usan esa tienda para reponer el stock.

`Phase12HrContracts` crea el registro de contratos:

- `employee_contract`: empleado, tipo (`Indefinite`, `FixedTerm`, `WorkOrService`,
  `ServiceProvision`), fechas de inicio y fin, salario base, estado (`Active`,
  `Extended`, `Terminated`), contrato anterior, fecha de creación y fecha de
  terminación. Solo los contratos indefinidos pueden dejar `end_date` nula; la
  regla se valida en el dominio antes de llegar a SQL.
- `contract_attachment`: nombre, tipo de contenido, bytes (`varbinary(max)`),
  usuario que cargó y fecha. El servicio limita el archivo a 2 MB y acepta solo
  PDF, PNG y JPG; el binario se guarda en la misma base para no introducir un
  servicio de almacenamiento externo en el alcance académico.
- `IX_employee_contract_employee_status`, `IX_employee_contract_end_date` e
  `IX_employee_contract_previous_contract_id` sostienen la búsqueda del contrato
  vigente, el aviso de vencimiento y el historial de prórrogas. Los adjuntos se
  borran en cascada con su contrato; las claves foráneas a empleado y usuario
  quedan restringidas.
- No crea datos iniciales. El sembrado de demostración añade contratos después
  de los empleados y usa la clave natural empleado + fecha de inicio.

`Phase13HrAttendanceAndShifts` crea el registro de asistencia y la programación
de turnos:

- `attendance_record`: empleado, tienda, fecha laboral (`date`), entrada y
  salida (`datetime2` en UTC), novedad (`int` convertido desde la enumeración),
  motivo de hasta 300 caracteres y fecha de creación UTC. El índice único
  `IX_attendance_record_employee_date` impide dos filas para el mismo empleado y
  día; `IX_attendance_record_date_store` sostiene el listado diario por tienda.
- `shift_template`: nombre único (80 caracteres), hora de inicio y fin (`time`),
  activo y fecha de creación UTC. El nombre es la clave natural que usa el
  sembrado y la validación de duplicados.
- `shift_assignment`: empleado, tienda, plantilla, fecha laboral, horas copiadas
  de la plantilla y fecha de creación UTC. Los índices por empleado + fecha,
  fecha + tienda, plantilla y tienda sostienen la semana, el reporte y el
  historial; la superposición y el tope diario se validan en el servicio antes
  de insertar.

Las claves foráneas a empleado, tienda y plantilla tienen borrado restringido.
Las horas trabajadas, la asistencia y la duración son valores calculados: no se
guardan como columnas y se derivan de las marcaciones y de los horarios. El
sembrado crea plantillas, asignaciones de la semana actual y marcaciones con
claves naturales estables; no sustituye a una fuente biométrica real.

`Phase14CrmLoyaltyPqrsCampaigns` crea los tres módulos de CRM que faltaban:

- `loyalty_movement` es un libro inmutable: cliente, tienda, tipo (`Earned` o
  `Redeemed`), puntos, valor COP, referencia y fecha UTC. El índice único
  `IX_loyalty_movement_type_reference` impide registrar dos veces la misma
  compra o canje. El saldo y el valor canjeable se derivan del libro; no hay
  columna de saldo que pueda quedar desincronizada.
- `pqrs_case` guarda cliente, tienda, tipo, asunto, descripción, estado
  (`Open`, `InProgress`, `Answered`, `Closed`), fechas de radicación y límite
  (`date`), respuesta y fecha de respuesta UTC. El plazo se calcula en días
  hábiles sin festivos; el servicio valida las transiciones hacia adelante.
- `campaign` guarda nombre único, descripción, canal, segmento, vigencia
  (`date`), estado (`Draft`, `Active`, `Paused`, `Closed`) y fecha de creación
  UTC. `IX_campaign_name` garantiza el nombre único en la base y
  `IX_campaign_dates` sostiene el listado por vigencia.

Las claves foráneas de puntos y PQRS a cliente y tienda tienen borrado
restringido. Los índices por cliente + fecha, fecha + tienda, estado + límite y
tienda sostienen los listados filtrados y paginados. El sembrado usa las claves
naturales `(tipo, referencia)`, `(cliente, asunto)` y nombre de campaña.

`Phase15SecurityReportIndexes` no crea tablas: añade `IX_audit_log_logged_at`
sobre `audit_log.logged_at`. El reporte de seguridad filtra ese rango y el índice
evita recorrer toda la tabla de auditoría. La tabla y las columnas ya existían
desde `Phase2Security`; la migración nueva no modifica las anteriores. La
consulta está limitada a los 5000 eventos más recientes del rango para que la
agregación no crezca sin límite; ese tope es una decisión documentada, no un
límite de la base de datos.

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

`DevDataSeeder` crea las tiendas, los siete roles, un usuario por rol, empleados,
productos, existencias, proveedores, clientes, órdenes, facturas, devoluciones,
conteos, traslados, contratos, turnos, asistencia, movimientos de puntos,
solicitudes PQRS y campañas de demostración. Se registra siempre que
`Seed:Enabled` sea `true`, **sin importar el entorno**; no está condicionado a
`Development`.

El sembrado decide **por registro**, no por tabla, con una clave natural estable:

| Sección | Clave natural |
| --- | --- |
| Tiendas y roles | Id fijo del catálogo |
| Empleados | Número de identificación |
| Usuarios | Correo electrónico |
| Productos y existencias | SKU y SKU + tienda |
| Proveedores | NIT |
| Clientes | Número de documento |
| Relación producto · proveedor | SKU + proveedor |
| Órdenes de compra | Proveedor + tienda + conjunto de SKU |
| Facturas | Documento del cliente y estado |
| Devoluciones | Factura (una devolución demo por factura) |
| Conteos | Tienda + ubicación |
| Traslados | Tienda origen + tienda destino + conjunto de SKU |
| Contratos | Empleado + fecha de inicio |
| Plantillas de turno | Nombre |
| Asignaciones de turno | Empleado + fecha + plantilla |
| Asistencia y novedades | Empleado + fecha |
| Movimientos de puntos | Tipo + referencia |
| Solicitudes PQRS | Cliente + asunto |
| Campañas | Nombre |
| Auditoría de seguridad | Detalle `SEED-` estable |

Así, ejecutar la aplicación dos veces no duplica nada y una base sembrada por una
versión anterior que perdió algunos registros recupera solo los que faltan. Las
pruebas `SeedIdempotencyTests` ejecutan el seeder real dos veces y también borran
filas de once secciones para comprobar la reparación sin duplicados. En auditoría
solo se borra una fila `SEED-`: los eventos reales de inicio de sesión o rechazo
no se tocan.

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
`ConnectionStrings__DefaultConnection`. En la verificación de esta rama se
ejecutaron contra un contenedor desechable (`tiendaflow-sql-test`) que aplica
las migraciones en una base nueva y no toca la base de desarrollo.
