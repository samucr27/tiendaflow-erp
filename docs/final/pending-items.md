# Auditoría de pendientes

Actualizado el 17 de noviembre de 2026.

La auditoría se hizo sobre el código de `feature/erp-final` y se actualiza en
`feature/erp-complete` a medida que cada fase cierra un pendiente:

1. Se compararon las constantes de `Permissions.cs` con sus usos en páginas,
   menú y servicios.
2. Se listaron las rutas `.razor` y la política declarada en cada una.
3. Se revisó el menú para descartar enlaces sin pantalla o textos
   “Próximamente”.
4. Se buscaron `TODO`, `FIXME`, `HACK`, `NotImplementedException` y datos de
   ejemplo fuera del sembrado de desarrollo.

Resultado: no hay pantallas de relleno, enlaces rotos ni datos falsos en tiempo
de ejecución. Lo que falta son funciones que nunca se implementaron; a
continuación se documentan con el permiso que las reserva.

## Pendientes funcionales

| Área | Permiso reservado | Qué falta | Por qué queda pendiente | Siguiente paso sugerido |
| --- | --- | --- | --- | --- |
| Seguridad | `security.roles.manage` | Edición de roles desde la interfaz | La matriz es código de solo lectura por decisión de diseño | Solo si la organización exige cambio en caliente, con aprobación de dos personas y auditoría |
| Ventas | `sales.invoices.view` | Factura electrónica real ante la DIAN | Solo existe el simulador académico | Integrar un proveedor autorizado, CUFE, firma certificada y transmisión |

## Decisión de diseño: contratos solo para el Gestor de recursos humanos

`hr.contracts.manage` existe en el catálogo y lo usa la pantalla, el menú y el
servicio de contratos. Solo el rol **Gestor de recursos humanos** lo tiene. El
**Administrador** conserva `hr.employees.view` (y el resto de la administración
técnica) pero no administra contratos ni descarga sus adjuntos. Es una decisión
deliberada, no un permiso olvidado:

- El contrato y sus anexos contienen salario y documentos de identidad; mezclar
  esa operación con el rol técnico amplía el acceso a datos personales sin
  necesidad funcional.
- La matriz es código y está cubierta por `RolePermissionsTests`, así que la
  separación es determinista entre despliegues.
- Si la organización necesita que el Administrador también opere contratos, el
  cambio es una línea en `RolePermissions.cs` más las pruebas de la matriz; no
  requiere tocar pantallas ni servicios.

## Decisión de diseño: roles de solo lectura

`security.roles.manage` aparece en el catálogo de permisos, pero no se
implementó la edición de roles desde la interfaz y no está en la tabla de
pendientes funcionales. La matriz `RolePermissions.cs` es código y `/seguridad/roles`
es una vista informativa. Es una decisión deliberada: hacer de la autorización
una tabla editable desde la misma aplicación permitiría que un error o un acceso
indebido cambiara quién puede autorizar compras, devoluciones o datos de RR. HH.
Si la organización llega a exigir edición en caliente, el cambio debe incluir
auditoría, aprobación de dos personas y pruebas de autorización; no se descarta
por diseño, simplemente no entra en este alcance académico.

## Decisión de diseño: el Administrador no opera CRM

Los tres permisos nuevos de CRM se asignan de forma exclusiva: `crm.points.manage`
al Cajero, `crm.pqrs.manage` al Jefe de tienda y `crm.campaigns.manage` al Gerente
corporativo. El **Administrador no recibe ninguno**, aunque sus servicios de
puntos y PQRS tienen alcance global por tienda. Es deliberado: se separa la
operación de punto de venta, la atención de solicitudes en la sucursal y la
gestión corporativa de campañas del rol técnico que administra usuarios,
tiendas y parámetros. La matriz está en código y cubierta por
`RolePermissionsTests`, así que la decisión es determinista. Si la organización
necesita que el Administrador opere estos módulos, basta una línea en
`RolePermissions.cs` más la prueba de la matriz; no hay que tocar pantallas ni
servicios.

## Resuelto en la revisión final (16 y 17 de noviembre)

- **Venta del POS:** se guarda como documento y la cuentan el panel, el reporte
  de ventas y la reposición.
- **Facturas e inventario:** emitir descuenta stock y anular lo devuelve.
- **Facturas emitidas:** ya no se editan; se anulan y se emite otra.
- **Asistencia:** hay marcación de entrada y salida del día, novedades para
  fechas pasadas, resumen por empleado, exportación CSV y unicidad por empleado
  y día en SQL Server.
- **Turnos:** hay plantillas con tope configurable, agenda semanal, asignación
  con control de solapamiento y tope diario, cancelación y reporte cruzado con
  la asistencia. El Jefe de tienda opera solo en su tienda.
- **CRM:** hay libro de puntos inmutable con acumulación y canje, solicitudes
  PQRS con estados y plazo en días hábiles, y campañas con vigencia y estados.
  Cada módulo tiene pantalla, permiso, CSV y auditoría; la migración es propia y
  no se editaron las anteriores.
- **Reportes de seguridad:** `/seguridad/reportes` agrega los eventos de
  `audit_log` por día local, muestra los seis KPI y exporta CSV con la misma
  política `security.reports.view`. No añade tablas de negocio; su migración
  solo crea el índice de fecha.

Queda pendiente un proceso, no código: los pull requests deben tener una
revisión registrada en GitHub por otro integrante antes de fusionarse, como
exige la Definition of Done.

## Aclaraciones importantes

- **XML simulado:** se genera un documento legible y un resumen SHA-256
  determinista. No tiene CUFE, no está firmado con certificado y no se
  transmite a la DIAN. El aviso está dentro del XML y en la pantalla de
  facturación.
- **Segmentación A/B/C:** es una regla académica (20 % / 30 % / resto). No es
  una política comercial validada ni usa un modelo estadístico.
- **Puntos:** las tasas de acumulación y canje son parámetros académicos
  (`crm.points.cop_per_point` y `crm.points.cop_per_redeemed_point`). El saldo no
  se guarda: se deriva del libro inmutable, y el índice único por tipo y
  referencia evita contar dos veces la misma compra o canje. No está integrado
  con la venta real ni con un programa de fidelización externo.
- **Plazo de PQRS:** se calcula en días hábiles omitiendo sábados y domingos. No
  se modelan días festivos ni calendario laboral por país, así que el vencimiento
  puede caer en un festivo. Para uso real haría falta una tabla de festivos.
- **Reporte de seguridad:** los KPI provienen de los eventos `LoginSucceeded`,
  `LoginFailed`, `AccessDenied` y los cuatro de cambios de usuario; cualquier
  otra acción cuenta solo en el total. El rango se interpreta en hora de
  Colombia y los eventos UTC se agrupan por fecha local. Se consultan como mucho
  los **5000 eventos más recientes** del rango. La consulta pide 5001 filas de
  sonda y descarta la más antigua, sin un `COUNT` adicional; cuando aparece esa
  fila extra el DTO marca `Truncated` y la página y el CSV muestran el aviso
  «Mostrando los 5.000 eventos más recientes del rango seleccionado. Acota el
  rango de fechas para ver el detalle completo.». El reporte sigue siendo
  parcial en auditorías grandes: el aviso lo hace explícito, pero no sustituye a
  un conteo forense completo. Es un reporte académico de apoyo, no un sistema de
  detección de intrusiones.
- **Nómina:** los parámetros son referencias académicas y están documentados en
  `docs/sprint2/payroll-parameters-validation.md`. El sistema calcula la
  corrida, pero no reemplaza una liquidación laboral certificada.
- **Parámetros:** los valores iniciales son referencias académicas documentadas
  en `ParameterCatalog`. Si no existe una versión en base, el sistema usa el
  valor de código; los cambios se guardan como versiones inmutables con fecha de
  vigencia y quedan auditados.
- **Devoluciones:** la devolución se registra contra una factura emitida y se
  autoriza una sola vez. La factura guarda la tienda de emisión, así que el
  reingreso se hace en esa misma tienda; el servicio rechaza una tienda distinta
  antes de tocar el stock. Una factura antigua sin tienda conserva la selección
  manual.
- **Alcance por tienda:** los conteos, traslados y devoluciones aplican el
  alcance en el servicio (`StoreScope`), no solo en la pantalla. El Jefe de
  tienda y el Auxiliar de bodega trabajan únicamente con documentos de su tienda;
  el Administrador y el Gerente corporativo no tienen esa restricción. Un usuario
  sin tienda asignada no puede operar documentos de tienda.
- **Doble envío de un traslado:** se resolvió con el token de concurrencia
  `RowVersion` que ya tiene cada fila de stock. Si dos envíos del mismo traslado
  compiten, el segundo actualiza una fila cuya versión cambió, EF Core lanza
  `DbUpdateConcurrencyException`, la transacción se revierte y el usuario ve el
  mensaje de reintento. Se eligió el token existente en lugar de un bloqueo
  pesimista porque no requiere migración ni mantiene conexiones abiertas, y
  porque el mismo token ya protege ventas y recepciones.
- **Pruebas de integración:** requieren una cadena de conexión a SQL Server en
  `TIENDAFLOW_TEST_CONNECTION`. En la verificación de esta rama se ejecutaron
  contra un contenedor desechable que aplica las migraciones desde cero; no se
  usó la base de desarrollo.
- **Datos de demostración:** el sembrado se registra siempre que
  `Seed:Enabled` sea verdadero, sin importar el entorno; no está atado a
  `Development`. Decide por registro con claves naturales (NIT, documento, SKU,
  correo, número de identificación, proveedor + tienda + SKU en órdenes,
  traslados y empleado + fecha de inicio en contratos), así que ejecutarlo dos
  veces no duplica y una base vieja recupera solo lo que falta. Si la clave es falsa o no existe, ninguna ruta crea
  usuarios, productos ni facturas de ejemplo. En la configuración de producción
  se deja desactivada.
- **Roles:** la matriz es código y no se edita desde la interfaz. Es una decisión
  de diseño para que la autorización sea determinista entre despliegues, no un
  pendiente funcional.

## Lo que sí quedó completo

Compras (órdenes, puntualidad y cumplimiento con datos de recepción,
exportación del ranking y borradores de reposición con relación producto ·
proveedor), panel, reportes, CRM (segmentación, puntos, PQRS y campañas),
exportación XML simulada, administración de tiendas, parámetros configurables,
inventario con conteos físicos y traslados entre tiendas, devoluciones con nota
crédito, contratos laborales con prórrogas y adjuntos, asistencia con novedades y
turnos con reporte cruzado están implementados, con permisos, pruebas y
documentación. El detalle está en [implemented-modules.md](implemented-modules.md).
