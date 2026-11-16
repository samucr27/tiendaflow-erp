# Prueba de humo de autorización por HTTP — Sprint 2

**Fecha:** 9 de noviembre de 2026
**Rama:** `feature/sprint2-erp`
**Aplicación:** `dotnet run --project src\TiendaFlow.Web --launch-profile http`
en `http://localhost:5212`
**Base de datos:** contenedor Docker `tiendaflow-sql` (`Up`, puerto 1433)
**Alcance:** comprobar por HTTP que cada usuario demo recibe `200` o una
redirección coherente con `RolePermissions` en las rutas de la Fase E.

> Las contraseñas se leyeron en memoria desde `dotnet user-secrets`
> (`Seed:AdminPassword` y `Seed:DemoPassword`) y nunca se imprimieron ni se
> escribieron en archivos. La cadena de conexión tampoco se registró.

## 1. Método

1. Se verificó que el contenedor `tiendaflow-sql` estuviera en ejecución.
2. Se arrancó la aplicación con el perfil `http` y se esperó a que
   `GET /login` respondiera `200`.
3. Para cada usuario demo se abrió una sesión independiente con
   `HttpClient` y `CookieContainer`:
   - `GET /login` para obtener la cookie antiforgery y el token
     `__RequestVerificationToken`.
   - `POST /login` con correo, contraseña, `returnUrl` vacío y el token, sin
     seguir redirecciones.
4. Con la cookie de sesión se pidió cada ruta sin seguir redirecciones y se
   registraron el código HTTP y la cabecera `Location`.
5. Se envió `POST /api/nomina/calculate` con un `PayrollInput` válido:
   `employeeId = 1`, `contractType = 1` (`Indefinite`), `baseSalary = 2000000`,
   `daysWorked = 30`, `otherSalaryEarnings = 0`, `riskClass = 1`.
6. Se compararon los resultados con los permisos de `RolePermissions` y con las
   políticas `[Authorize]` de cada página.

El script de prueba y el CSV de resultados se ejecutaron fuera del repositorio
(`%TEMP%\tiendaflow-hr-smoke-results.csv`); no se agregaron al control de
versiones.

## 2. Resultados observados

Convenciones: **200** = página o cálculo servido; **302 A** = redirección a
`/acceso-denegado`; **302 L** = redirección a `/login`; **302 I** = redirección
a `/inventario`; **n/v** = no verificado (el inicio de sesión falló).

| Petición | Permiso exigido | Admin | Gerente | Jefe tienda | Cajero | Bodega | Logística | RR. HH. |
|---|---|---|---|---|---|---|---|---|
| `POST /login` | — | 302 I | 302 I | 302 I | 302 a `/login?error=1` | 302 I | 302 I | 302 I |
| `GET /inventario` | `InventoryStockView` | 200 | 200 | 200 | n/v | 200 | 200 | 302 A |
| `GET /inventario/alertas` | `InventoryAlertsView` | 200 | 200 | 200 | n/v | 200 | 200 | 302 A |
| `GET /seguridad/usuarios` | `SecurityUsersView` | 200 | 302 A | 302 A | n/v | 302 A | 302 A | 302 A |
| `GET /seguridad/roles` | `SecurityRolesView` | 200 | 302 A | 302 A | n/v | 302 A | 302 A | 302 A |
| `GET /seguridad/auditoria` | `SecurityAuditView` | 200 | 302 A | 302 A | n/v | 302 A | 302 A | 302 A |
| `GET /rrhh/empleados` | `HrEmployeesView` | 200 | 200 | 200 | n/v | 302 A | 302 A | 200 |
| `GET /rrhh/empleados/nuevo` | `HrEmployeesManage` | 302 A | 302 A | 302 A | n/v | 302 A | 302 A | 200 |
| `GET /rrhh/nomina` | `HrPayrollManage` | 302 A | 302 A | 302 A | n/v | 302 A | 302 A | 200 |
| `POST /api/nomina/calculate` | `HrPayrollManage` | 302 A | 302 A | 302 A | n/v | 302 A | 302 A | 200 |

Las redirecciones de denegación observadas tienen la forma
`http://localhost:5212/acceso-denegado?ReturnUrl=...`; las de sesión ausente,
`http://localhost:5212/login?ReturnUrl=...`.

### 2.1 Verificación contra `RolePermissions`

Para las seis cuentas que iniciaron sesión, el resultado coincide con la matriz
y con las políticas de página:

- Solo Admin abre las tres pantallas de Seguridad.
- Admin, Gerente, Jefe de tienda, Bodega y Logística abren el panel y las
  alertas de inventario; RR. HH. no tiene ninguno de los dos permisos y recibe
  `302 A`, como corresponde a su rol.
- Admin, Gerente, Jefe de tienda y RR. HH. abren `/rrhh/empleados`; Bodega y
  Logística no.
- Solo RR. HH. abre `/rrhh/empleados/nuevo`, `/rrhh/nomina` y el endpoint de
  cálculo; Admin no administra empleados ni nómina y recibe `302 A`.
- La API devolvió `200` únicamente para RR. HH.; no se validó el contenido del
  JSON en esta fase, solo el código de autorización.

### 2.2 Comportamiento sin sesión

Una sesión nueva, sin iniciar sesión, recibe `302` hacia `/login?ReturnUrl=...`
en las tres páginas comprobadas y en `POST /api/nomina/calculate`. No se
encontró ninguna ruta que sirviera contenido protegido de forma anónima.

## 3. Hallazgos

### E-01 — La cuenta demo del cajero no inicia sesión con la contraseña de demostración

- **Observado:** `POST /login` con `cajero@tiendaflow.local` responde
  `302 /login?error=1`.
- **Estado de la cuenta:** la consulta de solo lectura a `app_user` muestra la
  cuenta activa y con rol `Cashier`; el correo coincide con `DevDataSeeder.cs`.
- **Causa comprobada:** el registro de auditoría contiene dos acciones
  `UserUpdated` sobre la cuenta del cajero el 9 de noviembre de 2026 a las
  20:19 (hora UTC), seguidas de inicios de sesión correctos anteriores con la
  cuenta. Es decir, la contraseña de la base de datos de desarrollo se cambió
  desde la pantalla de usuarios después del sembrado. El sembrado no vuelve a
  ejecutarse porque ya existen usuarios.
- **Impacto:** la fila del cajero no se pudo comprobar por HTTP. No se detectó
  un defecto de autorización en el código: la matriz de `RolePermissions` y la
  política de cada página siguen siendo la fuente de verdad y no dependen de
  este dato.
- **Decisión:** no se restableció la contraseña ni se modificó la base de datos,
  porque el cambio fue una acción deliberada de una persona y sobrescribirla
  podría destruir trabajo de demostración. Se registra como bloqueo de datos.
- **Valores esperados para la fila no verificada**, según `RolePermissions`:
  `/inventario` 200; `/inventario/alertas` 302 A; las tres rutas de Seguridad
  302 A; `/rrhh/empleados`, `/rrhh/empleados/nuevo`, `/rrhh/nomina` y la API
  302 A.
- **Cerrado el 11 de noviembre de 2026:** con una base de datos recién sembrada
  el cajero inicia sesión y su fila coincide con la matriz (ver la sección 5).
  Confirma que E-01 era un dato de la base de desarrollo y no un defecto.

### E-02 - El destino posterior al inicio de sesión no siempre es accesible

- **Observado:** el inicio de sesión sin `returnUrl` redirigía a `/inventario`
  para todos los roles (`Program.cs:126`). RR. HH. no tiene
  `InventoryStockView`, así que al seguir esa redirección recibía
  `302 A` y terminaba en `/acceso-denegado` justo después de autenticarse.
- **Impacto:** usabilidad de la demo para el rol de RR. HH.; no era una falla de
  autorización, porque la página protegida seguía negando el acceso.
- **Corregido en `1a80602`** (`feat(security): add landing page per role`):
  `LandingPage.For(user.Permissions)` elige la primera ruta permitida entre
  `/inventario`, `/rrhh/empleados` y `/seguridad/usuarios`, con `/inventario`
  como respaldo. Se usa solo cuando no hay un `returnUrl` local válido.
- **Verificación por HTTP después del cambio** (misma app y mismas cuentas):
  `rrhh@tiendaflow.local` recibe `302 /rrhh/empleados`; Admin, Gerente, Jefe de
  tienda, Bodega y Logística reciben `302 /inventario`. El cajero no se pudo
  probar por el bloqueo de contraseña de E-01. Un `returnUrl` local como
  `/rrhh/nomina` o `/seguridad/roles` se respeta; `//evil.example` y
  `/\evil.example` se descartan y aterrizan en `/inventario`.
- **Pruebas:** `tests/TiendaFlow.UnitTests/LandingPageTests.cs` cubre los siete
  roles con `RolePermissions.ForRole`, el respaldo sin permisos y el orden de
  prioridad.

### E-03 — Las rutas de API responden con redirecciones de cookie en lugar de 401/403

- **Observado:** un cliente sin sesión recibe `302 /login?...` y un usuario sin
  `HrPayrollManage` recibe `302 /acceso-denegado?...` en
  `POST /api/nomina/calculate`.
- **Impacto:** es el comportamiento normal de la autenticación por cookie, pero
  un cliente de API que no siga redirecciones no recibe un `401`/`403`
  estándar. Para la demo de Blazor no rompe nada.
- **Recomendación:** si el endpoint se va a consumir como API, configurar los
  eventos de la cookie para responder `401`/`403` en rutas `/api/*`. No se
  cambió en esta fase; se deja como observación de endurecimiento.

## 4. Conclusión

- La autorización por HTTP coincide con `RolePermissions` en las 10 peticiones
  para las seis cuentas demo que pudieron autenticarse, incluida la única
  cuenta con permiso de nómina.
- No se encontró acceso anónimo a contenido protegido.
- La única fila no verificada es la del cajero, por un cambio de contraseña
  hecho desde la aplicación sobre la base de datos de desarrollo; el hallazgo
  E-01 documenta la causa y los valores esperados.
- Durante la prueba de la Fase E no se modificó código, base de datos ni
  configuración. La corrección de E-02 llegó después, en `1a80602`.

## 5. Repetición sobre `develop` — 11 de noviembre de 2026

**Rama:** `develop` en `0e6b12d` (incluye los PR #2, #3 y #4)
**Aplicación:** la misma, en `http://localhost:5212`, con el contenedor
`tiendaflow-sql` (SQL Server 2022) y una base de datos nueva sembrada por
`DevDataSeeder`.
**Alcance ampliado:** las 7 cuentas demo contra 21 peticiones (20 páginas de
Inventario, Proveedores, Facturación, CRM, RR. HH. y Seguridad, más
`POST /api/nomina/calculate`). Son 147 comprobaciones.

El método es el de la sección 1, con dos cambios: el resultado esperado de cada
celda se obtuvo leyendo directamente la matriz de `RolePermissions.cs`, y en
cada respuesta `200` se revisó que el cuerpo no mostrara un error ni un aviso
de permiso. Las contraseñas se leyeron en memoria desde `dotnet user-secrets`;
el script y el CSV quedaron fuera del repositorio.

### 5.1 Inicio de sesión

Las 7 cuentas inician sesión. RR. HH. aterriza en `/rrhh/empleados` y las otras
seis en `/inventario`, como define `LandingPage` (E-02).

### 5.2 Resultados

Convenciones: **200** = servida; **A** = `302` a `/acceso-denegado`.

| Petición | Permiso | Admin | Gerente | Jefe tienda | Cajero | Bodega | Logística | RR. HH. |
|---|---|---|---|---|---|---|---|---|
| `GET /inventario` | `InventoryStockView` | 200 | 200 | 200 | 200 | 200 | 200 | A |
| `GET /inventario/stock` | `InventoryStockView` | 200 | 200 | 200 | 200 | 200 | 200 | A |
| `GET /inventario/alertas` | `InventoryAlertsView` | 200 | 200 | 200 | A | 200 | 200 | A |
| `GET /inventario/productos` | `InventoryProductsView` | 200 | 200 | 200 | A | 200 | 200 | A |
| `GET /inventario/productos/nuevo` | `InventoryProductsManage` | A | A | 200 | A | A | A | A |
| `GET /inventario/recibir` | `InventoryStockReceive` | A | A | 200 | A | 200 | 200 | A |
| `GET /inventario/ventas` | `SalesPosUse` | A | A | 200 | 200 | A | A | A |
| `GET /proveedores` | `PurchasingSuppliersView` | 200 | 200 | 200 | A | A | 200 | A |
| `GET /proveedores/nuevo` | `PurchasingSuppliersManage` | A | A | A | A | A | 200 | A |
| `GET /facturacion` | `SalesInvoicesView` | 200 | 200 | 200 | 200 | A | A | A |
| `GET /facturacion/nueva` | `SalesInvoicesManage` | 200 | 200 | 200 | 200 | A | A | A |
| `GET /crm/clientes` | `CrmCustomersView` | 200 | 200 | 200 | 200 | A | A | A |
| `GET /crm/clientes/nuevo` | `CrmCustomersManage` | 200 | 200 | 200 | 200 | A | A | A |
| `GET /rrhh/empleados` | `HrEmployeesView` | 200 | 200 | 200 | A | A | A | 200 |
| `GET /rrhh/empleados/nuevo` | `HrEmployeesManage` | A | A | A | A | A | A | 200 |
| `GET /rrhh/nomina` | `HrPayrollManage` | A | A | A | A | A | A | 200 |
| `GET /seguridad/usuarios` | `SecurityUsersView` | 200 | A | A | A | A | A | A |
| `GET /seguridad/usuarios/nuevo` | `SecurityUsersManage` | 200 | A | A | A | A | A | A |
| `GET /seguridad/roles` | `SecurityRolesView` | 200 | A | A | A | A | A | A |
| `GET /seguridad/auditoria` | `SecurityAuditView` | 200 | A | A | A | A | A | A |
| `POST /api/nomina/calculate` | `HrPayrollManage` | A | A | A | A | A | A | 200 |

**Diferencias con `RolePermissions`: 0 de 147.**

Sin sesión, `/inventario`, `/proveedores`, `/facturacion`, `/crm/clientes`,
`/rrhh/nomina` y `/seguridad/usuarios` redirigen a `/login`.

La revisión de cuerpos solo marcó `/seguridad/auditoria` para Admin. Es
esperado: la página lista los eventos `AccessDenied` que la propia prueba
generó en las demás cuentas.

### 5.3 Comprobación funcional de las HU

- **HU-04:** con RR. HH., `POST /api/nomina/calculate` con salario base
  2.000.000 y 30 días devuelve el cálculo completo (neto 2.089.095; costo total
  para el empleador 3.060.126). Un salario no positivo devuelve `400` con el
  mensaje de validación del motor.
- **HU-05:** `/inventario/alertas` carga, pero con la base recién sembrada
  muestra "Sin alertas de stock" porque ningún producto empieza por debajo de
  su mínimo. Para la demo hay que vender o retirar unidades desde
  `/inventario/ventas` hasta llegar al mínimo.
- **HU-06:** `/seguridad/roles` muestra la matriz de 7 roles y 43 permisos, y
  `/seguridad/auditoria` registra los accesos denegados.

Las 17 pruebas de integración también pasaron contra el mismo SQL Server
(`TIENDAFLOW_TEST_CONNECTION`).

### 5.4 Conclusión de la repetición

La autorización por HTTP coincide con la matriz para los 7 roles y las 21
peticiones. E-01 queda cerrado. E-03 sigue abierto como observación de
endurecimiento. No se modificó código ni configuración durante la repetición.
