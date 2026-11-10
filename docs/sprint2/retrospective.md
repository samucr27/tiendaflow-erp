# Sprint 2 - Retrospectiva - TiendaFlow ERP

## Qué salió bien

- La arquitectura por capas (Domain, Application, Infrastructure, Web) y la
  regla de que Domain y Application no referencian EF Core se mantuvieron.
- La seguridad quedó implementada con 7 roles, matriz central, autorización en
  tres niveles y auditoría de accesos cubierta por pruebas automatizadas.
  Quedan decisiones abiertas de privacidad (el correo del intento fallido de
  inicio de sesión) y una desalineación latente de permisos en alertas,
  registradas en `docs/sprint2/open-questions.md`.
- El motor de nómina quedó aislado tras interfaces, con pruebas unitarias de
  números calculados a mano.
- La documentación arc42 se produjo junto con el código.

## Resultados verificables del Sprint 2

- **Cobertura:** 87,6 % de líneas en Application + Domain al cierre (línea base:
  85,6 %), medida con coverlet y ReportGenerator.
- **Pruebas:** la línea base tenía 213 pruebas (190 unitarias y 6 de aceptación
  en verde, 17 de integración omitidas). La Fase 1 sumó 54 pruebas unitarias:
  al cierre hay 250 pruebas activas en verde (244 unitarias y 6 de aceptación)
  y las mismas 17 de integración omitidas por falta de SQL Server en CI.
- **CI:** el workflow `.github/workflows/ci.yml` ejecuta restore, build y
  `dotnet test` con cobertura en cada push; las pruebas de integración se omiten
  porque el runner no tiene SQL Server. Esta revisión no consultó el resultado
  remoto de GitHub Actions.
- **Terraform:** el workflow `.github/workflows/terraform-plan.yml` define
  `init`, `validate` y `plan` con estado remoto en HCP Terraform; el recurso
  actual es un *placeholder* declarado. Esta revisión no ejecutó `terraform
  plan` real.
- **Fase 1:** CRUD de empleados con enmascaramiento de salario, corrida de
  nómina por empleado activo, auditoría con módulo `hr` y pruebas unitarias de
  ambas piezas.
- **Documentación:** arc42 (secciones 05, 07 y 10), validación de parámetros de
  nómina, guion de demo, revisión de código y paquete para Notion.

## Qué se puede mejorar

- La complejidad de HU-04 (nómina) fue subestimada: el planning poker no
  consideró suficientes casos de borde.
- Se asumió demasiado alcance para el equipo disponible (CRUD de cinco módulos).
- La integración continua no ejecuta las pruebas de integración (no hay SQL
  Server en GitHub Actions), por lo que parte de la cobertura solo se verifica
  en local.

## Impedimentos

- La implementación se concentró en un integrante mientras el resto del equipo
  atendía otras asignaturas, lo que dejó pendiente la revisión cruzada de código.
- La herramienta de asistencia de código usada alcanzó su límite diario
  gratuito y el trabajo tuvo que continuar a mano.
- La fecha de entrega (antes del 11 de noviembre) dejó poco margen para
  CRM, Facturación y la interfaz de Compras y RR. HH.

## Acciones de mejora para el Sprint 3

1. **Spike antes de estimar** toda HU con complejidad legal o regulatoria
   (nómina, impuestos, facturación electrónica), con la regla de abajo.
2. **IaC:** pipeline básico de GitHub Actions para que `terraform plan` corra
   automáticamente en cada PR a `develop` que modifique archivos `.tf`.
3. **Reparto de trabajo:** asignar desde el Sprint Planning al menos una HU o
   tarea por integrante y exigir la revisión de cada PR por un compañero.

## Tarea 1: por qué HU-04 fue subestimada

1. **Muchos tipos de contrato y casos de borde.** Término indefinido, fijo,
   obra o labor y prestación de servicios siguen reglas distintas: la
   prestación de servicios solo lleva retención y no genera aportes del
   empleador ni provisiones, mientras que los laborales sí. Cada familia
   necesitó su propia estrategia y sus propias pruebas.
2. **Reglas legales con muchos topes y condiciones.** Base de cotización con
   piso y tope, subsidio de transporte hasta cierto salario, fondo de
   solidaridad por tramos, exoneración de salud, SENA e ICBF según el salario,
   riesgo ARL por clase y tabla de retención en UVT. El estimado inicial vio
   "calcular descuentos", no este conjunto de condiciones.
3. **Parámetros que cambian por ley.** Salario mínimo, subsidio de transporte
   y UVT cambian cada año. Esto obligó a diseñar los parámetros como
   configuración y no como constantes, y los valores por defecto del código
   son supuestos que deben validarse contra la normativa vigente.
4. **Necesidad de números verificados a mano.** Las pruebas exigen cálculos
   hechos a mano con parámetros redondos para poder detectar errores, y esa
   verificación consume tiempo que no aparece en una estimación por puntos.
5. **Validación legal fuera del código.** El resultado de una prueba en verde
   no demuestra que la norma esté bien aplicada; el riesgo legal no se reduce
   con pruebas unitarias.

## Regla de estimación para el Sprint 3 (riesgo legal/regulatorio)

Aplica a toda HU que dependa de normativa (nómina, impuestos, facturación
electrónica DIAN, datos personales):

1. **Spike obligatorio** con tiempo acotado (máximo 4 horas) **antes** del
   planning poker. Su salida es una lista de reglas, casos de borde y fuentes
   normativas por HU.
2. **Multiplicador por riesgo legal** sobre el estimado base, medido contra
   estos factores: (a) varias variantes de caso, (b) parámetros que cambian por
   ley, (c) dependencia de un tercero regulado (por ejemplo, un proveedor
   tecnológico). Un factor presente multiplica por 1,5; dos o más, por 2.
   Se redondea a la siguiente cifra de Fibonacci.
3. **Los casos de prueba salen del spike**, con la fuente de cada número, y se
   calculan a mano antes de programar.
4. **Los parámetros legales siempre externalizados** y con la fecha de
   vigencia anotada.
5. **Definition of Done ampliada:** revisión de las reglas por alguien que
   conozca la normativa, o marca explícita de "supuesto pendiente de validar".
6. **Registro obligatorio en la HU:** estimado base, factores de riesgo
   aplicados, multiplicador y resultado redondeado a Fibonacci. Una HU sin ese
   registro no entra al Sprint Planning.