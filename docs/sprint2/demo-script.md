# Guion de demo manual — Sprint 2

**Rama:** `feature/sprint2-erp`
**Historias cubiertas:** HU-04 (nómina), HU-05 (alertas de stock), HU-06 (roles y auditoría), los pasos de empleados y los CRUD de Proveedores, Facturación y CRM clientes.
**Formato:** pasos numerados y verificables. Este documento no incluye capturas de pantalla.
**Idioma de la demo:** español. Los nombres de campos y botones son los que aparecen en la interfaz.

> **Contraseñas:** el repositorio nunca contiene contraseñas. Los valores viven en
> `dotnet user-secrets` bajo los nombres `Seed:AdminPassword` y `Seed:DemoPassword`.
> En este guion solo se escriben esos **nombres de secreto**, nunca sus valores.
> Para consultarlos en tu máquina: `dotnet user-secrets list --project src\TiendaFlow.Web`.

## 1. Prerrequisitos

1. Docker Desktop está en ejecución y el contenedor de SQL Server `tiendaflow-sql` está encendido.
   - Verificación: `docker ps --filter "name=tiendaflow-sql"` muestra el contenedor en estado `Up`.
2. La cadena de conexión `ConnectionStrings:DefaultConnection` está configurada en los user-secrets del proyecto Web.
3. Desde la raíz del repositorio, iniciar la aplicación con el perfil HTTP:
   ```powershell
   dotnet run --project src\TiendaFlow.Web --launch-profile http
   ```
4. Abrir `http://localhost:5212`. La aplicación redirige a `/login`.
5. La primera ejecución crea la base de datos y el **seeder de desarrollo**:
   - 1 tienda, 4 productos y su stock inicial.
   - 7 roles y 7 usuarios de demostración.
   - 7 empleados, uno por usuario, **con salario 0**.

> **Nota del seeder:** todos los empleados quedan con salario `0`. La pantalla
> `/rrhh/nomina` calcula únicamente empleados activos y el motor rechaza un salario
> base de cero. Por eso, antes de calcular la nómina, Recursos Humanos debe asignar
> salarios en `/rrhh/empleados`. Esto hace parte de la demo de empleados (sección 2).

### 1.1 Usuarios de desarrollo

Todos usan el dominio `@tiendaflow.local`:

| Correo | Rol | Contraseña |
|---|---|---|
| `admin@tiendaflow.local` | Administrador | `Seed:AdminPassword` |
| `gerente@tiendaflow.local` | Gerente corporativo | `Seed:DemoPassword` |
| `jefe.tienda@tiendaflow.local` | Jefe de tienda | `Seed:DemoPassword` |
| `cajero@tiendaflow.local` | Cajero | `Seed:DemoPassword` |
| `bodega@tiendaflow.local` | Auxiliar de bodega | `Seed:DemoPassword` |
| `logistica@tiendaflow.local` | Coordinador de logística | `Seed:DemoPassword` |
| `rrhh@tiendaflow.local` | Gestor de recursos humanos | `Seed:DemoPassword` |

### 1.2 Datos sembrados que usa la demo

| SKU | Producto | Stock inicial | Mínimo |
|---|---|---:|---:|
| `SKU-1001` | Agua 600 ml | 40 | 10 |
| `SKU-1002` | Gaseosa 400 ml | 25 | 8 |
| `SKU-1003` | Arroz 500 g | 18 | 6 |
| `SKU-1004` | Pan tajado | 12 | 5 |

Los cuatro productos empiezan **por encima** de su mínimo, así que el panel arranca
con 0 alertas. Esa es la condición inicial que la demo de HU-05 va a cambiar.

El seeder también deja datos para los módulos nuevos:

| Módulo | Datos sembrados |
|---|---|
| Proveedores | 3 proveedores activos: Distribuidora Andina S.A.S., Lácteos del Altiplano Ltda. y Aseo Total Colombia S.A.S. |
| Facturación | 2 facturas emitidas: Cliente mostrador (documento `2222222222`) y María Fernanda Rojas (documento `1010101010`) |
| CRM clientes | 3 clientes: María Fernanda Rojas, Cliente mostrador y Jorge Iván Peña |

### 1.3 Regla de oro de la demo

Cada verificación debe compararse con un valor escrito en este guion. Si un valor
no coincide, detener la demo y anotar la diferencia; no continuar “a ojo”.

Los montos se muestran con formato `es-CO`. Según el sistema operativo, el símbolo
`$` puede aparecer pegado al número o separado por un espacio (`$1.750.905` o
`$ 1.750.905`). Lo que se valida en la demo son los dígitos y los separadores de
miles, no el espacio del símbolo.

### 1.4 Página de aterrizaje tras el inicio de sesión

Si el inicio de sesión no trae un `returnUrl` local válido, la aplicación elige la
primera página permitida del rol en este orden: `/inventario`,
`/rrhh/empleados`, `/seguridad/usuarios`. Si el rol no tiene ninguna de las tres,
usa `/inventario` como antes.

| Rol | Correo | Página tras el login |
|---|---|---|
| Administrador | `admin@tiendaflow.local` | `/inventario` |
| Gerente corporativo | `gerente@tiendaflow.local` | `/inventario` |
| Jefe de tienda | `jefe.tienda@tiendaflow.local` | `/inventario` |
| Cajero | `cajero@tiendaflow.local` | `/inventario` |
| Auxiliar de bodega | `bodega@tiendaflow.local` | `/inventario` |
| Coordinador de logística | `logistica@tiendaflow.local` | `/inventario` |
| Gestor de recursos humanos | `rrhh@tiendaflow.local` | `/rrhh/empleados` |

Un `returnUrl` local válido (por ejemplo, `/rrhh/nomina`) se respeta. Una URL
externa o malformada se descarta y se aplica la tabla anterior.

---

## 2. Paso previo: empleados y salarios (HU-04)

**Objetivo:** demostrar el listado de empleados, el formulario de creación/edición,
el enmascaramiento del salario y la asignación de salarios que la nómina necesita.

1. Iniciar sesión como `rrhh@tiendaflow.local` con la contraseña `Seed:DemoPassword`.
2. En el menú lateral, sección **Talento humano**, hacer clic en **Empleados**.
   - Ruta: `/rrhh/empleados`.
   - Verificación: se ven 7 empleados, todos con contrato **Indefinido** y estado **Activo**.
   - Verificación: en la columna **Salario** todos muestran `$0`.
   - Verificación: existen los botones **+ Registrar empleado** y **Calcular nómina**, y cada fila tiene **Editar**.
3. Hacer clic en **Editar** en la fila de **Marcela Díaz** (`rrhh@tiendaflow.local`).
   - Ruta: `/rrhh/empleados/7` (el identificador puede variar si la base ya tenía datos).
   - Verificación: el formulario muestra los datos de la persona y el selector de contrato.
4. En **Salario mensual (COP)**, escribir `1750905`. Mantener contrato **Indefinido** y **Empleado activo** marcado.
5. Hacer clic en **Guardar cambios**.
   - Verificación: vuelve a `/rrhh/empleados?guardado=1` y aparece el mensaje **“El empleado se guardó correctamente.”**
   - Verificación: la fila de Marcela Díaz ahora muestra `$1.750.905`.
6. Repetir los pasos 3 a 5 para **Carlos Ramírez** (`jefe.tienda@tiendaflow.local`) y asignarle `5000000`.
   - Verificación: la fila de Carlos Ramírez muestra `$5.000.000`.
7. (Opcional) Crear un empleado nuevo con **+ Registrar empleado**:
   - Ruta: `/rrhh/empleados/nuevo`.
   - Nombre: `Prueba Demo`; documento: `9999999999`; cargo: `Auxiliar`; tienda: la única disponible; contrato: **Término fijo**; salario: `1750905`.
   - Verificación: al guardar aparece el mensaje de éxito y el empleado nuevo en la lista con contrato **Término fijo**.
8. **Demostrar el enmascaramiento del salario:**
   - Cerrar sesión con **Salir**.
   - Iniciar sesión como `admin@tiendaflow.local` con `Seed:AdminPassword`.
   - Abrir **Talento humano → Empleados** (`/rrhh/empleados`).
   - Verificación: la columna **Salario** muestra `—` para todos.
   - Verificación: **no** aparecen **+ Registrar empleado** ni **Editar**, y **no** aparece la sección **Nómina** en el menú.
   - Verificación: el Administrador sí puede ver el listado porque tiene `HrEmployeesView`, pero no administra salarios porque no tiene `HrEmployeesManage`.

> El Administrador no tiene `HrPayrollManage`. Eso se usa en la sección 3.2 para
> probar el acceso denegado a la nómina.

---

## 3. HU-04: nómina del mes

### 3.1 Cálculo desde la página `/rrhh/nomina`

1. Iniciar sesión como `rrhh@tiendaflow.local` con `Seed:DemoPassword`.
   - Verificación: tras el login, la aplicación aterriza en `/rrhh/empleados` (no en `/acceso-denegado`).
2. Abrir **Talento humano → Nómina** (`/rrhh/nomina`).
   - Verificación: se ve el aviso **“Los valores usan parámetros de referencia pendientes de validación legal.”**
   - Verificación: **Días trabajados** vale `30` y **Clase de riesgo ARL** vale `1`.
3. Hacer clic en **Calcular nómina del mes**.
   - Verificación: aparece la sección **Resultado de la corrida** con los empleados activos.
   - Verificación: los empleados con salario 0 aparecen con la etiqueta **No calculado** y un texto de novedad en español; no rompen la corrida.
   - Verificación: la fila de totales dice **Totales (N empleados)** y solo suma las filas calculadas.
4. Hacer clic en la fila de **Marcela Díaz** (salario `$1.750.905`).
   - Verificación: se abre **Desglose de Marcela Díaz** con el detalle completo.
   - Comparar contra el **Caso A** de `docs/sprint2/payroll-parameters-validation.md`:

| Concepto | Valor esperado |
|---|---:|
| Salario devengado | $1.750.905 |
| Auxilio de transporte | $249.095 |
| Total devengado | $2.000.000 |
| Base de cotización | $1.750.905 |
| Salud (empleado) | $70.036 |
| Pensión (empleado) | $70.036 |
| Fondo de solidaridad | $0 |
| Retención en la fuente | $0 |
| Total deducciones | $140.072 |
| Neto a pagar | $1.859.928 |
| Total aportes del empleador | $289.285 |
| Total provisiones | $426.213 |
| Costo total del empleador | $2.715.498 |

5. Hacer clic en la fila de **Carlos Ramírez** (salario `$5.000.000`).
   - Comparar contra el **Caso B** del mismo documento:

| Concepto | Valor esperado |
|---|---:|
| Total devengado | $5.000.000 |
| Total deducciones | $400.000 |
| Neto a pagar | $4.600.000 |
| Total aportes del empleador | $826.100 |
| Total provisiones | $1.091.500 |
| Costo total del empleador | $6.917.600 |

6. Verificación de no persistencia: recargar la página con `F5`.
   - Verificación: el resultado desaparece; la corrida **no** se guardó. Es una limitación declarada del Sprint 2.

### 3.2 Endpoint `POST /api/nomina/calculate` con `fetch`

La página usa el servicio `PayrollRunService`; el endpoint expone el mismo motor
para una sola persona y está protegido con la política `HrPayrollManage`.
El cuerpo usa el contrato `PayrollInput`; `contractType` se serializa como número:
`1 = Indefinido`, `2 = Término fijo`, `3 = Obra o labor`, `4 = Prestación de servicios`.

**Caso exitoso (usuario de Recursos Humanos):**

1. Con la sesión de `rrhh@tiendaflow.local` abierta, presionar `F12` y abrir la pestaña **Console**.
2. Ejecutar exactamente:
   ```js
   await fetch('/api/nomina/calculate', {
     method: 'POST',
     headers: { 'Content-Type': 'application/json' },
     body: JSON.stringify({
       employeeId: 1,
       contractType: 1,
       baseSalary: 1750905,
       daysWorked: 30,
       otherSalaryEarnings: 0,
       riskClass: 1
     })
   }).then(r => r.json()).then(console.log);
   ```
3. Verificación: la consola muestra un objeto JSON con, al menos:
   - `grossEarnings: 2000000`
   - `totalDeductions: 140072`
   - `netPay: 1859928`
   - `totalEmployerContributions: 289285`
   - `totalProvisions: 426213`
   - `totalEmployerCost: 2715498`
4. Verificación: no se muestra ningún dato personal en la respuesta; solo `employeeId` y valores de nómina.

**Caso denegado (Administrador):**

1. Cerrar sesión y entrar como `admin@tiendaflow.local` con `Seed:AdminPassword`.
2. Abrir `http://localhost:5212/inventario` (cualquier página autenticada) para tener el contexto de la cookie.
3. Abrir la consola (`F12` → **Console**) y ejecutar el mismo `fetch` del paso anterior.
4. Verificación: la respuesta **no** es el JSON de nómina. Inspeccionar con:
   ```js
   const r = await fetch('/api/nomina/calculate', {
     method: 'POST',
     headers: { 'Content-Type': 'application/json' },
     body: JSON.stringify({ employeeId: 1, contractType: 1, baseSalary: 1750905, daysWorked: 30, otherSalaryEarnings: 0, riskClass: 1 })
   });
   console.log({ status: r.status, redirected: r.redirected, url: r.url });
   ```
   - Verificación: `redirected` es `true` y `url` termina en `/acceso-denegado`.
   - Verificación: la cookie de sesión sigue válida; el rechazo es por permisos, no por autenticación.
5. Navegar a `/rrhh/nomina` con la sesión de Administrador.
   - Verificación: la aplicación redirige a `/acceso-denegado` y muestra **“No tienes acceso a esta función”**.
6. Dejar esta sesión de Administrador abierta para la sección 5 (auditoría).

---

## 4. HU-05: venta que dispara la alerta de stock

**Objetivo:** vender hasta dejar un producto por debajo de su mínimo y comprobar la
alerta en `/inventario/alertas` y en el panel.

**Usuario:** `jefe.tienda@tiendaflow.local` (tiene `SalesPosUse` y `InventoryAlertsView`).
El cajero **no** tiene `InventoryAlertsView`, por eso no se usa para esta demo.

1. Iniciar sesión como `jefe.tienda@tiendaflow.local` con `Seed:DemoPassword`.
2. Abrir **Inventario → Panel de inventario** (`/inventario`).
   - Verificación: el KPI **Bajo mínimo** muestra `0`.
   - Verificación: en la tabla, **Pan tajado** (`SKU-1004`) muestra stock `12` y mínimo `5`, con estado **Disponible**.
3. Abrir **Inventario → Registrar venta** (`/inventario/ventas`).
4. En **Producto**, seleccionar `SKU-1004 · Pan tajado`.
   - Verificación: **Precio unitario** muestra `$5.500`.
   - Verificación: **Disponible en Tienda Centro** muestra `12 unidades`.
5. En **Cantidad**, escribir `8`.
   - Verificación: **Total** muestra `$44.000`.
   - Cálculo: `8 × 5.500 = 44.000`.
6. Hacer clic en **Confirmar venta**.
   - Verificación: el comprobante **Venta registrada** aparece en la columna derecha y su pie dice **Stock restante: 4 unidades**.
   - Verificación: el resumen **Disponible en Tienda Centro** puede seguir mostrando el valor anterior hasta cambiar de producto; el dato vinculante es el del comprobante y el de `/inventario/alertas`.
   - Verificación: si el stock inicial ya no era 12 por una demo anterior, vender la cantidad necesaria para quedar **por debajo de 5**: `cantidad = stock_actual − 5 + 1`. Anotar el stock visto en el paso 4.
7. Abrir **Inventario → Alertas de stock** (`/inventario/alertas`).
   - Verificación: el KPI **Alertas activas** muestra `1`.
   - Verificación: la tabla **Productos que requieren reposición** incluye **Pan tajado**, disponible `4`, mínimo `5`, faltante `1` y estado **Bajo mínimo**.
   - Verificación: el KPI **Reposición estimada** muestra `$5.500` (1 unidad × precio de venta).
8. Volver al **Panel de inventario** (`/inventario`).
   - Verificación: el KPI **Bajo mínimo** ahora muestra `1`.
   - Verificación: la fila de **Pan tajado** muestra stock `4` y estado **Bajo mínimo**.
9. Verificación del enlace de reposición: en `/inventario/alertas`, hacer clic en **Recibir** en la fila de Pan tajado.
   - Verificación: navega a `/inventario/recibir?sku=SKU-1004` con el SKU preseleccionado.
   - No es necesario registrar la recepción; con abrir la pantalla basta para la demo.

> Si se quiere dejar el inventario como al inicio, registrar una recepción de al
> menos 1 unidad de `SKU-1004` desde `/inventario/recibir`, o recrear la base de
> datos de desarrollo antes de la siguiente demo.

---

## 5. HU-06: cambio de rol, matriz y acceso denegado auditado

**Objetivo:** asignar un rol, ver la matriz de permisos, comprobar que el cambio
surte efecto en el siguiente inicio de sesión y ver el rechazo en la auditoría.

1. Iniciar sesión como `admin@tiendaflow.local` con `Seed:AdminPassword`.
2. Abrir **Seguridad → Usuarios** (`/seguridad/usuarios`).
   - Verificación: se listan los 7 usuarios con su rol actual.
   - Verificación: `bodega@tiendaflow.local` aparece con el rol **Auxiliar de bodega**.
3. Hacer clic en **Editar** en la fila de **Diego Herrera** (`bodega@tiendaflow.local`).
   - Ruta: `/seguridad/usuarios/{id}`.
4. En **Rol**, seleccionar **Jefe de tienda** y hacer clic en **Guardar cambios**.
   - Verificación: aparece el mensaje de éxito y la cabecera muestra el nuevo rol.
5. Abrir **Seguridad → Roles y permisos** (`/seguridad/roles`).
   - Verificación: la matriz es de solo lectura y muestra los 7 roles y el total de permisos.
   - Verificación: la fila `sales.pos.use` tiene **Sí** en la columna **Jefe de tienda** y **—** en la columna **Auxiliar de bodega**.
   - Verificación: la fila `inventory.alerts.view` tiene **Sí** en **Jefe de tienda** y **Sí** en **Auxiliar de bodega**.
   - Verificación: la fila `inventory.stock.receive` tiene **Sí** en **Jefe de tienda** y **Sí** en **Auxiliar de bodega**.
   - Énfasis para el video: la matriz se define en el código y se aplica en páginas, botones y servicios (texto visible en la pantalla).
6. Cerrar sesión con **Salir**.
7. Iniciar sesión como `bodega@tiendaflow.local` con `Seed:DemoPassword`. El cambio de rol se aplica al emitir la nueva cookie.
   - Verificación: la barra lateral muestra el rol **Jefe de tienda**.
   - Verificación: ahora aparece **Registrar venta** en el menú (antes no aparecía para Auxiliar de bodega).
8. Probar el acceso denegado con este usuario:
   - Navegar manualmente a `http://localhost:5212/seguridad/usuarios`.
   - Verificación: redirige a `/acceso-denegado` con **“No tienes acceso a esta función”**.
   - Explicación: el rol **Jefe de tienda** no tiene `SecurityUsersView`.
9. Cerrar sesión e iniciar sesión de nuevo como `admin@tiendaflow.local` con `Seed:AdminPassword`.
10. Abrir **Seguridad → Auditoría** (`/seguridad/auditoria`).
    - Verificación: la tabla **Eventos recientes** incluye una fila con:
      - **Usuario:** Diego Herrera
      - **Acción:** `AccessDenied`
      - **Módulo:** `security`
      - **Detalle:** `Intento de acceso denegado a /seguridad/usuarios`
    - Verificación: no hay contraseñas, documentos ni salarios en el detalle.
11. Restaurar el estado del usuario:
    - Volver a **Seguridad → Usuarios**, editar a Diego Herrera, seleccionar **Auxiliar de bodega** y guardar.
    - Verificación: la lista vuelve a mostrar **Auxiliar de bodega**.

> El evento de auditoría del paso 10 también se genera cuando el Administrador
> intenta abrir `/rrhh/nomina` (sección 3.2, paso 5). Ambos rechazos quedan en la
> misma tabla con el usuario autenticado, la función y la ruta; nunca con datos
> personales ni montos.

---

## 6. CRUD de Proveedores (Compras)

**Objetivo:** demostrar listado, búsqueda, alta, edición y desactivación de proveedores,
y que el rol de Coordinador de Logística es el único que administra el módulo.

**Usuario:** `logistica@tiendaflow.local` (`PurchasingSuppliersView` y `PurchasingSuppliersManage`).

1. Iniciar sesión como `logistica@tiendaflow.local` con `Seed:DemoPassword`.
2. Abrir **Compras → Proveedores** (`/proveedores`).
   - Verificación: se ven 3 proveedores sembrados y el botón **+ Registrar proveedor**.
   - Verificación: cada fila activa muestra **Editar** y **Desactivar**.
3. Hacer clic en **+ Registrar proveedor** (`/proveedores/nuevo`) y completar:
   - **NIT:** `901555444-9`
   - **Razón social:** `Panadería El Trigal S.A.S.`
   - **Dirección:** `Calle 100 # 20-30, Bogotá`
   - **Calificación inicial (%):** `95`
4. Hacer clic en **Registrar proveedor**.
   - Verificación: vuelve a `/proveedores?guardado=1` con **“El proveedor se guardó correctamente.”**
   - Verificación: la tabla muestra el nuevo proveedor con calificación `95 %`, compras `0` y estado **Activo**.
5. En la fila del proveedor nuevo, hacer clic en **Editar**.
   - Verificación: el NIT aparece en solo lectura; la razón social y la dirección son editables.
   - Cambiar la dirección a `Calle 100 # 20-32, Bogotá` y guardar.
   - Verificación: la tabla muestra la dirección nueva.
6. En la fila del proveedor nuevo, hacer clic en **Desactivar**.
   - Verificación: el estado pasa a **Inactivo** y el botón **Desactivar** desaparece de esa fila.
7. Probar la búsqueda: escribir `Trigal` en **Buscar**.
   - Verificación: queda solo la fila del proveedor nuevo.
8. **Verificación de roles (solo lectura y denegación):**
   - Cerrar sesión e iniciar como `jefe.tienda@tiendaflow.local` con `Seed:DemoPassword`.
   - Abrir **Compras → Proveedores**.
   - Verificación: la lista se ve, pero **no** aparecen **+ Registrar proveedor**, **Editar** ni **Desactivar**.
   - Cerrar sesión e iniciar como `bodega@tiendaflow.local` con `Seed:DemoPassword`.
   - Verificación: el menú no muestra la sección **Compras**.
   - Navegar a `http://localhost:5212/proveedores`.
   - Verificación: redirige a `/acceso-denegado`.

---

## 7. CRUD de Facturación (Ventas)

**Objetivo:** emitir una factura con varias líneas, editarla, anularla y comprobar que
una factura anulada queda en solo lectura.

**Usuario:** `cajero@tiendaflow.local` (`SalesInvoicesView` y `SalesInvoicesManage`).

1. Iniciar sesión como `cajero@tiendaflow.local` con `Seed:DemoPassword`.
2. Abrir **Facturación → Facturas** (`/facturacion`).
   - Verificación: se ven 2 facturas sembradas, ambas con estado **Emitida**, y el botón **+ Nueva factura**.
3. Hacer clic en **+ Nueva factura** (`/facturacion/nueva`) y completar:
   - **Nombre del cliente:** `Andrés Pérez`
   - **Documento:** `1032456789`
   - **Fecha de emisión:** la fecha del día
4. En la primera línea, completar:
   - **SKU:** `SKU-1001`
   - **Descripción:** `Agua 600 ml`
   - **Cantidad:** `3`
   - **Precio unitario:** `2500`
5. Hacer clic en **+ Agregar ítem** y completar la segunda línea:
   - **SKU:** `SKU-1004`
   - **Descripción:** `Pan tajado`
   - **Cantidad:** `1`
   - **Precio unitario:** `5500`
   - Verificación: **Total:** `$13.000` (3 × 2.500 + 1 × 5.500).
6. Hacer clic en **Emitir factura**.
   - Verificación: vuelve a `/facturacion?guardado=1` con **“La factura se guardó correctamente.”**
   - Verificación: la factura nueva aparece con cliente `Andrés Pérez`, 2 ítems, total `$13.000` y estado **Emitida**.
7. Hacer clic en **Editar** en la factura nueva.
   - Cambiar la cantidad de la primera línea de `3` a `4`.
   - Verificación: **Total:** `$15.500` (4 × 2.500 + 1 × 5.500).
   - Guardar y verificar el total en la lista.
8. En la lista, hacer clic en **Anular** en la factura nueva.
   - Verificación: vuelve a `/facturacion?anulada=1` con **“La factura quedó anulada.”**
   - Verificación: el estado pasa a **Anulada** y el botón **Anular** desaparece de esa fila.
9. Hacer clic en **Ver** en la factura anulada.
   - Verificación: aparece el aviso **“Esta factura está anulada y ya no se puede editar.”**
   - Verificación: los ítems se ven en solo lectura y no hay botón **Guardar cambios**.
10. **Verificación de roles:** cerrar sesión e iniciar como `rrhh@tiendaflow.local` con `Seed:DemoPassword`.
    - Verificación: el menú no muestra la sección **Facturación**.
    - Navegar a `http://localhost:5212/facturacion`.
    - Verificación: redirige a `/acceso-denegado`.

---

## 8. CRUD de CRM clientes

**Objetivo:** registrar, editar y desactivar un cliente, comprobar la unicidad del
documento y verificar que el módulo solo aparece para los roles autorizados.

**Usuario:** `cajero@tiendaflow.local` (`CrmCustomersView` y `CrmCustomersManage`).

1. Con la sesión de `cajero@tiendaflow.local`, abrir **CRM → Clientes** (`/crm/clientes`).
   - Verificación: se ven 3 clientes sembrados y el botón **+ Registrar cliente**.
2. Hacer clic en **+ Registrar cliente** (`/crm/clientes/nuevo`) y completar:
   - **Documento:** `1099887766`
   - **Nombre completo:** `Laura Gómez`
   - **Correo electrónico:** `laura.gomez@example.com`
   - **Teléfono:** `3205557788`
   - **Dirección:** `Carrera 11 # 70-15, Bogotá`
3. Hacer clic en **Registrar cliente**.
   - Verificación: vuelve a `/crm/clientes?guardado=1` con **“El cliente se guardó correctamente.”**
   - Verificación: la fila muestra documento, nombre, `laura.gomez@example.com · 3205557788` y estado **Activo**.
4. Hacer clic en **Editar** en la fila de Laura Gómez.
   - Verificación: el documento aparece en solo lectura.
   - Cambiar el teléfono a `3205557799` y guardar; verificar el cambio en la lista.
5. En la fila de Laura Gómez, hacer clic en **Desactivar**.
   - Verificación: el estado pasa a **Inactivo** y el botón **Desactivar** desaparece de esa fila.
6. **Verificación de documento único:** hacer clic en **+ Registrar cliente** e intentar crear otro cliente con documento `1099887766`.
   - Verificación: aparece **“Ya existe un cliente con ese documento.”** y no se crea una segunda fila.
7. Probar la búsqueda: escribir `Laura` en **Buscar**.
   - Verificación: queda solo la fila de Laura Gómez.
8. **Verificación de roles:** cerrar sesión e iniciar como `bodega@tiendaflow.local` con `Seed:DemoPassword`.
   - Verificación: el menú no muestra la sección **CRM**.
   - Navegar a `http://localhost:5212/crm/clientes`.
   - Verificación: redirige a `/acceso-denegado`.

---

## 9. Cierre de la demo

1. Recorrido final sugerido: **Empleados** (salario visible para RR. HH.) → **Nómina** (desglose y totales) → **Alertas de stock** (Pan tajado bajo mínimo) → **Roles** (matriz) → **Auditoría** (acceso denegado) → **Proveedores** → **Facturación** → **Clientes**.
2. Frase de cierre: “TiendaFlow separa la vista, la acción y el dato: cada pantalla pide un permiso, cada botón se oculta sin permiso y cada servicio vuelve a exigirlo; además, todo rechazo queda auditado sin datos personales”.
3. Verificación final de integridad: cerrar sesión y confirmar que `/rrhh/nomina`, `/seguridad/usuarios`, `/inventario/ventas`, `/proveedores`, `/facturacion` y `/crm/clientes` redirigen a `/login` cuando no hay sesión.

### 9.1 Estado que deja la demo

| Cambio | Cómo revertirlo |
|---|---|
| Salario de Marcela Díaz y Carlos Ramírez | Volver a `0` en `/rrhh/empleados` o recrear la base de desarrollo. |
| Empleado “Prueba Demo” (si se creó) | No hay borrado en la interfaz; se elimina al recrear la base de desarrollo. |
| Stock de Pan tajado en 4 | Registrar una recepción en `/inventario/recibir` o recrear la base de desarrollo. |
| Rol de Diego Herrera | Ya se restaura en el paso 11 de la sección 5. |
| Proveedor “Panadería El Trigal S.A.S.” | Queda inactivo; se elimina al recrear la base de desarrollo. |
| Factura de Andrés Pérez | Queda anulada; se elimina al recrear la base de desarrollo. |
| Cliente Laura Gómez | Queda inactivo; se elimina al recrear la base de desarrollo. |

> Recrear la base de datos de desarrollo vuelve a ejecutar el seeder y deja los
> salarios en `0` otra vez. No se deben borrar volúmenes ni contenedores de otros
> entornos; la demo siempre usa `tiendaflow-sql` y la cadena de desarrollo.
