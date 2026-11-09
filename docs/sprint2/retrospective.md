# Sprint 2 - Retrospectiva - TiendaFlow ERP

## Qué salió bien

- La arquitectura por capas (Domain, Application, Infrastructure, Web) y la
  regla de que Domain y Application no referencian EF Core se mantuvieron.
- La seguridad quedó completa: 7 roles, matriz central, autorización en tres
  niveles y auditoría, verificada de extremo a extremo.
- El motor de nómina quedó aislado tras interfaces, con pruebas unitarias de
  números calculados a mano.
- La documentación arc42 se produjo junto con el código.

## Qué se puede mejorar

- La complejidad de HU-04 (nómina) fue subestimada: el planning poker no
  consideró suficientes casos de borde.
- Se asumió demasiado alcance para una sola persona (CRUD de cinco módulos).
- La integración continua no ejecuta las pruebas de integración (no hay SQL
  Server en GitHub Actions), por lo que parte de la cobertura solo se verifica
  en local.

## Impedimentos

- El equipo estuvo ocupado con otras asignaturas y la implementación recayó en
  una sola persona.
- La herramienta de asistencia de código usada alcanzó su límite diario
  gratuito y el trabajo tuvo que continuar a mano.
- La fecha de entrega (antes del 11 de noviembre) dejó poco margen para
  CRM, Facturación y la interfaz de Compras y RR. HH.

## Acciones de mejora para el Sprint 3

1. **Spike antes de estimar** toda HU con complejidad legal o regulatoria
   (nómina, impuestos, facturación electrónica), con la regla de abajo.
2. **IaC:** pipeline básico de GitHub Actions para que `terraform plan` corra
   automáticamente en cada PR a `develop` que modifique archivos `.tf`.

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