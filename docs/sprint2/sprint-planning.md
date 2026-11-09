# Sprint 2 - Sprint Planning - TiendaFlow ERP

## 1. Sprint Goal

Al final del Sprint 2 habremos implementado el CRUD de cada módulo del ERP
(Proveedores, Inventario, CRM, Talento Humano y Facturación), el motor de
cálculo de nómina (RR. HH.) y el control de stock crítico (Inventario),
integrando la automatización de la infraestructura base y documentando las
decisiones de arquitectura de seguridad en arc42.

## 2. Capacidad y Definition of Done

**Capacidad.** Un solo desarrollador (Samuel Castellanos Rivera) a cargo de la
implementación; el resto del equipo está dedicado a otras asignaturas. Fecha
límite: antes del 11 de noviembre de 2026.
Horas disponibles en la ventana final: **[COMPLETAR: horas reales]**.

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
| (Pendiente) Persistencia y pantallas de empleados y corridas de nómina | Por hacer |

### HU-05 Alerta de stock crítico (5)

| Tarea técnica | Estado |
|---|---|
| Regla de mínimo por producto y alerta al bajar del umbral | Hecho |
| Panel de stock crítico y página de alertas | Hecho |
| Verificar el flujo de demo: vender hasta bajar del mínimo y ver la alerta | **[COMPLETAR]** |

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

## 5. Respuestas a la retroalimentación de la planificación

**Definición del QUÉ.** El objetivo es medible por módulo, pero fue demasiado
amplio: incluye cinco CRUD, un motor de nómina, control de stock crítico,
infraestructura y documentación en un solo sprint con un solo desarrollador.
Las cuatro HU seleccionadas sí contribuyen al objetivo; los CRUD completos de
todos los módulos fueron lo menos alcanzable.

**Definición del CÓMO.** La descomposición de HU-04 en tareas técnicas
(parámetros, motor, lógica, endpoint, pruebas, diagrama) fue lo bastante
granular para que una sola persona las ejecutara en orden. Lo que no se
desglosó fueron los casos de borde legales (tipos de contrato, topes de
cotización, retención), que es donde se fue el tiempo.

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

**Terraform con backend remoto.** **[COMPLETAR cuando se implemente:** el state
se guarda en un backend remoto para trabajo en grupo sin conflictos**]**.

**Adherencia al DoD.** Cobertura de pruebas unitarias medida:
**[COMPLETAR: % real]**. arc42 actualizado para HU-04, HU-06 y HU-08. Pull
requests de las HU terminadas: **[COMPLETAR]**.