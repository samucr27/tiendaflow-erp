# Módulos y pantallas implementados

Esta entrega completa las pantallas que faltaban para los módulos de compras,
panel, reportes, CRM y facturación simulada. Todas las rutas declaran su
`Authorize(Policy = ...)` y los servicios vuelven a exigir el permiso, de modo
que ocultar un enlace del menú no protege una función por sí solo.

## Compras

| Ruta | Permiso de página | Función |
| --- | --- | --- |
| `/compras/ordenes` | `purchasing.orders.view` | Listado de órdenes con totales y estado |
| `/compras/ordenes/nueva` | `purchasing.orders.create` | Crear una orden en borrador con líneas |
| `/compras/ordenes/{Id:int}` | `purchasing.orders.view` | Detalle y acciones según estado y permiso |
| `/proveedores/ranking` | `purchasing.reports.view` | Ranking por calificación y evaluación manual |

El detalle permite enviar, aprobar, confirmar la recepción y anular una orden,
cada acción con su permiso (`purchasing.orders.send`, `.approve`, `.receive`,
`.cancel`). La recepción genera el movimiento de inventario a través del
servicio existente; no escribe stock directamente desde la pantalla.

Para cerrar el ciclo se añadió `PurchaseOrderService.Confirm`, que pasa una
orden recibida a confirmada y deja el registro de auditoría correspondiente.

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
