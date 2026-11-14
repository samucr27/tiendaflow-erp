# Compilación, pruebas y cobertura

Última verificación: **13 de noviembre de 2026**, sobre `feature/erp-complete`,
con .NET 9 en Windows.

## Comandos

```powershell
dotnet build TiendaFlow.slnx
dotnet test TiendaFlow.slnx --no-build
dotnet test TiendaFlow.slnx --no-build --collect:"XPlat Code Coverage" --results-directory TestResults
```

## Resultado de compilación

```text
Compilación correcta.
    0 Advertencia(s)
    0 Errores
```

## Resultado de pruebas

| Proyecto | Superadas | Omitidas | Con error | Total |
| --- | ---: | ---: | ---: | ---: |
| `TiendaFlow.UnitTests` | 589 | 0 | 0 | 589 |
| `TiendaFlow.AcceptanceTests` | 6 | 0 | 0 | 6 |
| `TiendaFlow.IntegrationTests` | 42 | 0 | 0 | 42 |

Total ejecutado: **637 pruebas superadas**, sin omisiones ni errores.

Las pruebas de integración se ejecutaron contra un contenedor SQL Server
desechable (`tiendaflow-sql-test`, puerto 14333) mediante
`TIENDAFLOW_TEST_CONNECTION`. El fixture crea una base única, aplica todas las
migraciones en orden y ejecuta las pruebas; no toca la base de desarrollo. Sin
esa variable las pruebas se omiten, pero en esta verificación se ejecutaron.

## Cobertura

La cobertura se recogió con `coverlet.collector` y se agregó con
ReportGenerator usando el mismo filtro que documenta el README:

```powershell
reportgenerator -reports:"TestResults\**\coverage.cobertura.xml" `
  -targetdir:coveragereport -reporttypes:TextSummary `
  "-assemblyfilters:+TiendaFlow.Application;+TiendaFlow.Domain"
```

| Capa | Líneas cubiertas | Líneas válidas | Cobertura de línea |
| --- | ---: | ---: | ---: |
| `TiendaFlow.Application` | 2985 | 3070 | **97,2 %** |
| `TiendaFlow.Domain` | 776 | 812 | **95,5 %** |
| Aplicación + dominio | 3761 | 3882 | **96,8 %** |

La lógica de negocio supera el objetivo de 85 %. La cobertura global del
reporte incluye `TiendaFlow.Infrastructure`: repositorios EF Core y migraciones
que solo se ejercitan con SQL Server. No se ocultó esa cifra ni se excluyó el
código generado para inflar el resultado; el filtro de Application + Domain es
el que usa la documentación del proyecto.

## Corrección de consultas y errores inesperados (Bloque 1)

Los listados y detalles de proveedores y clientes fallaban contra SQL Server con
un error de traducción de EF Core. La causa era el orden de la consulta:
`OrderBy`, `Where` y `FirstOrDefault` se aplicaban **después** de proyectar a un
constructor de record, y el proveedor no puede traducir esas operaciones sobre
el resultado ya proyectado.

La corrección mueve el filtro y el orden al `IQueryable` de la entidad, antes de
la proyección, en `EfSupplierQuery`, `EfCustomerQuery` y `EfPurchaseOrderQuery`
(encabezado y líneas). No se materializan tablas completas: la base sigue
filtrando y ordenando.

Además, `DomainErrorTranslator` registra con `ILogger` cualquier excepción no
controlada (tipo, mensaje y traza) y conserva el mensaje genérico para el
usuario; `Program.cs` conecta el logger una sola vez. `WebSmokeTests` recorre las
rutas permitidas de los siete roles con `WebApplicationFactory`, inicia sesión
por el formulario real y falla si una página muestra el aviso de error; las
contraseñas se generan en tiempo de prueba y nunca se guardan. El seeder de
demostración es idempotente y solo se activa con `Seed:Enabled`: dos tiendas,
once empleados, siete usuarios de rol, quince productos con stock variado y bajo,
seis proveedores, ocho clientes, ocho órdenes en todos los estados, seis facturas
emitidas y una anulada.

## Conteos físicos y traslados (Bloque 2, fase 4)

La fase 4 añade dos documentos de inventario con migración propia
(`Phase9InventoryDocuments`):

- **Conteo físico** (`inventory.stock.count`): congela la cantidad del sistema
  de cada fila de stock al abrirse, permite registrar la cantidad contada línea
  por línea, exige que todas las líneas estén contadas para cerrar y aplica el
  ajuste una sola vez. Después de ajustado el documento es inmutable.
- **Traslado entre tiendas** (`inventory.stock.transfer`): nace en borrador, al
  enviarse descuenta el stock del origen dentro de una transacción y al recibirse
  repone el destino creando la fila si no existe; la anulación de un traslado
  enviado devuelve las unidades al origen. Un traslado recibido no se anula.

Ambos servicios exigen el permiso antes de tocar repositorios, escriben la
auditoría con módulo, usuario, IP y detalle dentro de la misma transacción que el
movimiento de stock y nunca dejan cantidades negativas. Las pantallas
`/inventario/conteos`, `/inventario/conteos/{id}`, `/inventario/traslados` y
`/inventario/traslados/{id}` paginan de 20 en 20 y declaran su política.

## Pruebas añadidas en esta fase

- `QueryRegressionIntegrationTests`: diez pruebas que ejecutan las consultas
  reales contra SQL Server (proveedores, clientes, órdenes y líneas, facturas,
  productos, stock, consumo, usuarios y empleados, auditoría, parámetros y
  relaciones producto · proveedor); son la regresión directa del orden de
  proyección que rompía los listados.
- `WebSmokeTests`: inicio de sesión real y recorrido de todas las rutas
  permitidas para los siete roles, sin errores de página ni redirecciones
  inesperadas, más el rechazo de credenciales incorrectas sin revelar qué campo
  falló.
- `DomainErrorTranslatorTests`: una excepción desconocida se registra con tipo,
  mensaje y traza y devuelve el mensaje genérico; una excepción de dominio
  conserva su mensaje y no genera ruido en el log.
- `SqlServerDatabaseFixture` y `ErpWebApplicationFactory`: bases desechables,
  migraciones reales, siembra con contraseñas aleatorias y limpieza al terminar.

- `SystemParameterTests`: normalización de enteros, decimales, booleanos y
  texto; claves, alcances, versiones y fechas inválidas.
- `ParameterServiceTests`: catálogo con valores de código, valor global y por
  tienda, versionado, fecha duplicada o anterior, rangos, tienda inactiva,
  historial con autor, auditoría y negación sin permiso antes de consultar.
- `ParameterValuesTests`: valor por defecto, valor global, anulación por tienda,
  versión futura y clave desconocida.
- `ParameterVersioningIntegrationTests`: versiones reales sobre SQL Server,
  lectura de la última vigente, historial y auditoría persistida, más el índice
  único que rechaza un duplicado global.
- `RolePermissionsTests`: solo el Administrador administra parámetros.
- `FinalScreenAuthorizationTests`: política de las dos rutas de parámetros y
  enlace del menú.
- `StoreServiceTests`: alta con nombre único, edición, activación y
  desactivación con los tres bloqueos, reasignación de usuarios y empleados,
  auditoría y negación sin permiso antes de tocar repositorios.
- `StoreDirectoryServiceTests`: listado y detalle con `security.stores.view`,
  negación sin consultar y tienda inexistente.
- `StoreDirectoryIntegrationTests`: contadores reales de usuarios, empleados,
  stock y órdenes abiertas sobre SQL Server, más `ExistsName` y `NextId`.
- `RolePermissionsTests`: el Administrador administra tiendas y el Gerente
  corporativo solo consulta; ningún otro rol recibe esos permisos.
- `FinalScreenAuthorizationTests`: política de las tres rutas de tiendas y
  enlace del menú.
- `BusinessDashboardServiceTests`: ventas del día, órdenes abiertas y
  verificación de que sin permiso no se consultan los repositorios.
- `ReportingTests`: CSV con comas, comillas y saltos de línea; totales de
  inventario; rango local de ventas; nómina con empleados incompletos.
- `SupplierServiceTests`: ranking y evaluación, KPIs de puntualidad y
  cumplimiento, exportación CSV y neutralización de fórmulas.
- `ProductSupplierTests`: validación de SKU, proveedor y costo, y cambio de
  costo predeterminado.
- `ProductSupplierServiceTests`: primera relación predeterminada, cambio de
  predeterminado, costo, producto/proveedor inválido o inactivo, auditoría y
  negación sin permiso antes de consultar.
- `ReplenishmentServiceTests`: ventana y cobertura configuradas, fórmula contra
  stock mínimo y consumo, proveedor predeterminado, agrupación por proveedor,
  omisión de filas sin proveedor activo, borradores y negación sin permiso.
- `CsvReportSecurityTests`: neutralización de celdas que empiezan por `=`, `+`,
  `-` o `@`.
- `PurchaseOrderTests`: fecha prometida, fecha de recepción de la última
  entrega y validaciones de ambas.
- `PurchaseOrderServiceTests`: creación con fecha prometida, recepción con
  fecha y actualización o alta del último costo de la relación.
- `PurchasingPhase3IntegrationTests`: relación persistida con un solo
  predeterminado por producto, KPIs reales sobre SQL Server, consumo por
  ventana y borradores de reposición sin envío automático.
- `FinalScreenAuthorizationTests`: política de la ruta de reposición y enlace
  del menú.
- `CustomerSegmentationServiceTests`: regla 20/30, clientes sin compras,
  facturas anuladas y permiso.
- `ElectronicInvoiceTests`: campos del XML, escape de caracteres, digest
  determinista, factura anulada, permiso y factura inexistente.
- `FinalScreenAuthorizationTests`: políticas de las rutas nuevas, enlaces del
  menú y aviso de que el XML no es válido ante la DIAN.
- `InventoryCountTests` / `InventoryTransferTests`: estados, transiciones,
  inmutabilidad tras cerrar o ajustar, líneas duplicadas, cantidades inválidas y
  movimientos de stock de entrada, salida y ajuste.
- `InventoryCountServiceTests` / `InventoryTransferServiceTests`: permiso antes
  de consultar, congelado de cantidades, validación de toda la hoja antes de
  mutar, transacción única, auditoría con usuario e IP, stock insuficiente sin
  efectos parciales y reposición del destino.
- `InventoryDocumentsIntegrationTests`: flujo real sobre SQL Server del conteo
  (instantánea, ajuste único), del traslado (envío, recepción y anulación) y de
  los modelos de lista y detalle con nombres y filtro por tienda.
- `WebSmokeTests`: las cuatro rutas de conteos y traslados se recorren con los
  roles que tienen el permiso; el seeder siembra un conteo abierto y uno cerrado
  y dos traslados sin mover el stock de las demás pantallas.

## Cómo reproducir la verificación

```powershell
dotnet build TiendaFlow.slnx
dotnet test TiendaFlow.slnx --no-build
dotnet test TiendaFlow.slnx --no-build --collect:"XPlat Code Coverage" --results-directory TestResults
```

La carpeta `TestResults/` está ignorada por Git. No se deben subir los archivos
de cobertura ni los resultados de la ejecución.
