# Módulos y pantallas implementados

Esta entrega completa las pantallas que faltaban para los módulos de compras,
panel, reportes, CRM, facturación simulada, devoluciones y notas crédito,
administración de tiendas, parámetros configurables, inventario (conteos
físicos y traslados), contratos, asistencia y turnos. Todas las rutas declaran su `Authorize(Policy = ...)`
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

## Inventario: conteos y traslados

| Ruta | Permiso de página | Función |
| --- | --- | --- |
| `/inventario/conteos` | `inventory.stock.count` | Abrir un conteo y listar los existentes con su avance |
| `/inventario/conteos/{Id:int}` | `inventory.stock.count` | Registrar cantidades, cerrar y ajustar el stock |
| `/inventario/traslados` | `inventory.stock.transfer` | Crear un traslado en borrador y listar los existentes |
| `/inventario/traslados/{Id:int}` | `inventory.stock.transfer` | Enviar, recibir o anular según el estado |

Reglas aplicadas en el dominio y en los servicios:

1. El conteo congela la cantidad del sistema de cada fila de la tienda (o de una
   ubicación concreta) al abrirse. Una recepción posterior no reescribe la
   instantánea; la diferencia se calcula contra lo congelado.
2. Para cerrar, todas las líneas deben tener cantidad contada. El ajuste se
   aplica una sola vez y deja el documento `Adjusted`; después no admite más
   cambios.
3. El traslado nace en borrador y no mueve stock. Al enviarse descuenta el origen
   y al recibirse repone el destino, creando la fila de stock si no existe. La
   anulación de un traslado enviado devuelve las unidades al origen; un traslado
   recibido no se anula.
4. Cada transición ocurre dentro de `IUnitOfWork`, escribe la auditoría con
   módulo `inventory`, usuario, IP y detalle, y no deja cantidades negativas ni
   movimientos parciales si algo falla.
5. Las listas se paginan de 20 en 20 y los importes y fechas se muestran en
   español (es-CO), con la fecha almacenada en UTC.
6. **Alcance por tienda:** el Jefe de tienda y el Auxiliar de bodega solo abren,
   editan y consultan documentos de su propia tienda; el Administrador y el
   Gerente corporativo trabajan sobre cualquier tienda. `StoreScope` exige el
   alcance dentro del servicio, antes de leer el repositorio, y las listas
   fuerzan el filtro a la tienda del usuario.
7. **Doble envío:** el traslado descuenta con el token `RowVersion` de la fila de
   stock. Si dos usuarios envían el mismo traslado a la vez, la segunda
   escritura falla con `DbUpdateConcurrencyException` y la transacción se
   revierte completa; nunca se descuenta dos veces. El detalle de la decisión
   está en [pending-items.md](pending-items.md).

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

## Interfaz y navegación

El acceso, el panel y el menú comparten una identidad de “turno de tienda” sobre
variables CSS: bermellón `#E8412C` para la marca, carbón `#1C211E` para las
superficies oscuras y papel cálido `#F4F2EC` para el fondo. El cambio es solo de
presentación: no modifica autenticación, rutas, políticas ni condiciones del
menú.

- El acceso usa una composición dividida: historia de producto a la izquierda y
  formulario a la derecha, con el mismo `POST /login`, antiforgery y campos.
- El panel abre con un bloque oscuro con saludo y acciones, seguido de las
  tarjetas de indicadores y del contenido existente.
- El menú lateral es oscuro y el botón de 42 px abre un cajón fuera de pantalla
  en móvil. El cajón se cierra con Escape, con el velo o al navegar; el fondo no
  se desplaza mientras está abierto.
- Los colores de texto y botones de estas pantallas cumplen contraste AA y la
  tabla se desplaza dentro de su tarjeta en pantallas estrechas. El detalle de la
  comprobación está en [testing.md](testing.md).

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

## Puntos, PQRS y campañas

| Ruta | Permiso de página | Función |
| --- | --- | --- |
| `/crm/puntos` | `crm.points.manage` | Saldo por cliente, libro de movimientos, acumulación y canje |
| `/crm/pqrs` | `crm.pqrs.manage` | Listado de peticiones, quejas, reclamos y sugerencias |
| `/crm/pqrs/{Id:int}` | `crm.pqrs.manage` | Detalle, toma en carga, respuesta y cierre |
| `/crm/campanas` | `crm.campaigns.manage` | Listado de campañas, creación y cambios de estado |

Los tres CSV (`/crm/puntos/export.csv`, `/crm/pqrs/export.csv` y
`/crm/campanas/export.csv`) son endpoints mínimos con la misma política que la
pantalla y usan el escritor seguro `CsvReport`, que neutraliza las fórmulas de
hoja de cálculo.

Reglas de negocio aplicadas en el dominio, los servicios y la base de datos:

1. **Puntos.** El libro `loyalty_movement` es inmutable: cada acumulación o canje
   es una fila con cliente, tienda, tipo, puntos, valor COP, referencia y fecha
   UTC. El saldo y el valor canjeable se calculan desde el libro, nunca se
   guardan. La tasa de acumulación la da `crm.points.cop_per_point` (1000 COP por
   punto) y la de canje `crm.points.cop_per_redeemed_point` (500 COP por punto).
   El índice único `(tipo, referencia)` impide contar dos veces la misma compra o
   el mismo canje. El canje exige saldo suficiente; ambos importes deben ser
   positivos y la referencia obligatoria.
2. **PQRS.** El estado avanza solo hacia adelante: `Abierta → En curso →
   Respondida → Cerrada`. La fecha límite se calcula en días hábiles con
   `crm.pqrs.response_days` (15), omitiendo sábados y domingos; no se modelan
   festivos. La respuesta conserva texto y fecha UTC. Las fechas de radicación y
   límite se guardan como `date`, no como `datetime2`, y el listado se filtra por
   estado y se pagina a 20 filas.
3. **Campañas.** Recurso global sin alcance por tienda. El nombre es único, la
   fecha de inicio no puede estar en el pasado y los detalles solo se editan en
   borrador. Los estados son `Borrador`, `Activa`, `Pausada` y `Cerrada`: una
   campaña activa puede pausarse y reanudarse (vuelve a `Activa`), y solo se
   activa dentro de su vigencia. Canal, estado y segmento son enumeraciones fijas,
   no texto libre.

Puntos y PQRS aplican `CrmStoreScope`: el Gerente corporativo ve todas las
tiendas, el Cajero y el Jefe de tienda solo la suya, con el código de negación
`crm.store.scope`. El Administrador tiene alcance global en el servicio, pero no
posee estos tres permisos por decisión de diseño; el detalle está en
[permissions.md](permissions.md) y [pending-items.md](pending-items.md). Cada
operación sensible escribe auditoría con módulo `crm`, usuario, IP y detalle, y
comparte transacción con la escritura principal.

## Devoluciones y notas crédito

| Ruta | Permiso de página | Función |
| --- | --- | --- |
| `/ventas/devoluciones` | `sales.returns.authorize` | Registrar una devolución sobre una factura emitida y listar el historial |
| `/ventas/devoluciones/{Id:int}` | `sales.returns.authorize` | Detalle, autorización con nota crédito y rechazo |

Reglas aplicadas en el dominio y en `SalesReturnService`:

1. La devolución se registra contra una factura **emitida**; una factura anulada
   no admite devoluciones. La descripción y el precio unitario se copian de la
   factura y quedan congelados en el documento.
2. La cantidad por producto no puede superar lo facturado menos lo ya
   **autorizado** en otras devoluciones de la misma factura, y un producto no
   puede aparecer dos veces. El límite se valida al crear y otra vez al
   autorizar, porque otra devolución pudo aprobarse mientras esta esperaba.
3. La devolución nace `Pending`. Al autorizar se asigna una sola vez el
   consecutivo `NC-000000`, las unidades vuelven al stock de la tienda con el
   movimiento `Return` y el documento queda bloqueado; repetir la autorización
   falla sin mover inventario.
4. El rechazo cierra la devolución sin tocar el stock. Ninguna de las dos salidas
   modifica la factura original.
5. Autorizar y rechazar se ejecutan dentro de `IUnitOfWork` y escriben auditoría
   con módulo `sales`, usuario, IP y detalle. Las filas de stock se resuelven
   antes de mutar, de modo que una fila faltante no deja una devolución a medias.
6. Las pantallas paginan de 20 en 20 y muestran fechas e importes en español
   (es-CO), con la fecha almacenada en UTC.
7. **Alcance por tienda:** el Jefe de tienda y el Auxiliar de bodega solo
   registran, consultan, autorizan y rechazan devoluciones de su tienda; el
   Administrador y el Gerente corporativo pueden operar cualquier tienda. La
   lista y los selectores de factura se filtran por la tienda del usuario.
8. La devolución debe reintegrar la misma tienda que emitió la factura. Si la
   factura guarda otra tienda, el servicio rechaza la operación antes de tocar
   el stock.

**Tienda de la factura:** `SalesInvoice` guarda `StoreId` desde la integración
de la fase de ventas. La devolución usa esa tienda para el reingreso de stock y
rechaza una tienda distinta; una factura antigua sin tienda conserva el
comportamiento anterior y permite elegirla en el formulario.

## Contratos laborales

| Ruta | Permiso de página | Función |
| --- | --- | --- |
| `/rrhh/contratos` | `hr.contracts.manage` | Historial, aviso de vencimiento y búsqueda |
| `/rrhh/contratos/nuevo` | `hr.contracts.manage` | Registrar la vinculación inicial |
| `/rrhh/contratos/{Id:int}` | `hr.contracts.manage` | Detalle, prórroga, terminación, adjuntos e historial |

Reglas aplicadas en `EmploymentContract` y `EmploymentContractService`:

1. Un contrato guarda tipo (`Indefinido`, `Término fijo`, `Obra o labor`,
   `Prestación de servicios`), fechas, salario base y estado (`Vigente`,
   `Prorrogado`, `Terminado`). Solo el indefinido puede omitir la fecha de fin;
   la fecha de fin nunca puede ser anterior a la de inicio.
2. Un empleado no puede tener dos contratos vigentes a la vez. La pantalla de
   creación deshabilita a quienes ya tienen uno y el servicio vuelve a validarlo
   antes de guardar.
3. La prórroga no sobrescribe el contrato anterior: lo marca `Prorrogado`,
   crea uno nuevo `Vigente` con las fechas y el salario indicados y lo enlaza con
   `PreviousContractId`. Las dos escrituras ocurren en la misma transacción.
4. La terminación exige confirmación en pantalla, marca `Terminado` y guarda la
   fecha UTC. Un contrato terminado o prorrogado ya no se puede renovar ni
   volver a terminar.
5. Los adjuntos aceptan PDF, PNG o JPG de máximo 2 MB. El nombre se reduce al
   archivo, el tipo de contenido se valida contra la extensión y los bytes se
   guardan en SQL Server. La descarga exige `hr.contracts.manage` y deja una
   entrada de auditoría con módulo `hr`, usuario, IP y detalle.
6. `hr.contracts.expiry_alert_days` define la ventana del aviso de contratos por
   vencer; el valor se lee con `IParameterValues`, por lo que puede cambiarse
   desde Parámetros sin recompilar. El listado muestra el aviso y enlaza directo
   a la prórroga.
7. Crear, prorrogar, terminar y descargar se auditan; la auditoría no copia el
   salario, el nombre del empleado ni el contenido del archivo. Las fechas se
   guardan en UTC y se muestran en hora local de Colombia; la lista pagina de 20
   en 20.

El sembrado de demostración crea un contrato por empleado, un contrato a término
fijo que vence dentro de la ventana de aviso, un contrato terminado y un
historial de prórroga. La clave natural empleado + fecha de inicio mantiene el
sembrado idempotente.

## Asistencia y turnos

| Ruta | Permiso de página | Función |
| --- | --- | --- |
| `/rrhh/asistencia` | `hr.attendance.manage` | Marcaciones del día, novedades, resumen y exportación CSV |
| `/rrhh/turnos` | `hr.shifts.manage` | Plantillas, calendario semanal, asignación y cancelación |
| `/rrhh/turnos/reporte` | `hr.shifts.manage` | Cruce turno · asistencia, tardanzas, ausencias y exportación CSV |
| `/rrhh/asistencia/export.csv` | `hr.attendance.manage` | Resumen de asistencia con el mismo permiso que la pantalla |
| `/rrhh/turnos/reporte/export.csv` | `hr.shifts.manage` | Reporte cruzado con el mismo permiso que la pantalla |

Reglas aplicadas en `AttendanceService` y `ShiftService`:

1. La entrada solo se registra para el día laboral actual calculado en hora de
   Colombia; un segundo intento actualiza la misma fila en lugar de duplicarla.
   La salida cierra la marcación abierta. Una fecha pasada no se marca: se corrige
   con una novedad (ausencia justificada o injustificada, licencia o vacaciones).
2. Un día es **trabajado o de novedad**, nunca ambos. El índice único por
   empleado y fecha lo confirma en SQL Server, y `hours_worked` siempre es cero
   cuando existe una novedad.
3. Las plantillas tienen nombre único, duración positiva y no superan
   `hr.shifts.max_hours_per_shift`. Una asignación exige empleado y tienda
   activos, que el empleado pertenezca a esa tienda, plantilla activa, fecha de
   hoy o futura, sin solapamiento con otro turno del mismo día y sin exceder
   `hr.shifts.max_hours_per_day` con la suma de turnos.
4. El reporte consulta primero las asignaciones y después la asistencia del
   mismo rango. Una novedad muestra su etiqueta; sin marcación muestra
   `Ausente`; marcación sin salida muestra `Incompleto`; y una marca después de
   la hora esperada muestra `Tarde` con sus minutos de tardanza.
5. El alcance lo decide `HrStoreScope`: el Jefe de tienda solo ve y opera su
   tienda, mientras el Gestor de recursos humanos trabaja en cualquier tienda.
   La negación se escribe con `hr.store.scope` y ocurre antes de consultar los
   repositorios, también en las dos exportaciones CSV.
6. Crear plantilla, asignar, cancelar, marcar entrada, registrar salida, registrar
   novedad y exportar dejan auditoría con módulo `hr`, usuario, IP y detalle. Los
   detalles usan identificadores numéricos de empleado, nunca el número de
   identificación ni el motivo de la novedad.
7. Los tres listados paginan de 20 en 20, muestran horas y fechas en es-CO y
   almacenan los instantes en UTC. Los CSV usan `CsvReport`, con BOM UTF-8 y
   neutralización de celdas que empiezan por `=`, `+`, `-` o `@`.

El sembrado de demostración crea tres plantillas, la semana laboral actual de
cuatro empleados, marcaciones de los días transcurridos, una marcación abierta,
cuatro novedades y una entrada tardía para que las tres pantallas y el reporte
muestren ejemplos reales. Las claves naturales son nombre de plantilla,
empleado + fecha + plantilla y empleado + fecha.

## Facturación simulada

La lista de facturas ofrece **XML simulado** para facturas emitidas. La ruta
`/facturacion/{id:int}/xml` devuelve un archivo `FV-xxxxx-simulada.xml` con:

- Número, cliente, documento, fecha, líneas y total.
- Un aviso dentro del propio XML que indica que no es válido ante la DIAN.
- Un resumen SHA-256 determinista marcado como `example-digest-not-certified`.

`IElectronicInvoiceGateway` es la abstracción prevista para un proveedor real;
la única implementación es `SimulatedElectronicInvoiceGateway`. No hay CUFE,
firma certificada, transmisión ni almacenamiento del documento.

## Seguridad: reportes

| Ruta | Permiso de página | Función |
| --- | --- | --- |
| `/seguridad/reportes` | `security.reports.view` | KPI de accesos, rechazos y cambios de usuario por día local |
| `/seguridad/reportes/export.csv` | `security.reports.view` | Descarga de los eventos del rango en CSV |

Los seis KPI se derivan de `audit_log`: inicios de sesión correctos
(`LoginSucceeded`), fallidos (`LoginFailed`), accesos denegados (`AccessDenied`),
cambios de usuario (`UserCreated`, `UserUpdated`, `UserDeactivated`,
`PasswordReset`), total de eventos y usuarios identificados distintos. No hay
tablas nuevas: la única migración de la fase añade el índice
`IX_audit_log_logged_at` sobre la columna de fecha.

El rango se elige en fechas locales de Colombia; los eventos se guardan en UTC y
se agrupan por fecha local, de modo que un evento de las 02:00 UTC del día
siguiente cuenta para la noche anterior en la tienda. El listado y el detalle se
paginan a 20 filas y el CSV usa el escritor seguro `CsvReport`, que neutraliza
fórmulas de hoja de cálculo. La consulta se limita a los 5000 eventos más
recientes del rango; cuando hay más, la página muestra un aviso visible y el CSV
lo incluye como primera fila, ambos con el mismo texto. La limitación se
documenta en [pending-items.md](pending-items.md). La página y el CSV comprueban
el permiso en tres capas: directiva de la página, enlace del menú y
`IAccessControl.Demand` en `SecurityReportService`.

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
| `20e6636` | Devoluciones y notas crédito |
| `f7c76d7` | Alcance por tienda en conteos y traslados |
| `009cd85` | Sembrado idempotente por registro |
| `66882d6` | Contratos laborales con prórrogas y adjuntos |
| `39e3d78` | Pruebas independientes del sistema operativo y sin SQL |
| `2c70ccd` | Asistencia y turnos |
| `10b33b7` | Puntos, PQRS y campañas |
| `392e3f3` | Reportes de seguridad |

La lista completa y su orden están en `git log --oneline feature/erp-complete`.
