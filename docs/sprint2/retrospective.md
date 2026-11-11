# Sprint 2 - Retrospectiva - TiendaFlow ERP

**Fecha de cierre:** 10 de noviembre de 2026.

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

- **Pruebas:** 296 pruebas activas en verde (290 unitarias y 6 de aceptación) y 17
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
- **Terraform:** el workflow `.github/workflows/terraform-plan.yml` define
  `init`, `validate` y `plan` con estado remoto en HCP Terraform; el recurso
  actual es un *placeholder* declarado. Esta revisión no ejecutó `terraform
  plan` real.
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

**Lo que no se puede afirmar solo con el repositorio:** las horas reales
invertidas en HU-04 no están registradas en ningún archivo del proyecto. Por eso
no se afirma un porcentaje de desviación ni una comparación horas-estimado.
**[CONFIRMAR POR EL EQUIPO]** si existe un registro de tiempo en otra herramienta.

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
no vive en un archivo local. El workflow `.github/workflows/terraform-plan.yml`
se dispara en pull requests hacia `develop` que modifican `**/*.tf`, ejecuta
`terraform init -input=false`, `validate` y `plan -input=false` en
`infra/terraform`, y toma el token de HCP Terraform desde el secreto
`TF_API_TOKEN`.

**Limitación verificable:** el backend está declarado, pero esta entrega **no**
creó el workspace, no configuró variables y no migró un state local. El workflow
tampoco se ejecutó en GitHub Actions durante esta revisión; no hay evidencia en el
repositorio de una corrida exitosa. `terraform apply` no forma parte del workflow.

## 8. Definition of Done

| Criterio | Estado | Evidencia |
|---|---|---|
| Cobertura de lógica de negocio mayor al 80 % | Cumplido | 88,4 % de líneas en Application + Domain |
| arc42 actualizado por cada HU terminada | Cumplido | Secciones 05 y 10 actualizadas en el mismo PR que los módulos |
| Build sin advertencias ni errores | Cumplido | `dotnet build` con 0 advertencias y 0 errores |
| `dotnet test` en verde antes de cada commit | Cumplido | 290 unitarias + 6 de aceptación en verde |
| Una migración de EF Core por fase | Cumplido | `Phase3Purchasing`, `Phase4Invoicing`, `Phase5Crm` |
| Nada de datos personales en logs | Cumplido | La auditoría de RR. HH. guarda ids, estados y conteos; CRM y facturación guardan ids y totales |
| Pull request revisado por un compañero | **Pendiente** | La revisión cruzada no se completó antes de esta entrega |

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

## 11. Acciones de mejora para el Sprint 3

1. **Spike antes de estimar** toda HU con complejidad legal o regulatoria
   (nómina, impuestos, facturación electrónica), con la regla de la sección 6.
2. **IaC:** mantener el pipeline de GitHub Actions que ejecuta `terraform plan`
   en cada PR a `develop` que modifique archivos `.tf`, y aprovisionar el
   workspace de HCP Terraform antes de intentar un `apply`.
3. **Reparto de trabajo:** asignar desde el Sprint Planning al menos una HU o
   tarea por integrante y exigir la revisión de cada PR por un compañero.
4. **Cerrar la brecha de cobertura de integración:** definir una base SQL Server
   efímera en CI o un contenedor de servicio para ejecutar las 17 pruebas
   omitidas.
