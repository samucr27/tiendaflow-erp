# Compilación, pruebas y cobertura

Última verificación: **16 de noviembre de 2026**, sobre `feature/erp-complete`,
con .NET 9 en Windows y SQL Server 2022 en Docker.

Esta página se actualiza al cerrar cada fase. La última fila de resultados
corresponde a la fase de devoluciones y notas crédito.

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
| `TiendaFlow.UnitTests` | 649 | 0 | 0 | 649 |
| `TiendaFlow.AcceptanceTests` | 6 | 0 | 0 | 6 |
| `TiendaFlow.IntegrationTests` | 56 | 0 | 0 | 56 |

Total ejecutado: **711 pruebas superadas**, sin omisiones ni errores.

Las pruebas de integración se ejecutaron contra un contenedor SQL Server
desechable (`tiendaflow-sql-test`, puerto 14333) mediante
`TIENDAFLOW_TEST_CONNECTION`. El fixture crea una base única, aplica todas las
migraciones en orden y ejecuta las pruebas; no toca la base de desarrollo. Las
pruebas de humo web usan su propia base desechable: la fábrica inyecta la cadena
en el entorno durante la creación del host y falla de forma explícita si el
contexto apunta a otra base. Sin esa variable las pruebas se omiten, pero en
esta verificación se ejecutaron.

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
| `TiendaFlow.Application` | 3371 | 3476 | **97,0 %** |
| `TiendaFlow.Domain` | 896 | 946 | **94,7 %** |
| Aplicación + dominio | 4267 | 4422 | **96,5 %** |
| `TiendaFlow.Infrastructure` (con integración SQL) | 12157 | 12375 | **98,2 %** |

La lógica de negocio supera el objetivo de 85 %. La cobertura de
`TiendaFlow.Infrastructure` se midió en la misma ejecución, con las pruebas de
integración contra SQL Server real; incluye repositorios EF Core, configuraciones
y migraciones. No se ocultó la cifra global ni se excluyó el código generado para
inflar el resultado; el filtro de Application + Domain es el que usa la
documentación del proyecto.

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
- `SalesReturnTests`: estados y transiciones, autorización única, líneas
  duplicadas, cantidades y precios inválidos.
- `SalesReturnServiceTests`: permiso antes de consultar, cantidad máxima contra
  lo facturado y contra lo ya autorizado, producto ajeno a la factura, factura
  anulada, tienda inactiva, revalidación al autorizar, reingreso con
  `Return`, rechazo sin tocar stock y auditoría con usuario e IP.
- `SalesReturnsIntegrationTests`: flujo real sobre SQL Server de la devolución
  pendiente, la autorización con nota crédito y stock, la revalidación de
  cantidades después de una devolución autorizada, el rechazo sin movimiento y
  los modelos de lista, detalle y unidades disponibles.
- `FinalScreenAuthorizationTests` y `WebSmokeTests`: política de las dos rutas de
  devoluciones, enlace del menú y recorrido con los roles autorizados.

## Devoluciones y notas crédito (fase 5)

La fase 5 añade el documento de devolución con migración propia (`AddSalesReturns`):

- **Registro** (`sales.returns.authorize`): se elige una factura emitida, la tienda
  que recibe y el motivo. Las líneas se copian de la factura con su descripción y
  precio unitario, de modo que un cambio posterior del catálogo no reescribe el
  documento. La cantidad no puede superar lo facturado menos lo ya autorizado y
  un producto no puede repetirse.
- **Autorización**: solo una devolución pendiente se autoriza una vez. La
  operación asigna el consecutivo `NC-000000`, reingresa las unidades al stock de
  la tienda con el movimiento `Return` y deja el documento bloqueado. Las
  cantidades se revalidan al autorizar, porque otra devolución de la misma
  factura pudo aprobarse mientras esta esperaba.
- **Rechazo**: cierra el documento sin tocar el inventario.
- Todas las acciones exigen el permiso antes de consultar repositorios y escriben
  auditoría con módulo `sales`, usuario, IP y detalle dentro de la misma
  transacción que el movimiento de stock.

Las pantallas `/ventas/devoluciones` y `/ventas/devoluciones/{id}` paginan de 20
en 20 y declaran su política. La tienda se elige explícitamente porque
`SalesInvoice` no guarda `StoreId`; es una limitación conocida y documentada en
[implemented-modules.md](implemented-modules.md).

## Rediseño de acceso, panel y menú (Bloque 3)

El 13 de noviembre de 2026 se verificó la capa visual del inicio de sesión, el
panel y el menú con Chromium automatizado (Playwright) contra la aplicación real
y una base SQL Server desechable. La comprobación se hizo fuera de la suite de
pruebas del repositorio y no añade dependencias al proyecto.

- **1280 × 900:** el acceso muestra las dos columnas (historia de 691 px y
  formulario de 589 px), el formulario queda sobre el pliegue y el panel conserva
  el menú lateral de 264 px y el contenido de 936 px.
- **390 × 844:** el acceso colapsa a una columna, la historia se reduce a 247 px,
  la tarjeta mide 354 px y el botón queda visible; el panel no desborda
  horizontalmente y la tabla de productos se desplaza dentro de su tarjeta
  (308 px visibles, 487 px de contenido).
- **Menú móvil:** abre con el botón de 42 × 42 px, cubre los 390 px, bloquea el
  desplazamiento del fondo, cierra al tocar el velo, con Escape y al seguir un
  enlace (navega a `/inventario/stock` y deja el menú cerrado).
- **Accesibilidad:** Axe (WCAG 2.0 y 2.1, niveles A y AA) no reporta violaciones
  en el acceso, el panel ni el menú abierto, en ninguno de los dos anchos. Se
  corrigieron el contraste del botón y de los enlaces del panel, el subtítulo de
  la marca y el desbordamiento de la tabla con `position: relative` en
  `.table-wrap`.

Las capturas se generaron en el equipo de verificación y no se versionan. La
revisión estética subjetiva queda a cargo del revisor; la comprobación cubre
composición, desbordamiento, contraste y comportamiento del menú, no el juicio de
diseño.

## Revisión final del 16 de noviembre

Revisión de backend y frontend antes de la entrega. Cada corrección tiene una
prueba que fallaba antes del cambio o que cubre la regla nueva.

| Hallazgo | Corrección | Prueba |
| --- | --- | --- |
| **Ventas perdidas entre sesiones.** Blazor Server mantiene un `DbContext` por pestaña; una consulta con seguimiento devolvía la copia en caché y no el valor actual. Dos cajeros vendiendo el mismo SKU dejaban el stock en 8 en lugar de 7. | `FreshTracking.LoadFresh` olvida la copia sin cambios antes de leer en los repositorios que cargan para modificar (stock, productos, proveedores, órdenes, facturas, clientes, empleados, usuarios, tiendas, parámetros, conteos y traslados). | `LongLivedContextStockTests.SalesFromTwoCircuits_DiscountEveryUnit` |
| **Ventas simultáneas.** Sin control de concurrencia, dos escrituras al mismo tiempo se sobrescribían. | Columna `row_version` (`rowversion`) en `stock`, migración `Phase10StockConcurrency`; el conflicto se informa como "Otro usuario modificó estos datos…". | `SimultaneousSales_RejectTheSecondWriteInsteadOfLosingAUnit` |
| **Cambios fallidos que quedaban vivos.** Tras un error o un rollback, el contexto de la pestaña conservaba entidades modificadas o con ids inexistentes. | `TiendaFlowDbContext.SaveChanges` y `EfUnitOfWork` limpian el seguimiento al fallar. | `ARejectedSale_DoesNotPoisonTheNextOperationOfTheCircuit` |
| **Último administrador.** Un administrador podía quitarse el rol o desactivar al último administrador activo y dejar el sistema sin quien gestione usuarios. | `UserService.EnsureAnAdministratorRemains`. | 3 pruebas en `UserServiceTests` |
| **Mensajes de usuario ocultos.** "No puedes desactivar tu propia cuenta" o la longitud de la contraseña se mostraban como un mensaje genérico. | `DomainErrorTranslator` muestra los mensajes en español de `UserService` y deja el genérico para las validaciones del dominio. | Cubierto por las pruebas anteriores y `WebSmokeTests` |
| **Fuerza bruta en el acceso.** `POST /login` no limitaba intentos. | Límite por IP (`Security:LoginAttemptsPerMinute`, 10 por minuto) con mensaje en la pantalla de acceso. | `WebSmokeTests.LoginThrottlesRepeatedAttemptsFromTheSameClient` |
| **HU-05 sin aviso en la venta.** El comprobante no indicaba que la venta dejaba el producto en stock crítico, y el panel decía "Faltan 0 unidades" para un producto justo en su mínimo. | Aviso de stock crítico en el comprobante del POS y texto "En el mínimo" en el panel. | Verificado en el navegador con el cajero |
| **Reposición sin proveedores en la demo.** El sembrado no creaba relaciones producto · proveedor, así que la pantalla de reposición marcaba todo "Sin proveedor" y no generaba borradores. | `DevDataSeeder.SeedProductSuppliersAsync` asigna un proveedor predeterminado a cada producto demo. | Verificado: 3 borradores por $ 118.700 |
| **Ventas del POS sin documento.** La venta descontaba stock y mostraba un comprobante, pero no guardaba nada: el panel, el reporte de ventas y el consumo de la reposición la ignoraban. | `PosSaleService` guarda la venta (`pos_sale`, `pos_sale_line`) en la misma transacción del descuento; el panel, el reporte, su CSV y `EfConsumptionQuery` la suman. Migración `Phase11PosSalesAndInvoiceStock`. | `PosSaleServiceTests`, `SalesDocumentsIntegrationTests.PosSale_IsSavedWithTheStockDiscountAndShowsInTheSalesReport` |
| **Facturas sin efecto en inventario.** Emitir o anular una factura no movía stock. | La factura guarda la tienda del usuario, descuenta al emitir y devuelve (movimiento `Return`) al anular, en una transacción. Las facturas anteriores, sin tienda, no devuelven nada al anularse. | `SalesInvoiceServiceTests`, `Invoice_TakesStockWhenIssuedAndReturnsItWhenCancelled`, `Invoice_WithMoreUnitsThanTheStock_RollsBackEverything` |
| **Facturas emitidas editables.** Una factura emitida se podía cambiar. | Se quitaron `UpdateDetails` y `ClearLines`; `/facturacion/{id}` es un detalle de solo lectura y la corrección es anular y emitir otra. El formulario elige productos del catálogo con su precio. | `SalesInvoiceTests.AnIssuedInvoiceOffersNoWayToEditItsData` |
| **Detalles de interfaz.** La barra lateral decía "Tienda Tienda Centro" y el aviso de parámetros de nómina usaba el estilo de error. | Nombre de la tienda sin prefijo y aviso con estilo de advertencia. | Verificado en capturas |

Cobertura de líneas de `TiendaFlow.Application` y `TiendaFlow.Domain` en la
verificación de esta rama (unión de las tres suites, `coverlet.collector`):
**96,5 %** (Application 97,0 %, Domain 94,7 %).

## Alcance por tienda en inventario (PASS 1B)

La revisión de alcance agregó 17 pruebas que fijan la regla en el servicio, no
solo en la pantalla:

- `InventoryCountServiceTests` y `InventoryTransferServiceTests`: crear, enviar,
  recibir, anular, contar, cerrar y ajustar un documento de otra tienda lanza
  `AccessDeniedException` y no consulta stock ni abre transacción; un rol
  corporativo sí opera cualquier tienda.
- `SalesReturnServiceTests`: crear, listar, consultar, autorizar y rechazar una
  devolución de otra tienda se deniega; el filtro de lista se fuerza a la tienda
  del usuario; una factura emitida por otra tienda no se puede reintegrar en una
  tienda distinta.
- `InventoryDocumentsIntegrationTests.AStoreManagerCannotOperateTheDocumentsOfAnotherStore`:
  contra SQL Server real, un Jefe de tienda no crea conteos ni traslados de otra
  tienda y la base no recibe el documento, mientras su propia tienda sigue
  funcionando.

## Sembrado idempotente por registro (PASS 2)

El sembrado pasó de comprobar “la tabla está vacía” a decidir por registro con
una clave natural estable. Las pruebas ejecutan el `DevDataSeeder` real contra
SQL Server desechable, no un doble:

- `SeedIdempotencyTests.RunningTheSeederTwiceDoesNotDuplicateAnyRecord` toma un
  inventario de 19 tablas, ejecuta el seeder por segunda vez y exige el mismo
  inventario exacto.
- `SeedIdempotencyTests.TheSeederRepairsAnOldDatabaseWithoutDuplicatingTheRest`
  borra una relación producto · proveedor, una orden de compra, la factura
  anulada y un traslado para simular una base sembrada por una versión anterior;
  vuelve a ejecutar el seeder y exige que el inventario regrese al original sin
  duplicar el resto.

La fábrica de pruebas web serializa la creación de hosts con un candado global.
Sin él, dos clases de prueba en paralelo podían intercambiarse la cadena de
conexión a través de las variables de entorno del proceso y sembrar la misma
base dos veces; el fallo se reproducía de forma intermitente en la suite
completa. Con el candado, la suite completa se ejecutó tres veces seguidas sin
errores.

## Cómo reproducir la verificación

```powershell
dotnet build TiendaFlow.slnx
dotnet test TiendaFlow.slnx --no-build
dotnet test TiendaFlow.slnx --no-build --collect:"XPlat Code Coverage" --results-directory TestResults
```

La carpeta `TestResults/` está ignorada por Git. No se deben subir los archivos
de cobertura ni los resultados de la ejecución.
