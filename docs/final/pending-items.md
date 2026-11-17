# Estado final del proyecto

**Cierre:** 16 de noviembre de 2026. El proyecto termina con el Sprint 2; no hay
un Sprint 3.

## Auditoría de permisos

Se compararon las 45 constantes de `Permissions.cs` con sus usos en páginas,
menú y servicios, se listaron las rutas `.razor` con su política y se buscaron
`TODO`, `FIXME`, `HACK`, `NotImplementedException` y datos de ejemplo fuera del
sembrado de desarrollo.

Resultado: **cada permiso de la matriz tiene una función implementada**. No hay
pantallas de relleno, enlaces rotos ni datos falsos en tiempo de ejecución.

| Permiso | Función | Pantalla |
| --- | --- | --- |
| `security.roles.manage` | Edición de la matriz de roles; el Administrador conserva siempre la gestión de usuarios y roles | `/seguridad/roles` |
| `security.reports.view` | Reporte de seguridad con exportación CSV | `/reportes/seguridad` |
| `sales.returns.authorize` | Devoluciones parciales (notas crédito) que devuelven unidades al stock | `/facturacion/{id}` |
| `hr.contracts.manage` | Contratos con vigencia, prórroga y terminación | `/rrhh/contratos` |
| `hr.attendance.manage` | Asistencia y novedades; la nómina descuenta las ausencias | `/rrhh/asistencia` |
| `hr.shifts.manage` | Turnos por tienda sin cruces | `/rrhh/turnos` |
| `crm.points.manage` | Libro de puntos: acumulación por factura y canje | `/crm/puntos` |
| `crm.pqrs.manage` | PQRS con radicado, responsable y plazo de 15 días | `/crm/pqrs` |
| `crm.campaigns.manage` | Campañas por segmento A/B/C con resultado | `/crm/campanas` |

Además, el punto de venta registra varios productos por venta, las rutas
`/api/*` responden `401` y `403` en lugar de redirecciones, y la corrida de
nómina se guarda por mes.

## Fuera del alcance

| Tema | Motivo |
| --- | --- |
| Factura electrónica real ante la DIAN | Requiere un proveedor tecnológico autorizado, un certificado de firma digital y la habilitación del facturador ante la DIAN; no es posible en un proyecto académico. El sistema genera un XML **simulado** con un resumen SHA-256, sin CUFE ni firma certificada, y lo advierte en el documento y en la pantalla. |

## Pendientes de operación (no de código)

- **Publicar la versión final en Azure:** aplicar en Azure SQL las migraciones
  hasta `Phase12CloseoutModules` y desplegar desde `main`
  (`docs/final/deploy-azure.md`).
- **Revisión cruzada de pull requests:** la Definition of Done pide que otro
  integrante apruebe cada pull request; GitHub no permite que el autor apruebe
  el suyo.
- **`terraform plan` automático:** con la infraestructura real necesita
  credenciales de Azure que la suscripción de estudiante no permite crear; el
  CI valida y el `plan` se ejecuta a mano
  (`docs/sprint2/terraform-plan-workflow.md`).

## Aclaraciones importantes

- **Segmentación A/B/C:** es una regla académica (20 % / 30 % / resto). No es
  una política comercial validada ni usa un modelo estadístico.
- **Nómina:** los parámetros son referencias académicas y están documentados en
  `docs/sprint2/payroll-parameters-validation.md`. El sistema calcula y guarda
  la corrida, pero no reemplaza una liquidación laboral certificada.
- **Parámetros:** los valores iniciales son referencias académicas documentadas
  en `ParameterCatalog`. Si no existe una versión en base, el sistema usa el
  valor de código; los cambios se guardan como versiones inmutables con fecha de
  vigencia y quedan auditados.
- **Roles editables:** la matriz de `RolePermissions` es la configuración por
  defecto. Cuando un administrador guarda los permisos de un rol, se guardan en
  la tabla `role_permission` y reemplazan los de ese rol en el siguiente inicio
  de sesión; "Restablecer por defecto" vuelve a la matriz del código.
- **Pruebas de integración:** requieren una cadena de conexión a SQL Server en
  `TIENDAFLOW_TEST_CONNECTION`. Se ejecutan contra una base desechable que
  aplica las migraciones desde cero.
- **Datos de demostración:** el sembrado solo se activa con `Seed:Enabled`. En
  producción queda desactivado.
