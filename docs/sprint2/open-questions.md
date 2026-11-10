# Preguntas abiertas — Sprint 2

Este archivo registra dudas, bloqueos y decisiones pendientes detectadas durante el Sprint 2. No reemplaza los issues del repositorio; sirve como bitácora para el Sprint Planning y la retrospectiva.

| # | Tema | Pregunta o bloqueo | Estado | Responsable sugerido |
|---|---|---|---|---|
| 1 | Privacidad / auditoría | ¿Se conserva el correo intentado en `LoginFailed` (`EntityId`) para investigación de accesos, o se seudonimiza con un hash con sal? | Abierta | Seguridad + líder técnico |
| 2 | Nómina / API | `POST /api/nomina/calculate` recibe `baseSalary` y `employeeId` del cliente. ¿Debe releer al empleado desde la base o declararse explícitamente como calculadora de simulación? | Abierta | Nómina + líder técnico |
| 3 | Inventario | La recepción de una orden de compra crea filas de stock con `minStock: 0`. ¿El mínimo se toma del producto, de una fila de referencia o se pide en la recepción? | Abierta | Inventario + Compras |
| 4 | Permisos | `/inventario/alertas` exige `InventoryAlertsView`, pero `InventoryDashboardService.Get` exige `InventoryStockView`. ¿Las alertas son una vista del panel o una función independiente? | Abierta | Seguridad + Inventario |
| 5 | Nómina / persistencia | La corrida no se guarda. ¿Se requieren tablas de corrida y detalle en el Sprint 3? Implicaría una migración nueva. | Abierta | Nómina + base de datos |
| 6 | Nómina / legal | Las tres discrepancias de parámetros (solidaridad, retención de prestación de servicios y base de retención con auxilio de transporte) requieren validación legal antes de producción. | Documentada en `payroll-parameters-validation.md` | Nómina + jurídica |
| 7 | Pruebas | No hay pruebas de componentes Razor (bUnit). ¿Se agrega un proyecto en el Sprint 3? | Abierta | Calidad |
| 8 | CI | El workflow usa `dotnet build` / `dotnet test` sin fijar `TiendaFlow.slnx`. ¿Se fija el objetivo para evitar descubrimientos accidentales? | Abierta | DevOps |

> Ninguna de estas preguntas bloqueó la entrega del Sprint 2. Las migraciones no fueron necesarias y no se creó ninguna.
