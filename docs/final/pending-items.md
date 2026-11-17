# Auditoría de pendientes

Actualizado el 16 de noviembre de 2026.

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
| Seguridad | `security.roles.manage` | Editar roles desde la interfaz | La matriz es código para que la autorización sea determinista; la pantalla es de solo lectura | Persistir roles solo si el negocio lo exige, con auditoría y pruebas de autorización |
| Seguridad | `security.reports.view` | Reportes de seguridad | La auditoría ya es consultable en `/seguridad/auditoria`; no hay reportes agregados | Agregar conteos de accesos, cambios de usuario y exportación CSV |
| Ventas | `sales.invoices.view` | Factura electrónica real ante la DIAN | Solo existe el simulador académico | Integrar un proveedor autorizado, CUFE, firma certificada y transmisión |
| RR. HH. | `hr.contracts.manage` | Contratos y documentos laborales | El empleado guarda tipo de contrato, pero no el contrato como documento | Contratos con vigencia, prórrogas y adjuntos |
| RR. HH. | `hr.attendance.manage` | Asistencia | No hay registro de entrada/salida ni novedades | Marcaciones, ausencias y cálculo de días trabajados |
| RR. HH. | `hr.shifts.manage` | Turnos | No hay calendario de turnos | Turnos por tienda, asignación y cruce con asistencia |
| CRM | `crm.points.manage` | Puntos de fidelización | El cliente no tiene saldo ni movimientos de puntos | Libro de puntos con acumulación y canje |
| CRM | `crm.pqrs.manage` | PQRS | No hay entidad de peticiones, quejas o reclamos | Radicado, responsable, estados y tiempos |
| CRM | `crm.campaigns.manage` | Campañas | La segmentación es de solo consulta | Campaña sobre un segmento, vigencia y resultado |

## Resuelto en la revisión final (16 de noviembre)

- **Venta del POS:** se guarda como documento y la cuentan el panel, el reporte
  de ventas y la reposición.
- **Facturas e inventario:** emitir descuenta stock y anular lo devuelve.
- **Facturas emitidas:** ya no se editan; se anulan y se emite otra.

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
  correo, número de identificación, proveedor + tienda + SKU en órdenes y
  traslados), así que ejecutarlo dos veces no duplica y una base vieja recupera
  solo lo que falta. Si la clave es falsa o no existe, ninguna ruta crea
  usuarios, productos ni facturas de ejemplo. En la configuración de producción
  se deja desactivada.
- **Roles:** la matriz es código y no se edita desde la interfaz. Es una decisión
  de diseño para que la autorización sea determinista entre despliegues, no un
  pendiente funcional.

## Lo que sí quedó completo

Compras (órdenes, puntualidad y cumplimiento con datos de recepción,
exportación del ranking y borradores de reposición con relación producto ·
proveedor), panel, reportes, segmentación CRM, exportación XML simulada,
administración de tiendas, parámetros configurables, inventario con conteos
físicos y traslados entre tiendas, y devoluciones con nota crédito están
implementados, con permisos, pruebas y documentación. El detalle está en
[implemented-modules.md](implemented-modules.md).
