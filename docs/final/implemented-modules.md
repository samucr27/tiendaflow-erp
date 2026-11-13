# Módulos y pantallas implementados

Esta entrega completa las pantallas que faltaban para los módulos de compras,
panel, reportes, CRM, facturación simulada, administración de tiendas y
parámetros configurables. Todas las rutas declaran su `Authorize(Policy = ...)`
y los servicios vuelven a exigir el permiso, de modo que ocultar un enlace del
menú no protege una función por sí solo.

## Tiendas

| Ruta | Permiso de página | Función |
| --- | --- | --- |
| `/seguridad/tiendas` | `security.stores.view` | Listado de sucursales con usuarios, empleados, stock y órdenes abiertas |
| `/seguridad/tiendas/nueva` | `security.stores.manage` | Crear una tienda con nombre único y dirección |
| `/seguridad/tiendas/{Id:int}` | `security.stores.view` | Detalle, edición, activación/desactivación y reasignación de personal |

Reglas de negocio aplicadas en el dominio y en `StoreService`:

1. El nombre se guarda sin espacios sobrantes y debe ser único; el repositorio
   compara sin distinguir mayúsculas al validar desde la interfaz y la base
   mantiene su índice único.
2. El identificador lo asigna `IStoreRepository.NextId()` porque la columna
   `store.id` no es identidad; se calcula dentro de la misma transacción de alta.
3. Una tienda no se puede desactivar si tiene unidades en stock, órdenes de
   compra abiertas o usuarios activos. Cada bloqueo tiene su propia excepción
   para que la pantalla explique la primera razón concreta.
4. Asignar un usuario mueve el empleado al que está vinculado, de modo que el
   usuario y su registro laboral siempre apuntan a la misma tienda. No se puede
   asignar personal a una tienda inactiva.
5. Crear, actualizar, activar, desactivar y reasignar quedan auditados con
   módulo `security`, usuario, IP y detalle. La desactivación se ejecuta dentro
   de `IUnitOfWork` para que el cambio y su auditoría sean atómicos.

El Gerente corporativo entra en modo consulta; el Administrador administra. La
matriz y la justificación están en [permissions.md](permissions.md).

## Parámetros configurables

| Ruta | Permiso de página | Función |
| --- | --- | --- |
| `/seguridad/parametros` | `security.parameters.manage` | Valores vigentes por parámetro, filtro global/tienda e historial |
| `/seguridad/parametros/{Key}` | `security.parameters.manage` | Historial de versiones y formulario para registrar una nueva |

El modelo `SystemParameter` guarda una fila por versión; nunca actualiza la
anterior. Cada versión tiene clave, tipo (`Integer`, `Decimal`, `Text`,
`Boolean`), alcance (`Global` o `Store`), tienda cuando aplica, valor
normalizado, fecha de vigencia, número de versión, autor y fecha de creación en
UTC.

Reglas aplicadas en el dominio y en `ParameterService`:

1. El catálogo es código (`SystemParameterCatalog`): una clave desconocida no se
   puede guardar y cada clave conserva un valor por defecto documentado.
2. El valor se normaliza antes de comparar o guardar (números invariantes,
   booleanos como `true`/`false`, texto sin espacios sobrantes). Para decimales
   se aceptan `1500.50` y `1.500,50`.
3. La fecha de vigencia es una fecha calendario. No se repite para la misma
   clave, alcance y tienda, y no puede ser anterior a la última versión.
4. Un parámetro global no puede tener tienda; uno por tienda exige una tienda
   activa.
5. La lectura usa la versión por tienda si existe, luego la global y, si no hay
   ninguna, el valor de código. Una versión futura no se aplica antes de su
   fecha.
6. Crear una versión queda auditado con módulo `security`, usuario, IP y el
   cambio `anterior -> nuevo`; la inserción y la auditoría comparten
   transacción.

| Clave | Tipo | Valor de código | Uso |
| --- | --- | --- | --- |
| `crm.points.cop_per_point` | Decimal | 1000 COP | Compra que genera un punto |
| `crm.points.cop_per_redeemed_point` | Decimal | 500 COP | Valor de un punto al canjear |
| `crm.pqrs.response_days` | Entero | 15 días hábiles | Plazo de respuesta a PQRS |
| `inventory.replenishment.consumption_window_days` | Entero | 30 días | Ventana de consumo para reposición |
| `inventory.replenishment.coverage_days` | Entero | 15 días | Cobertura objetivo de stock |
| `hr.contracts.expiry_alert_days` | Entero | 30 días | Aviso de contrato por vencer |
| `hr.shifts.max_hours_per_shift` | Decimal | 8 horas | Duración máxima de un turno |
| `hr.shifts.max_hours_per_day` | Decimal | 8 horas | Horas máximas asignadas por día |

`IParameterValues` expone los valores tipados a los demás módulos. Los
parámetros de nómina siguen en `IPayrollParametersProvider` con sus referencias
académicas; esta fase no cambió ese contrato.

## Compras

| Ruta | Permiso de página | Función |
| --- | --- | --- |
| `/compras/ordenes` | `purchasing.orders.view` | Listado de órdenes con totales, estado, fecha prometida y recibida |
| `/compras/ordenes/nueva` | `purchasing.orders.create` | Crear una orden en borrador con líneas y fecha prometida |
| `/compras/ordenes/{Id:int}` | `purchasing.orders.view` | Detalle, recepción con fecha y acciones según estado y permiso |
| `/compras/reposicion` | `purchasing.orders.create` | Sugerencias de reposición, generación de borradores y relación producto · proveedor |
| `/proveedores/ranking` | `purchasing.reports.view` | Ranking por calificación, evaluación manual y KPIs de recepción |

El detalle permite enviar, aprobar, confirmar la recepción y anular una orden,
cada acción con su permiso (`purchasing.orders.send`, `.approve`, `.receive`,
`.cancel`). La recepción genera el movimiento de inventario a través del
servicio existente; no escribe stock directamente desde la pantalla. Para cerrar
el ciclo se añadió `PurchaseOrderService.Confirm`, que pasa una orden recibida a
confirmada y deja el registro de auditoría correspondiente.

Reglas y datos añadidos en esta fase:

1. La orden guarda una **fecha prometida** opcional (fecha calendario) y la
   **fecha de la última recepción**. Solo se considera puntual una orden
   totalmente recibida que llegó en la fecha prometida o antes.
2. El **cumplimiento** compara unidades recibidas contra unidades pedidas en
   órdenes con al menos una unidad recibida. El ranking muestra la calificación
   manual junto con `Puntualidad` y `Cumplimiento`, o `Sin recepciones` cuando
   no hay datos medibles. Las órdenes anuladas no cuentan.
3. El ranking se exporta a CSV con `purchasing.reports.view` reutilizando el
   escritor existente; las celdas que empiezan por `=`, `+`, `-` o `@` se
   neutralizan con un apóstrofo para evitar inyección de fórmulas.
4. La **relación producto · proveedor** (`product_supplier`) guarda el último
   costo y un único proveedor predeterminado por producto; la base impone esa
   unicidad con un índice filtrado. Al recibir una orden se actualiza el último
   costo de la relación (o se crea si no existe).
5. La **reposición** calcula, con el stock disponible y el consumo de la ventana
   configurada, la cantidad
   `max(stock mínimo - disponible, consumo diario promedio × días de cobertura - disponible)`.
   Solo propone productos por debajo del objetivo y agrupa los borradores por
   proveedor predeterminado activo. Los productos sin proveedor se listan pero
   no generan orden.
6. Generar borradores crea órdenes en estado **Borrador** dentro de una sola
   transacción y las audita; nunca las envía al proveedor. El envío sigue siendo
   una acción humana con `purchasing.orders.send`.
7. La pantalla de reposición usa `purchasing.orders.create`; el editor de la
   relación producto · proveedor solo aparece con
   `purchasing.suppliers.manage`. La matriz de permisos no cambió.

## Panel

`/` y `/inventario` muestran ahora dos bloques:

- El panel de inventario que ya existía.
- Un resumen del negocio con ventas del día y órdenes de compra abiertas.

`BusinessDashboardService` consulta facturas y órdenes solo cuando el usuario
tiene `sales.reports.view` o `purchasing.orders.view`. Si no tiene ninguno de
los dos permisos, no ejecuta las consultas y no muestra cifras.

## Reportes

| Ruta | Permiso | Exportación |
| --- | --- | --- |
| `/reportes/inventario` | `inventory.reports.view` | `/reportes/inventario/export.csv` |
| `/reportes/ventas` | `sales.reports.view` | `/reportes/ventas/export.csv?from=...&to=...` |
| `/reportes/nomina` | `hr.payroll.manage` | `/reportes/nomina/export.csv?days=...&risk=...` |

Los reportes se calculan en la capa de aplicación (`Reporting/`) y la descarga
se expone en endpoints minimal API con la misma política que la página. El CSV
usa UTF-8 con BOM para que Excel abra bien tildes y símbolos, y escapa comas,
comillas y saltos de línea según RFC 4180.

El reporte de ventas interpreta el rango en la zona horaria de la tienda
(Bogotá) para que “hoy” coincida con lo que ven los cajeros. Las facturas
anuladas aparecen en el detalle pero no suman al total vendido.

## CRM

`/crm/segmentacion` clasifica a los clientes en A, B y C:

1. Solo cuentan facturas no anuladas.
2. Los clientes con compras se ordenan de mayor a menor valor facturado.
3. El 20 % superior es A, el 30 % siguiente es B y el resto es C.
4. Los clientes sin compras quedan en C.

La regla es explícita, está cubierta por pruebas y se presenta en pantalla como
referencia académica, no como política comercial validada.

## Facturación simulada

La lista de facturas ofrece **XML simulado** para facturas emitidas. La ruta
`/facturacion/{id:int}/xml` devuelve un archivo `FV-xxxxx-simulada.xml` con:

- Número, cliente, documento, fecha, líneas y total.
- Un aviso dentro del propio XML que indica que no es válido ante la DIAN.
- Un resumen SHA-256 determinista marcado como `example-digest-not-certified`.

`IElectronicInvoiceGateway` es la abstracción prevista para un proveedor real;
la única implementación es `SimulatedElectronicInvoiceGateway`. No hay CUFE,
firma certificada, transmisión ni almacenamiento del documento.

## Infraestructura y operación

- Terraform crea App Service Linux B1, Azure SQL Basic, firewall y nombres
  únicos, en las regiones permitidas por la suscripción.
- `/healthz` comprueba la base de datos y devuelve 503 si no está disponible.
- Las claves de Data Protection persisten en `/home/.tiendaflow/keys`.
- El despliegue manual usa `dotnet publish`, un zip y `az webapp deploy`.
- El flujo de GitHub con perfil de publicación es la vía secundaria.
- `terraform-plan.yml` ya no ejecuta `plan`: solo `fmt -check`, `init
  -backend=false` y `validate`, porque no hay credenciales de Azure en GitHub.

## Commits de la fase final

| Commit | Contenido |
| --- | --- |
| `279317a` | Terraform para Azure SQL y App Service Linux |
| `ca205a0` | Preparación de producción: health check y claves persistentes |
| `59f6221` | Despliegue con perfil de publicación y validación de Terraform |
| `2f1fc7e` | Guía de despliegue en Azure |
| `af38c91` | Pantallas de órdenes de compra |
| `94474e3` | Indicadores de ventas y compras en el panel |
| `6a77c8f` | Reportes de inventario, ventas y nómina con CSV |
| `f0d8a50` | Ranking y evaluación de proveedores |
| `700034d` | Segmentación A/B/C de clientes |
| `40e77ea` | Exportación de factura electrónica simulada |
