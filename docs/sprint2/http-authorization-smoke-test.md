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
