# Validación de parámetros de nómina — Sprint 2

**Fecha de consulta:** 9 de noviembre de 2026
**Rama:** `feature/opencode-erp-ui`
**Archivo revisado:** `src/TiendaFlow.Application/Payroll/PayrollParameters.cs`
**Alcance:** documentar y contrastar los valores; **no se modificó ningún valor del código** en esta fase.

> Este documento sustenta la advertencia visible de la pantalla `/rrhh/nomina`:
> «Los valores usan parámetros de referencia pendientes de validación legal».
> Es un ejercicio académico y no reemplaza la revisión de un contador o de un
> abogado laboral antes de usar el motor en producción.

## 1. Método

1. Se leyó el archivo completo de parámetros, incluidas las provisiones y la tabla de retención.
2. Se contrastó cada valor con la norma general que lo define y con una fuente oficial
   (Ministerio del Trabajo, DIAN, Ministerio de Salud/UGPP, ARL o Código Sustantivo del Trabajo).
3. Se marcó **Verificado** cuando el valor coincide con la fuente consultada.
   Se marcó **Discrepancia** cuando el valor del código no coincide con la regla vigente,
   indicando el valor o modelo correcto propuesto.
4. Las fuentes se consultaron el 9 de noviembre de 2026. Los enlaces completos están en la sección 6.

**Resumen:** 26 parámetros revisados: **23 verificados** y **3 discrepancias**.
Las discrepancias no rompen los cálculos de los tres casos de prueba de la sección 4,
pero sí afectan salarios altos y contratos de prestación de servicios de no declarantes.

## 2. Tabla consolidada de parámetros

### 2.1 Valores de referencia

| Parámetro | Valor actual en código | Norma que lo define | Fuente oficial a consultar | Estado |
|---|---:|---|---|---|
| `MinimumWage` | $1.750.905 | Salario mínimo legal mensual vigente 2026 (Decreto 1469 de 2025). El Decreto 0159 de 2026 mantuvo transitoriamente el mismo valor tras la suspensión provisional. | Ministerio del Trabajo — Decreto 1469 del 29-12-2025 | **Verificado** — Decreto 1469 de 2025, consultado 09-11-2026 |
| `TransportSubsidy` | $249.095 | Auxilio de transporte 2026 (Decreto 1470 de 2025), para quienes devengan hasta 2 SMMLV. | Ministerio del Trabajo — Decreto 1470 del 29-12-2025 | **Verificado** — Decreto 1470 de 2025, consultado 09-11-2026 |
| `TransportSubsidyLimitInMinimumWages` | 2 | Límite de 2 SMMLV para tener derecho al auxilio de transporte (Decreto 1470 de 2025). | Ministerio del Trabajo — Decreto 1470 de 2025 | **Verificado** — el código usa `<= 2 SMMLV`, que coincide con «hasta dos salarios mínimos» |
| `Uvt` | $52.374 | Unidad de Valor Tributario 2026 (Resolución DIAN 000238 del 15-12-2025). | DIAN — Resolución 000238 de 2025 | **Verificado** — DIAN, consultado 09-11-2026 |
| `MaxContributionBaseInMinimumWages` | 25 | Límite de la base de cotización a seguridad social: 25 SMMLV (Ley 100 de 1993, art. 18). | Ministerio de Salud / UGPP — Ley 100 de 1993 | **Verificado** — regla general de 25 SMMLV |

### 2.2 Deducciones del empleado

| Parámetro | Valor actual en código | Norma que lo define | Fuente oficial a consultar | Estado |
|---|---:|---|---|---|
| `EmployeeHealthRate` | 4 % | Aporte del empleado al régimen contributivo de salud (Ley 100 de 1993, art. 204). | Ministerio de Salud / UGPP — Ley 100 de 1993 | **Verificado** — 4 % a cargo del empleado |
| `EmployeePensionRate` | 4 % | Aporte del empleado al sistema general de pensiones (Ley 100 de 1993, art. 20). | Ministerio de Salud / UGPP — Ley 100 de 1993 | **Verificado** — 4 % a cargo del empleado |
| `SolidarityFundThresholdInMinimumWages` | 4 | Aporte adicional al Fondo de Solidaridad Pensional desde 4 SMMLV de ingreso base de cotización (Ley 100 de 1993 y Ley 797 de 2003, art. 8). | Ministerio de Salud / UGPP — Ley 797 de 2003 | **Verificado** — el umbral de 4 SMMLV es correcto |
| `SolidarityFundRate` | 1 % | Aporte adicional al Fondo de Solidaridad Pensional: 1 % entre 4 y menos de 16 SMMLV; luego 1,2 % (16–17), 1,4 % (17–18), 1,6 % (18–19), 1,8 % (19–20) y 2 % (20 o más) (Ley 797 de 2003, art. 8). | Ministerio de Salud / UGPP — Ley 797 de 2003 | **Discrepancia** — el código aplica 1 % plano a todo IBC ≥ 4 SMMLV. Valor correcto propuesto: escala graduada 1 % / 1,2 % / 1,4 % / 1,6 % / 1,8 % / 2 % según el tramo. No se modificó el código. |

### 2.3 Aportes del empleador

| Parámetro | Valor actual en código | Norma que lo define | Fuente oficial a consultar | Estado |
|---|---:|---|---|---|
| `EmployerHealthRate` | 8,5 % | Aporte del empleador al régimen contributivo de salud (Ley 100 de 1993, art. 204). | Ministerio de Salud / UGPP — Ley 100 de 1993 | **Verificado** — 8,5 % a cargo del empleador |
| `EmployerPensionRate` | 12 % | Aporte del empleador al sistema general de pensiones (Ley 100 de 1993, art. 20). | Ministerio de Salud / UGPP — Ley 100 de 1993 | **Verificado** — 12 % a cargo del empleador |
| `ArlRates` | Clase 1: 0,522 %; clase 2: 1,044 %; clase 3: 2,436 %; clase 4: 4,350 %; clase 5: 6,960 % | Tarifas de cotización al Sistema General de Riesgos Laborales por clase de riesgo (Decreto 1771 de 1994, que desarrolla el Decreto 1295 de 1994). | ARL / Ministerio del Trabajo — Decreto 1771 de 1994 | **Verificado** — las cinco tarifas coinciden |
| `CompensationBoxRate` | 4 % | Aporte parafiscal a las Cajas de Compensación Familiar (Ley 21 de 1982). | Ministerio del Trabajo / UGPP — Ley 21 de 1982 | **Verificado** — 4 % sobre la nómina salarial |
| `SenaRate` | 2 % | Aporte parafiscal al SENA (Ley 21 de 1982). | Ministerio del Trabajo / UGPP — Ley 21 de 1982 | **Verificado** — 2 % sobre la nómina salarial |
| `IcbfRate` | 3 % | Aporte parafiscal al ICBF (Ley 21 de 1982). | Ministerio del Trabajo / UGPP — Ley 21 de 1982 | **Verificado** — 3 % sobre la nómina salarial |
| `ApplyExoneration` | `true` | Exoneración de aportes del empleador a salud, SENA e ICBF para trabajadores que devenguen menos de 10 SMMLV (Estatuto Tributario, art. 114-1, incorporado por la Ley 1819 de 2016). | Ministerio de Salud / UGPP — Estatuto Tributario art. 114-1 | **Verificado** — la exoneración no cobija pensión ni ARL ni caja |
| `ExonerationLimitInMinimumWages` | 10 | Límite de 10 SMMLV para la exoneración de aportes (Estatuto Tributario, art. 114-1). | Ministerio de Salud / UGPP — Estatuto Tributario art. 114-1 | **Verificado** — el código usa `< 10 SMMLV`, que coincide con «menos de 10» |

### 2.4 Provisiones sociales (valor mensual como fracción de la base)

| Parámetro | Valor actual en código | Norma que lo define | Fuente oficial a consultar | Estado |
|---|---:|---|---|---|
| `SeveranceRate` | 8,33 % | Cesantías: un mes de salario por año (Código Sustantivo del Trabajo, art. 249). La provisión mensual es 1/12 = 8,333… %. | Ministerio del Trabajo — CST | **Verificado con aproximación** — el código usa 0,0833 en lugar de 1/12; la diferencia es de centavos por mes |
| `SeveranceInterestRate` | 1 % | Intereses sobre cesantías: 12 % anual sobre el saldo de cesantías (CST y normas que lo desarrollan). La provisión mensual aproximada es 1 %. | Ministerio del Trabajo — CST | **Verificado con simplificación** — el interés real se causa día a día sobre el saldo acumulado, no exactamente 1 % del salario mensual |
| `ServiceBonusRate` | 8,33 % | Prima de servicios: 30 días de salario por año, pagada en dos cuotas (CST, art. 306). La provisión mensual es 8,333… %. | Ministerio del Trabajo — CST | **Verificado con aproximación** — el código usa 0,0833 |
| `VacationRate` | 4,17 % | Vacaciones: 15 días hábiles de descanso remunerado por año (CST, art. 186). La provisión mensual es 15/360 = 4,1666… %. | Ministerio del Trabajo — CST | **Verificado con aproximación** — el código usa 0,0417 y excluye el auxilio de transporte de la base, lo cual es correcto |

### 2.5 Retención en la fuente del empleado

| Parámetro | Valor actual en código | Norma que lo define | Fuente oficial a consultar | Estado |
|---|---:|---|---|---|
| `ExemptIncomeRate` | 25 % | Renta exenta del 25 % del ingreso laboral (Estatuto Tributario, art. 206, numeral 10). | DIAN — Estatuto Tributario art. 206 | **Verificado con simplificación** — el código aplica el 25 % sobre el ingreso neto de aportes y prorratea el tope anual por mes |
| `ExemptIncomeAnnualCapUvt` | 790 UVT | Tope anual de la renta exenta: 790 UVT (Estatuto Tributario, art. 206, numeral 10). | DIAN — Estatuto Tributario art. 206 | **Verificado** — el tope mensual usado por el código es 790/12 × UVT |
| `WithholdingBrackets` | 7 tramos (ver tabla 2.5.1) | Tabla de retención en la fuente por rentas de trabajo (Estatuto Tributario, art. 383). | DIAN — Estatuto Tributario art. 383 | **Verificado** — tramos, tarifas marginales y valores adicionales coinciden |

#### 2.5.1 Tramos de `WithholdingBrackets`

| Desde (UVT) | Hasta (UVT) | Tarifa marginal | Adicional (UVT) | Estado |
|---:|---:|---:|---:|---|
| 0 | 95 | 0 % | 0 | Verificado |
| 95 | 150 | 19 % | 0 | Verificado |
| 150 | 360 | 28 % | 10 | Verificado |
| 360 | 640 | 33 % | 69 | Verificado |
| 640 | 945 | 35 % | 162 | Verificado |
| 945 | 2.300 | 37 % | 268 | Verificado |
| 2.300 | En adelante | 39 % | 770 | Verificado |

### 2.6 Contratos de prestación de servicios

| Parámetro | Valor actual en código | Norma que lo define | Fuente oficial a consultar | Estado |
|---|---:|---|---|---|
| `ServiceProvisionWithholdingRate` | 4 % | Retención en la fuente por servicios en general: 4 % para declarantes de renta y 6 % para no declarantes (Decreto 1625 de 2016, arts. 1.2.4.4.14 y 1.2.4.4.1, modificados por el Decreto 572 de 2025, art. 2). | DIAN — Decreto 1625 de 2016 y Decreto 572 de 2025 | **Discrepancia** — el código aplica 4 % plano. Valor correcto propuesto: 4 % para declarante y 6 % para no declarante, con un indicador de declarante en la entrada o dos tarifas separadas. No se modificó el código. |
| `ServiceProvisionWithholdingMinimumUvt` | 4 UVT | Base mínima individual para practicar retención por servicios: 2 UVT desde el 1 de julio de 2026 (Decreto 572 de 2025, art. 2). | DIAN — Decreto 572 de 2025; Consejo de Estado, exp. 30229, auto del 02-06-2026 | **Discrepancia** — el código usa 4 UVT (regla anterior). Valor correcto propuesto: 2 UVT ($104.748 en 2026). No se modificó el código. |

> **Nota de vigencia del Decreto 572 de 2025:** el decreto fue suspendido
> provisionalmente entre el 8 de mayo y el 30 de junio de 2026. El Consejo de
> Estado revocó la suspensión mediante auto del 2 de junio de 2026 (exp. 30229)
> y las reglas del decreto volvieron a aplicar desde el **1 de julio de 2026**.
> Por eso la validación usa la base de 2 UVT y las tarifas 4 % / 6 %.

## 3. Discrepancias y propuesta (sin cambiar el código)

| # | Parámetro | Valor actual | Valor o modelo propuesto | Impacto | Acción sugerida |
|---:|---|---|---|---|---|
| 1 | `SolidarityFundRate` | 1 % plano para IBC ≥ 4 SMMLV | Escala: 1 % (4 a <16 SMMLV), 1,2 % (16–17), 1,4 % (17–18), 1,6 % (18–19), 1,8 % (19–20), 2 % (≥20) | Subestima el aporte de salarios altos. Con 30 SMMLV, el código descuenta 1 % en lugar de 2 %. | Reemplazar la tarifa única por una lista de tramos o una función `SolidarityRateFor(ibc)`. Requiere una decisión de diseño y pruebas nuevas. |
| 2 | `ServiceProvisionWithholdingRate` | 4 % plano | 4 % declarante / 6 % no declarante | Un prestador no declarante queda con retención menor a la legal. | Agregar un indicador `IsIncomeTaxDeclarant` a `PayrollInput` o dos constantes separadas. |
| 3 | `ServiceProvisionWithholdingMinimumUvt` | 4 UVT | 2 UVT desde 01-07-2026 | El motor no retiene en pagos entre 2 y 4 UVT, aunque la norma ya obliga. | Cambiar el valor a 2 UVT y ajustar las pruebas del contrato de prestación de servicios. |

**Por qué no se corrigieron en esta fase:** el alcance de la Fase 3 es documentar,
no modificar valores. Cambiar la escala de solidaridad o la retención de servicios
exige nuevas pruebas unitarias y una revisión contable. Cualquier cambio debe
hacerse en un commit aparte, con su propia validación.

### 3.1 Observaciones de implementación (no son parámetros, pero afectan el cálculo)

- **Base de retención y auxilio de transporte.** `EmploymentPayrollStrategy` suma el
  auxilio de transporte al ingreso bruto antes de calcular la retención. El auxilio
  de transporte no es un ingreso salarial gravable; para el caso de salario mínimo
  la retención es cero de todos modos, así que el resultado de la sección 4 no cambia.
  Se marca **Por validar** la conveniencia de excluirlo de la base de retención en
  una próxima iteración.
- **Intereses de cesantías.** La provisión mensual del 1 % es una aproximación. El
  interés legal es del 12 % anual sobre el saldo acumulado de cesantías, que crece
  mes a mes. El código no acumula saldos porque no persiste corridas.
- **Retención de prestación de servicios.** El motor solo practica retención; no
  descuenta salud ni pensión del contratista, porque el contratista las paga por su
  cuenta. Es una decisión de diseño documentada en el propio código, no un error de
  parámetros.
- **Persistencia.** Las corridas no se guardan. Los valores calculados existen solo
  en memoria mientras la página está abierta. Esto ya está declarado como limitación
  en `docs/arc42/05-building-block-view.md`.

## 4. Casos de prueba a calcular a mano

Los tres casos usan **30 días trabajados**, **clase de riesgo ARL 1**, **sin otros
devengos** y **sin deducciones previas**. Los valores están en pesos colombianos.
El redondeo del motor es a pesos, con `MidpointRounding.AwayFromZero`
(`PayrollMath.Round`).

> Los resultados de esta sección se obtuvieron aplicando **las fórmulas del código
> actual** con los parámetros de la tabla 2. No son valores inventados: son la
> salida esperada que debe coincidir con la pantalla `/rrhh/nomina`. Si el código
> cambia, estos valores deben recalcularse.

### 4.1 Caso A — Salario mínimo con auxilio de transporte

**Datos de entrada**

| Campo | Valor |
|---|---|
| Contrato | Indefinido |
| Salario mensual | $1.750.905 |
| Días trabajados | 30 |
| Clase de riesgo ARL | 1 |
| Otros devengos | $0 |

**Procedimiento manual**

1. Salario devengado: `1.750.905 × 30 / 30 = 1.750.905`.
2. Auxilio de transporte: como `1.750.905 ≤ 2 × 1.750.905`, se paga el auxilio completo: `249.095`.
3. Total devengado: `1.750.905 + 249.095 = 2.000.000`.
4. Base de cotización (IBC): `mín(máx(1.750.905, 1.750.905), 25 × 1.750.905) = 1.750.905`.
5. Salud del empleado: `1.750.905 × 4 % = 70.036,2 → 70.036`.
6. Pensión del empleado: `1.750.905 × 4 % = 70.036,2 → 70.036`.
7. Solidaridad: el IBC es menor que `4 × 1.750.905 = 7.003.620`, así que **0**.
8. Retención: ingreso neto = `2.000.000 − 140.072 = 1.859.928`; renta exenta = `25 % × 1.859.928 = 464.982` (menor que el tope mensual de 790/12 UVT = $3.447.955); base en UVT = `(1.859.928 − 464.982) / 52.374 = 26,63 UVT`, que cae en el tramo 0–95 → **0**.
9. Deducciones totales: `70.036 + 70.036 + 0 + 0 = 140.072`.
10. Neto a pagar: `2.000.000 − 140.072 = 1.859.928`.
11. Aportes del empleador (con exoneración, porque `1.750.905 < 10 × 1.750.905`):
    - Salud empleador: **0** (exonerada).
    - Pensión empleador: `1.750.905 × 12 % = 210.108,6 → 210.109`.
    - ARL: `1.750.905 × 0,522 % = 9.139,72 → 9.140`.
    - Caja de compensación: `1.750.905 × 4 % = 70.036,2 → 70.036`.
    - SENA e ICBF: **0** (exonerados).
    - Total aportes del empleador: `210.109 + 9.140 + 70.036 = 289.285`.
12. Provisiones:
    - Base de cesantías y prima: `1.750.905 + 249.095 = 2.000.000`.
    - Cesantías: `2.000.000 × 8,33 % = 166.600`.
    - Intereses: `2.000.000 × 1 % = 20.000`.
    - Prima: `2.000.000 × 8,33 % = 166.600`.
    - Vacaciones: `1.750.905 × 4,17 % = 73.012,74 → 73.013`.
    - Total provisiones: `166.600 + 20.000 + 166.600 + 73.013 = 426.213`.
13. Costo total del empleador: `2.000.000 + 289.285 + 426.213 = 2.715.498`.

**Resultado esperado en la pantalla**

| Concepto | Valor |
|---|---:|
| Devengado | $2.000.000 |
| Deducciones | $140.072 |
| Neto | $1.859.928 |
| Aportes del empleador | $289.285 |
| Provisiones | $426.213 |
| Costo total del empleador | $2.715.498 |

**Cómo reproducirlo:** iniciar sesión como `rrhh@tiendaflow.local` (contraseña en
`Seed:DemoPassword`), crear el empleado en `/rrhh/empleados/nuevo` con el salario
indicado, abrir `/rrhh/nomina`, dejar 30 días y riesgo 1, calcular y hacer clic en
la fila para ver el desglose.

### 4.2 Caso B — Salario de $5.000.000

**Datos de entrada**

| Campo | Valor |
|---|---|
| Contrato | Indefinido |
| Salario mensual | $5.000.000 |
| Días trabajados | 30 |
| Clase de riesgo ARL | 1 |
| Otros devengos | $0 |

**Procedimiento manual**

1. Salario devengado: `5.000.000`.
2. Auxilio de transporte: `5.000.000 > 2 × 1.750.905 = 3.501.810` → **0**.
3. Total devengado: `5.000.000`.
4. IBC: `mín(máx(5.000.000, 1.750.905), 43.772.625) = 5.000.000`.
5. Salud del empleado: `5.000.000 × 4 % = 200.000`.
6. Pensión del empleado: `5.000.000 × 4 % = 200.000`.
7. Solidaridad: `5.000.000 < 4 × 1.750.905 = 7.003.620` → **0**.
8. Retención: ingreso neto = `5.000.000 − 400.000 = 4.600.000`; renta exenta = `25 % × 4.600.000 = 1.150.000`; base en UVT = `(4.600.000 − 1.150.000) / 52.374 = 65,87 UVT`, tramo 0–95 → **0**.
9. Deducciones totales: `200.000 + 200.000 + 0 + 0 = 400.000`.
10. Neto a pagar: `5.000.000 − 400.000 = 4.600.000`.
11. Aportes del empleador (exonerado, porque `5.000.000 < 10 × 1.750.905 = 17.509.050`):
    - Salud empleador: **0**.
    - Pensión empleador: `5.000.000 × 12 % = 600.000`.
    - ARL: `5.000.000 × 0,522 % = 26.100`.
    - Caja: `5.000.000 × 4 % = 200.000`.
    - SENA e ICBF: **0**.
    - Total aportes del empleador: `600.000 + 26.100 + 200.000 = 826.100`.
12. Provisiones:
    - Cesantías: `5.000.000 × 8,33 % = 416.500`.
    - Intereses: `5.000.000 × 1 % = 50.000`.
    - Prima: `5.000.000 × 8,33 % = 416.500`.
    - Vacaciones: `5.000.000 × 4,17 % = 208.500`.
    - Total provisiones: `416.500 + 50.000 + 416.500 + 208.500 = 1.091.500`.
13. Costo total del empleador: `5.000.000 + 826.100 + 1.091.500 = 6.917.600`.

**Resultado esperado en la pantalla**

| Concepto | Valor |
|---|---:|
| Devengado | $5.000.000 |
| Deducciones | $400.000 |
| Neto | $4.600.000 |
| Aportes del empleador | $826.100 |
| Provisiones | $1.091.500 |
| Costo total del empleador | $6.917.600 |

**Nota sobre la discrepancia de solidaridad:** este salario no alcanza el tramo
de 16 SMMLV ($28.014.480), así que la diferencia entre la tarifa plana del 1 % y
la escala no se manifiesta aquí. Para validar la discrepancia #1 se recomienda un
caso adicional con un salario de, por ejemplo, 30 SMMLV.

### 4.3 Caso C — Prestación de servicios por $5.000.000

**Datos de entrada**

| Campo | Valor |
|---|---|
| Contrato | Prestación de servicios |
| Honorarios mensuales | $5.000.000 |
| Días trabajados | 30 |
| Clase de riesgo ARL | No aplica |
| Otros devengos | $0 |

**Procedimiento manual**

1. Honorarios devengados: `5.000.000 × 30 / 30 = 5.000.000`.
2. Total devengado: `5.000.000`.
3. Retención: el pago supera el umbral de 4 UVT del código (`4 × 52.374 = 209.496`),
   así que se aplica `5.000.000 × 4 % = 200.000`.
4. Deducciones totales: `200.000`.
5. Neto a pagar: `5.000.000 − 200.000 = 4.800.000`.
6. Aportes del empleador y provisiones: **no aplican** para prestación de servicios.
7. Costo total del empleador: `5.000.000`.

**Resultado esperado en la pantalla**

| Concepto | Valor |
|---|---:|
| Devengado | $5.000.000 |
| Deducciones | $200.000 |
| Neto | $4.800.000 |
| Aportes del empleador | $0 |
| Provisiones | $0 |
| Costo total del empleador | $5.000.000 |

**Efecto de las discrepancias #2 y #3 con la norma vigente**

- Con el umbral correcto de 2 UVT ($104.748), este pago igualmente supera el
  umbral, así que la retención se practica.
- Si el prestador es **no declarante**, la tarifa correcta es 6 %, no 4 %:
  `5.000.000 × 6 % = 300.000` y el neto sería `4.700.000`. El código actual
  retiene `200.000` porque no distingue declarantes.
- Para ver el efecto del umbral de 2 UVT hace falta un pago entre $104.748 y
  $209.496: con el código actual no se retiene; con la norma vigente sí.

## 5. Conclusión y siguiente paso

- Los valores de referencia (salario mínimo, auxilio de transporte, UVT, topes y
  tarifas de aportes) están alineados con la normativa 2026 consultada.
- Las tres discrepancias están identificadas y cuantificadas, pero **no se corrigió
  el código** en esta fase. Quedan como deuda técnica para un cambio posterior con
  pruebas propias.
- La pantalla `/rrhh/nomina` mantiene el aviso de parámetros pendientes de
  validación legal, ahora respaldado por este documento.
- No se requiere una migración de base de datos para estas correcciones: son valores
  en memoria de `PayrollParameters`.

## 6. Fuentes consultadas (9 de noviembre de 2026)

- Ministerio del Trabajo, **Decreto 1469 del 29 de diciembre de 2025** (salario mínimo 2026):
  https://www.cerlatam.com/normatividad/mintrabajo-decreto-1469-de-2025-29-dic-2025/
- Presidencia de la República, **Decreto 0159 del 19 de febrero de 2026** (fijación transitoria del salario mínimo):
  https://dapre.presidencia.gov.co/normativa/normativa/DECRETO%20No.%200159%20DEL%2019%20DE%20FEBRERO%20DE%202026.pdf
- Ministerio del Trabajo, **Decreto 1470 del 29 de diciembre de 2025** (auxilio de transporte 2026):
  https://imagenes.noticiasrcn.com/cms/2025/12/29215454/DECRETO-1470-DEL-29-DE-DICIEMBRE-DE-2025.pdf
- DIAN, **Resolución 000238 del 15 de diciembre de 2025** (UVT 2026):
  https://www.dian.gov.co/normatividad/Normatividad/Resoluci%C3%B3n%20000238%20de%2015-12-2025.pdf
- DIAN, **Estatuto Tributario, art. 383** (tabla de retención en la fuente):
  https://estatuto.co/383
- DIAN, **Estatuto Tributario, art. 206** (renta exenta del 25 % y tope de 790 UVT).
- DIAN, **Decreto 1625 de 2016, arts. 1.2.4.4.14 y 1.2.4.4.1**, modificados por el
  **Decreto 572 de 2025** (retención por servicios):
  https://normograma.dian.gov.co/dian/compilacion/docs/decreto_0572_2025.htm
- Consejo de Estado, **auto del 2 de junio de 2026, exp. 30229** (revocatoria de la
  suspensión del Decreto 572 de 2025, efectos desde el 1 de julio de 2026):
  https://www.itscontable.com/blog/cuando-vuelve-a-aplicar-el-decreto-572-de-2025/
- **Ley 100 de 1993** (arts. 18, 20, 25 y 204; base, límites y aportes a salud y pensión):
  http://www.secretariasenado.gov.co/senado/basedoc/ley_0100_1993.html
- **Ley 797 de 2003, art. 8** (aporte adicional al Fondo de Solidaridad Pensional):
  https://www.fasecolda.com/wp-content/uploads/ley-797-2003.pdf
- **Ley 1819 de 2016 / Estatuto Tributario, art. 114-1** (exoneración de aportes):
  https://www.funcionpublica.gov.co/eva/gestornormativo/norma.php?i=79140
- **Ley 21 de 1982** (aportes parafiscales: caja 4 %, SENA 2 %, ICBF 3 %):
  https://www.funcionpublica.gov.co/eva/gestornormativo/norma.php?i=4827
- **Decreto 1771 de 1994** (tarifas ARL por clase de riesgo):
  https://coriesgos.com/blog/arl-independientes-colombia-2026
- **Código Sustantivo del Trabajo** (arts. 186, 249 y 306; vacaciones, cesantías y prima):
  https://www.gerencie.com/porcentajes-prestaciones-sociales.html
