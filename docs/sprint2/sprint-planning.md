# Sprint 2 - Sprint Planning - TiendaFlow ERP

## 1. Sprint Goal

Al final del Sprint 2 habremos implementado el CRUD de cada módulo del ERP
(Proveedores, Inventario, CRM, Talento Humano y Facturación), el motor de
cálculo de nómina (RR. HH.) y el control de stock crítico (Inventario),
integrando la automatización de la infraestructura base y documentando las
decisiones de arquitectura de seguridad en arc42.

## 2. Capacidad y Definition of Done

**Capacidad.** Equipo de 4 integrantes. Fecha límite: antes del 11 de
noviembre de 2026. En este sprint la implementación se concentró en Samuel
Castellanos Rivera, mientras el resto del equipo atendía otras asignaturas.
Las horas se estiman para la ventana final (9 al 11 de noviembre),
descontando clases y entregas de otras asignaturas.

> Nota: tabla de ejemplo del formato. Los datos de los demás integrantes se
> completan con lo que cada uno confirme.

| Integrante | Horas disponibles | Rol en el sprint |
|---|---|---|
| Samuel Castellanos Rivera | 6,5 h | Hecho: nómina, seguridad, Compras, pruebas, arc42, Terraform, CI y CRUD de RR. HH. Asignado: entrega final |
| Jonathan Alejandro Yacuma Rivera | 6 h | Asignado: arc42 a Notion y video del tour de arquitectura |
| Juan Sebastián Rodríguez Blanco | 5,5 h | Asignado: revisión del PR #2 y ejecución de la demo manual |
| Sergio Steeven Moreno Forero | 6 h | Asignado: grabar el video del tour de arquitectura y revisar el PR #2 |

| Tarea | Responsable | Estimado | Estado |
|---|---|---|---|
| Pasar `docs/arc42/` (secciones 5, 7 y 10) a Notion | Samuel | 1 h | Por hacer |
| Grabar el video de 1 minuto con `video-guide.md` | Sergio | 1 h | Por hacer |
| Revisar y aprobar el PR #2 | Sergio | 0,5 h | Por hacer |
| Ejecutar la demo de HU-04, HU-05 y HU-06 | Samuel | 1 h | Por hacer |
| Validar los parámetros de nómina contra la normativa (borrador del asistente en `docs/sprint2/payroll-parameters-validation.md`) | Samuel | 1 h | Por hacer |
| Revisión humana de la retrospectiva | Samuel | 0,5 h | Por hacer |
| CRUD de RR. HH. (código y pruebas) | Samuel | 4 h | Hecho |
| Capturas de evidencia y entrega en el aula virtual | Samuel | 1 h | Por hacer |

**Definition of Done (vigente).**
- Pruebas unitarias con cobertura de la lógica de negocio mayor al 80 %.
- Documentación arc42 actualizada por cada HU terminada.
- `dotnet build` sin advertencias ni errores y `dotnet test` en verde antes de
  cada commit; CI de GitHub Actions en verde.
- Una migración de EF Core por fase; nada de datos personales en logs.
- Pull request revisado y commits pequeños en inglés.

## 3. Historias seleccionadas

| HU | Módulo | Descripción | Puntos |
|---|---|---|---|
| HU-04 | RR. HH. / Nómina | Calcular la nómina mensual aplicando deducciones y parafiscales según el tipo de contrato | 8 |
| HU-05 | Inventario | Alerta automática cuando el stock baja del mínimo | 5 |
| HU-06 | Seguridad | Asignación de roles y control de acceso por módulo | 5 |
| HU-08 | Documentación | arc42 secciones 7 y 10 con el modelo de roles | 3 |

## 4. Sprint Backlog (tablero Kanban)

### HU-04 Cálculo de nómina (8)

| Tarea técnica | Estado |
|---|---|
| (Backend) Estructura de datos de parámetros de nómina (`PayrollParameters`) | Hecho |
| (Backend) `PayrollEngine` con responsabilidad única (SRP) | Hecho |
| (Backend) Lógica de aportes y deducciones tras abstracciones (DIP): estrategias, `IWithholdingCalculator`, `IPayrollParametersProvider` | Hecho |
| (API) Endpoint `POST /api/nomina/calculate` protegido por `hr.payroll.manage` | Hecho |
| (Pruebas) Pruebas unitarias del motor (`PayrollEngineTests`) | Hecho |
| (Documentación) Diagrama de secuencia UML (`docs/uml/payroll_sequence.puml`) | Hecho |
| (Pantallas) Empleados y corrida de nómina sin persistencia | Hecho |
| (Persistencia) Guardar las corridas de nómina | Por hacer |

### HU-05 Alerta de stock crítico (5)

| Tarea técnica | Estado |
|---|---|
| Regla de mínimo por producto y alerta al bajar del umbral | Hecho |
| Panel de stock crítico y página de alertas | Hecho |
| Verificar manualmente la demo: vender hasta bajar del mínimo y ver la alerta | Hecho |

### HU-06 Asignación de roles (5)

| Tarea técnica | Estado |
|---|---|
| 7 roles, catálogo de permisos y matriz central `RolePermissions` | Hecho |
| Autenticación con cookie y contraseñas con hash | Hecho |
| Autorización en tres niveles (página, UI, servicio) y auditoría de accesos denegados | Hecho |
| Pantallas `/seguridad/usuarios`, `/seguridad/roles`, `/seguridad/auditoria` | Hecho |

### HU-08 Documentación de seguridad y despliegue (3)

| Tarea técnica | Estado |
|---|---|
| arc42 sección 5 (incluye componente de nómina) | Hecho |
| arc42 sección 7 (vista de despliegue) | Hecho |
| arc42 sección 10 (seguridad y roles, matriz) | Hecho |

### Infraestructura como código (Tarea 3 de la guía)

| Tarea técnica | Estado |
|---|---|
| Terraform con backend remoto en HCP Terraform (`infra/terraform/main.tf`) | Hecho |
| Workflow de GitHub Actions que ejecuta `terraform plan` en PR a `develop` con cambios en `.tf` | Hecho |

## 5. Respuestas a la retroalimentación de la planificación

**Definición del QUÉ.** El objetivo es medible por módulo, pero fue demasiado
amplio: incluye cinco CRUD, un motor de nómina, control de stock crítico,
infraestructura y documentación en un solo sprint. Las cuatro HU seleccionadas
sí contribuyen al objetivo; los CRUD completos de todos los módulos fueron lo
menos alcanzable.

**Definición del CÓMO.** La descomposición de HU-04 en tareas técnicas
(parámetros, motor, lógica, endpoint, pruebas, diagrama) fue lo bastante
granular para ejecutarse en orden. Lo que no se desglosó fueron los casos de
borde legales (tipos de contrato, topes de cotización, retención), que es donde
se fue el tiempo.

**Estimación y compromiso.** Los 8 puntos de HU-04 no fueron realistas por la
complejidad legal (ver retrospectiva). El equipo seleccionó las HU y el
backlog; no hubo asignación impuesta por el PO. HU-07 (IaC) no estaba en la
tabla de selección aunque la guía la menciona, lo que dejó su estimación sin
definir.

## 6. Respuestas a la retroalimentación de ejecución y arquitectura

**SOLID en `PayrollEngine`.**
- **SRP:** `PayrollEngine` solo valida la entrada, elige la estrategia según el
  tipo de contrato y delega. El cálculo de retención vive en
  `WithholdingCalculator` y las reglas de cada familia de contratos en su
  propia estrategia.
- **DIP:** el motor depende solo de abstracciones (`IPayrollStrategy`,
  `IPayrollParametersProvider`, `IWithholdingCalculator`); las implementaciones
  se registran en `Program.cs`. Los parámetros legales no están escritos en la
  lógica y se pueden cambiar sin tocar el motor.

**Terraform con backend remoto.** Sí. El state se guarda en HCP Terraform
(organization `Tienda-Flow`, workspace `tiendaflow-infra`), de modo que el
equipo comparte un único state sin conflictos de archivos locales. Un workflow
de GitHub Actions (`.github/workflows/terraform-plan.yml`) ejecuta
`terraform init`, `validate` y `plan` en cada PR hacia `develop` que modifique
archivos `.tf`; se verificó en el PR #2 con resultado exitoso. Limitación: el
recurso declarado es de relleno (`random_pet`) y no describe infraestructura
real; la definición de la infraestructura de despliegue queda para el Sprint 3.

**Adherencia al DoD.** Cobertura de líneas medida sobre `TiendaFlow.Application`
y `TiendaFlow.Domain`: **85,6 %** (1.311 de 1.530 líneas), por encima del 80 %
exigido; cobertura de ramas 68,5 %. Las 17 pruebas de integración requieren
SQL Server y se saltan en CI, por lo que su cobertura solo se verifica en
local. arc42 actualizado para HU-04, HU-06 y HU-08 (secciones 5, 7 y 10). Los
cambios viajan en el PR #2 hacia `develop`, con CI y Terraform Plan en verde.
La revisión cruzada del PR #2 está pendiente.