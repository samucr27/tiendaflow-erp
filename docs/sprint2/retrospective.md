# Sprint 2 - Retrospectiva - TiendaFlow ERP

**Fecha de cierre:** 10 de noviembre de 2026. **Actualizada:** 16 de noviembre de
2026 con la versión final publicada en `main` (sección 12).

## 1. Qué salió bien

- La arquitectura por capas (Domain, Application, Infrastructure, Web) y la regla
  de que Domain y Application no referencian EF Core se mantuvieron en los tres
  módulos nuevos: Proveedores, Facturación y CRM clientes.
- La seguridad quedó implementada con 7 roles, una matriz central, autorización en
  tres niveles y auditoría de accesos cubierta por pruebas automatizadas. Quedan
  decisiones abiertas de privacidad (el correo del intento fallido de inicio de
  sesión) y una desalineación latente de permisos en alertas, registradas en
  `docs/sprint2/open-questions.md`.
- El motor de nómina quedó aislado tras interfaces, con pruebas unitarias de
  números calculados a mano.
- La documentación arc42 se actualizó junto con el código, incluida la matriz de
  roles de la sección 10 y los módulos nuevos de la sección 5.

## 2. Resultados verificables del Sprint 2

- **Pruebas:** 310 pruebas activas en verde (304 unitarias y 6 de aceptación) y 17
  de integración omitidas porque el runner no tiene SQL Server. La línea base del
  Sprint 1 tenía 213 pruebas.
- **Cobertura:** 88,4 % de líneas y 73,6 % de ramas sobre `TiendaFlow.Application`
  y `TiendaFlow.Domain`, medida con coverlet y ReportGenerator el 10 de noviembre
  de 2026. `Application` queda en 87,2 % y `Domain` en 91,9 %. El umbral exigido
  es 80 % de líneas.
- **Build:** `dotnet build TiendaFlow.slnx` termina con 0 advertencias y 0 errores.
- **CI:** el workflow `.github/workflows/ci.yml` ejecuta restore, build y
  `dotnet test` con cobertura en cada push; las pruebas de integración se omiten
  porque el runner no tiene SQL Server. Esta revisión no consultó el resultado
  remoto de GitHub Actions.
- **Terraform:** el workflow `.github/workflows/terraform-plan.yml` se ejecutó en
  verde en el pull request hacia `develop`: `init`, `validate` y `plan` con estado
  remoto en HCP Terraform, job `plan` de unos 20 segundos. El recurso planificado
  es el *placeholder* `random_pet`; no hubo `terraform apply` ni infraestructura
  real.
- **Módulos entregados:** Inventario y RR. HH. (base previa), Proveedores,
  Facturación y CRM clientes, cada uno con entidad de dominio, servicio de
  aplicación, repositorio y consulta EF Core, migración, páginas Blazor en
  español, permisos y pruebas unitarias.
- **Documentación:** arc42 (secciones 05, 07 y 10), guion del tour de
  arquitectura con tabla de verificación módulo-código-permiso, guion de demo,
  retrospectiva y paquete para Notion.

## 3. Respuesta al Sprint Goal

El Sprint Goal se cumplió en el alcance funcional: existen los cinco CRUD
mencionados (Inventario, RR. HH., Proveedores, Facturación y CRM), el motor de
nómina, la alerta de stock crítico, el workflow de Terraform y la documentación
arc42. La única limitación declarada es que **las corridas de nómina no se
persisten**: `PayrollRunService` calcula y devuelve un resultado en memoria y la
tabla de corridas quedó fuera de este sprint.

La evidencia está en el código y en las migraciones `Phase3Purchasing`,
`Phase4Invoicing` y `Phase5Crm`; el detalle por módulo está en la sección 5 de
arc42 y en `docs/sprint2/demo-script.md`.

## 4. Desglose de HU-04 y estimación realista

### 4.1 Desglose registrado

El Sprint Planning registra HU-04 con **8 puntos** y estas tareas técnicas:

| Tarea técnica | Puntos |
|---|---:|
| Estructura de parámetros de nómina (`PayrollParameters`) | 1 |
| `PayrollEngine` con responsabilidad única (SRP) | 1 |
| Estrategias por familia de contrato (`IPayrollStrategy`) | 2 |
| Retención en la fuente (`IWithholdingCalculator`) | 1 |
| Proveedor de parámetros (`IPayrollParametersProvider`) | 1 |
| Endpoint `POST /api/nomina/calculate` protegido | 1 |
| Pruebas unitarias, pantallas y diagrama UML | 1 |
| **Total** | **8** |

### 4.2 Por qué el estimado no cubrió el trabajo real

El repositorio permite verificar estos hechos:

1. **Cuatro tipos de contrato con reglas distintas.** `ContractType` tiene
   `Indefinido`, `Término fijo`, `Obra o labor` y `Prestación de servicios`; el
   motor tiene dos estrategias porque la prestación de servicios solo aplica
   retención y no genera aportes del empleador ni provisiones. Esa diferencia no
   aparece en la tarea "calcular descuentos".
2. **La validación de parámetros ocupa un documento completo.** 
   `docs/sprint2/payroll-parameters-validation.md` tiene 296 líneas y enumera
   topes de cotización, subsidio de transporte, fondo de solidaridad, exoneración
   de salud, SENA e ICBF, riesgo ARL y tabla de retención en UVT. Cada regla es un
   caso de borde que el estimado original no desglosó.
3. **Los parámetros cambian por ley.** Salario mínimo, subsidio de transporte y
   UVT son configuración, no constantes; el código los trata como
   `PayrollParameters` y los valores por defecto son supuestos pendientes de
   validación legal.
4. **Las pruebas exigen cálculos manuales.** `PayrollEngineTests` y
   `PayrollRunServiceTests` comparan contra números calculados a mano; esa
   verificación consume tiempo que una estimación por puntos no refleja.
5. **El riesgo legal no se reduce con pruebas.** Una prueba en verde demuestra
   que el código hace lo que se programó, no que la norma esté bien aplicada.

**Lo que no se puede afirmar solo con el repositorio:** el repositorio no tiene
un registro de horas por HU del Sprint 2. Por eso no se afirma un porcentaje de desviación ni una comparación
horas-estimado; la evidencia de la subestimación son las tareas que se agregaron
durante el sprint (estrategias por contrato, retención, parámetros externos y
pruebas con cálculos manuales). Registrar las horas por HU es la acción 5 de la
sección 11.

### 4.3 Re-estimación con la regla del Sprint 3

Aplicando la regla de la sección 6 al estimado base de 8 puntos:

- Factor (a), varias variantes de caso: presente (cuatro tipos de contrato y
  múltiples topes).
- Factor (b), parámetros que cambian por ley: presente (salario mínimo, subsidio,
  UVT).
- Factor (c), dependencia de un tercero regulado: **no** presente en nómina; sí
  aplicaría a facturación electrónica DIAN.

Dos factores presentes implican multiplicador 2: `8 × 2 = 16`. La regla redondea
a la siguiente cifra de Fibonacci, por lo que la **estimación realista es 21
puntos**. El valor es una referencia de re-estimación, no una medición de horas.

## 5. SRP y DIP en `PayrollEngine`

- **SRP:** `PayrollEngine` solo valida la entrada, elige la estrategia según el
  tipo de contrato y delega. El cálculo de retención vive en
  `WithholdingCalculator`; las reglas de cada familia de contratos viven en su
  propia estrategia (`EmploymentPayrollStrategy`,
  `ServiceProvisionPayrollStrategy`); los parámetros legales viven en
  `IPayrollParametersProvider`.
- **DIP:** el motor depende solo de abstracciones (`IPayrollStrategy`,
  `IPayrollParametersProvider`, `IWithholdingCalculator`); las implementaciones se
  registran en `Program.cs`. Cambiar un parámetro legal no obliga a tocar el
  motor.

## 6. Regla de estimación para el Sprint 3 (riesgo legal/regulatorio)

Aplica a toda HU que dependa de normativa (nómina, impuestos, facturación
electrónica DIAN, datos personales):

1. **Spike obligatorio** con tiempo acotado (máximo 4 horas) **antes** del
   planning poker. Su salida es una lista de reglas, casos de borde y fuentes
   normativas por HU.
2. **Multiplicador por riesgo legal** sobre el estimado base, medido contra estos
   factores: (a) varias variantes de caso, (b) parámetros que cambian por ley,
   (c) dependencia de un tercero regulado (por ejemplo, un proveedor
   tecnológico). Un factor presente multiplica por 1,5; dos o más, por 2. Se
   redondea a la siguiente cifra de Fibonacci.
3. **Los casos de prueba salen del spike**, con la fuente de cada número, y se
   calculan a mano antes de programar.
4. **Los parámetros legales siempre externalizados** y con la fecha de vigencia
   anotada.
5. **Definition of Done ampliada:** revisión de las reglas por alguien que
   conozca la normativa, o marca explícita de "supuesto pendiente de validar".
6. **Registro obligatorio en la HU:** estimado base, factores de riesgo
   aplicados, multiplicador y resultado redondeado a Fibonacci. Una HU sin ese
   registro no entra al Sprint Planning.

## 7. Infraestructura como código y state remoto

`infra/terraform/main.tf` declara un backend `cloud` de HCP Terraform
(organización `Tienda-Flow`, workspace `tiendaflow-infra`), de modo que el state
no vive en un archivo local y todo el equipo comparte uno solo.

El workflow `.github/workflows/terraform-plan.yml` se dispara en pull requests
hacia `develop` que modifican `**/*.tf` y pasó por dos etapas (detalle en
`docs/sprint2/terraform-plan-workflow.md`):

1. **Con el recurso de ejemplo `random_pet`** ejecutaba `init`, `validate` y
   `plan` contra HCP Terraform con el secreto `TF_API_TOKEN`. El job `plan`
   quedó en verde el 11 de noviembre (corrida 42942551547).
2. **Con la infraestructura real de Azure** (App Service y Azure SQL) el `plan`
   necesita credenciales de Azure, y Azure for Students no permite crear el
   service principal que las daría a GitHub Actions. El workspace pasó a modo
   *Local* y el CI quedó en `fmt`, `init -backend=false` y `validate`; el `plan`
   y el `apply` se ejecutan en la máquina del operador con `az login`.

**Limitación vigente:** el `plan` automático en el pull request solo existió en la
etapa 1. Volver a tenerlo requiere credenciales dinámicas de HCP Terraform o un
service principal, fuera del alcance de la suscripción de estudiante.

## 8. Definition of Done

| Criterio | Estado | Evidencia |
|---|---|---|
| Cobertura de lógica de negocio mayor al 80 % | Cumplido | 95,5 % de líneas en Application + Domain (versión final, 16 de noviembre) |
| arc42 actualizado por cada HU terminada | Cumplido | Secciones 05, 07 y 10 actualizadas en el mismo PR que los módulos |
| Build sin advertencias ni errores | Cumplido | `dotnet build` con 0 advertencias y 0 errores |
| `dotnet test` en verde antes de cada commit | Cumplido | 694 pruebas en verde: 635 unitarias, 6 de aceptación y 53 de integración contra SQL Server |
| Una migración de EF Core por fase | Cumplido | De `Phase3Purchasing` a `Phase12CloseoutModules` |
| Nada de datos personales en logs | Cumplido | La auditoría guarda ids, estados, conteos y totales |
| Pull request revisado por un compañero | **Pendiente** | Los PR #2 a #8 se fusionaron sin una revisión aprobada en GitHub; GitHub no permite que el autor apruebe su propio PR |

## 9. Qué se puede mejorar

- La complejidad de HU-04 (nómina) fue subestimada: el planning poker no
  consideró suficientes casos de borde legales.
- Se asumió demasiado alcance para el equipo disponible (CRUD de cinco módulos).
- La integración continua no ejecuta las pruebas de integración (no hay SQL
  Server en GitHub Actions), por lo que parte de la cobertura solo se verifica en
  local.

## 10. Impedimentos

- La implementación se concentró en un integrante mientras el resto del equipo
  atendía otras asignaturas, lo que dejó pendiente la revisión cruzada de código.
- La herramienta de asistencia de código alcanzó su límite diario y el trabajo
  continuó a mano.
- La fecha de entrega (antes del 11 de noviembre de 2026) dejó poco margen para
  CRM, Facturación y la interfaz de Compras y RR. HH.

## 11. Lecciones aprendidas

El proyecto termina con este sprint, así que estas lecciones quedan como cierre
y no como acciones de un sprint siguiente:

1. **Investigar antes de estimar** toda HU con complejidad legal o regulatoria
   (nómina, impuestos, facturación electrónica), con la regla de la sección 6.
2. **Infraestructura como código:** el `terraform plan` automático necesita
   credenciales de nube desde el principio; sin ellas, el CI solo valida.
3. **Reparto y revisión:** asignar trabajo a todos los integrantes desde el
   Sprint Planning y exigir la revisión de cada pull request por un compañero.
4. **Pruebas con varios usuarios:** los errores de concurrencia solo
   aparecieron al probar con varias sesiones a la vez.
5. **Registrar horas por HU** para poder comparar estimado y real.

## 12. Cierre de la entrega (16 de noviembre de 2026)

Antes de la entrega se revisó el backend y el frontend de `feature/erp-complete`.
La revisión y sus correcciones llegaron a `main` con los PR #6, #7 y #8
(commit `43997af`, etiqueta `sprint-2`):

- **Stock:** dos sesiones de Blazor podían perder ventas porque el `DbContext`
  de cada pestaña devolvía datos en caché. Se corrigió con lectura actualizada y
  una columna `rowversion` en `stock`.
- **Seguridad:** siempre queda al menos un administrador activo y el inicio de
  sesión limita los intentos por IP.
- **Ventas:** la venta del punto de venta se guarda como documento y la cuentan
  el panel, el reporte de ventas y la reposición.
- **Facturas:** descuentan stock al emitirse, lo devuelven al anularse y ya no se
  editan después de emitidas.
- **HU-05:** el comprobante de la venta avisa cuando el producto queda en stock
  crítico.

Con el cierre del proyecto se implementaron también las funciones que los
permisos reservaban: venta POS con varios productos, devoluciones (notas
crédito), respuestas `401`/`403` en `/api`, matriz de roles editable, reporte de
seguridad, contratos, asistencia, turnos, corrida de nómina guardada, puntos de
fidelización, PQRS y campañas (migración `Phase12CloseoutModules`). La factura
electrónica real ante la DIAN queda fuera del alcance.

Detalle y pruebas en `docs/final/testing.md`. Quedan pendientes de operación la
revisión cruzada de los PR y la publicación de `main` en Azure con las
migraciones hasta `Phase12CloseoutModules`.
