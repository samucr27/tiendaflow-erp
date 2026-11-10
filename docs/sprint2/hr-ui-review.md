# Revisión estática de la UI de RR. HH. — Sprint 2

**Fecha:** 9 de noviembre de 2026
**Rama:** `feature/opencode-erp-ui`
**Alcance:** revisión de código sin navegador de `Empleados.razor`,
`EmpleadoForm.razor`, `Nomina.razor`, `NavMenu.razor`, `EmployeeService` y
`PayrollRunService`. No se agregó funcionalidad.
**Resultado:** no se encontraron defectos de seguridad ni de autorización. Se
registran cuatro observaciones menores de usabilidad o accesibilidad, ninguna
corregida en esta fase (ver la justificación en la sección 4).

## 1. Verificaciones realizadas

| Verificación | Resultado | Evidencia |
|---|---|---|
| Política `[Authorize]` de la lista de empleados | Correcta: exige `HrEmployeesView` | `Components/Pages/Rrhh/Empleados.razor:3` |
| Política del formulario de empleado | Correcta: exige `HrEmployeesManage` | `Components/Pages/Rrhh/EmpleadoForm.razor:4` |
| Política de la página de nómina | Correcta: exige `HrPayrollManage` | `Components/Pages/Rrhh/Nomina.razor:3` |
| Salario oculto en la tabla sin `HrEmployeesManage` | Correcto: la página muestra `—` cuando `Salary` es nulo y el servicio lo devuelve nulo sin el permiso | `Empleados.razor:80-89`; `Application/EmployeeService.cs:68-73`, `:82`, `:215-240` |
| Salario oculto en el servicio (no solo en la vista) | Correcto: `List` y `Get` enmascaran el salario dentro de `ToRow` | `EmployeeService.cs:68`, `:82`, `:225`, `:237` |
| Redirección ante `AccessDeniedException` | Correcta en las tres páginas hacia `/acceso-denegado` | `Empleados.razor:126-129`; `EmpleadoForm.razor:158-161`, `:198-201`; `Nomina.razor:262-265` |
| Uso de `DomainErrorTranslator` | Correcto en las tres páginas para los errores no redirigidos | `Empleados.razor:130-133`; `EmpleadoForm.razor:162-165`, `:202-205`; `Nomina.razor:266-269` |
| Validaciones del formulario | Correctas: nombre, documento y cargo obligatorios y con longitud máxima; tienda ≥ 1; contrato obligatorio; salario ≥ 0 | `EmpleadoForm.razor:208-232`; `EmployeeService.cs:178-213` |
| Ruta doble del formulario (`/nuevo` y `/{Id:int}`) | Correcta: `Id` es `int`, vale `0` en `/nuevo` y `IsEditing` es `Id > 0`; la restricción `:int` excluye `nuevo` | `EmpleadoForm.razor:1-2`, `:116-117`, `:123` |
| Posibles `NullReferenceException` | No se encontró ninguno alcanzable: el formulario solo se pinta cuando `_options` y `_form` no son nulos, y `Save` usa `_form!` dentro de ese bloque; `PayrollRun` suma solo líneas con resultado | `EmpleadoForm.razor:25-29`, `:176`, `:187`; `PayrollRunService.cs:31-34`; `Nomina.razor:88`, `:131` |
| Auditoría sin datos personales ni montos | Correcta: empleados registra identificadores internos, tienda y estado; nómina registra cantidad de empleados y días | `EmployeeService.cs:122`, `:162-175`, `:242-250`; `PayrollRunService.cs:83`, `:138-146` |
| Entradas del menú coherentes con los permisos | Correctas: RR. HH. aparece con `HrEmployeesView` o `HrPayrollManage`; “Empleados” y “Nómina” se muestran solo con su permiso | `Components/Layout/NavMenu.razor:87-104` |
| Textos en español (es-CO) | Correctos; no se encontraron cadenas en inglés visibles al usuario | Las tres páginas y `NavMenu.razor` |
| Auditoría de intentos denegados | Delegada en `IAccessControl.Demand`, que registra el intento; las páginas no la duplican | `EmployeeService.cs:65`, `:78`, `:89`, `:99`, `:129`; `PayrollRunService.cs:67` |

## 2. Hallazgos por severidad

### 2.1 Ninguno de severidad alta o media

No se encontraron fallas de autorización, fugas de salario, redirecciones
incorrectas, excepciones no controladas ni mensajes de auditoría con datos
personales o montos.

### 2.2 Menor

**D-01 — El formulario de edición queda visible cuando el empleado no existe.**

- **Archivo:** `src/TiendaFlow.Web/Components/Pages/Rrhh/EmpleadoForm.razor:127-166`.
- **Qué ocurre:** `Load` crea un `_form` vacío antes de llamar a
  `EmployeeService.Get(Id)`. Si el identificador no existe, se captura
  `EmployeeNotFoundException` y se muestra el error, pero el `_form` vacío sigue
  montado y el `<EditForm>` se dibuja debajo. Al enviarlo se intenta un `Update`
  sobre un empleado inexistente y vuelve a fallar.
- **Impacto:** solo usabilidad; no hay escritura ni fuga de datos, porque el
  servicio vuelve a validar la existencia y el permiso. La página no muestra el
  salario de nadie.
- **Recomendación:** construir el formulario solo después de cargar el empleado,
  o limpiar `_form` en el `catch` y ocultar el formulario cuando hay error.
- **No corregido en esta fase** (ver sección 4).

**D-02 — Editar un empleado cuya tienda está inactiva no muestra la tienda actual.**

- **Archivo:** `EmpleadoForm.razor:131`, `:148`; `src/TiendaFlow.Application/EmployeeService.cs:87-95`, `:201-204`.
- **Qué ocurre:** el desplegable se llena con `ListStores()`, que filtra tiendas
  activas. Si el empleado pertenece a una tienda inactiva, ninguna opción
  coincide con su `StoreId`; el navegador muestra la primera tienda activa, pero
  el valor enlazado sigue siendo el de la tienda inactiva.
- **Impacto:** guardar sin tocar el desplegable produce el error controlado «La
  tienda seleccionada está inactiva». No se reasigna la tienda en silencio, pero
  el usuario no ve por qué falla.
- **Recomendación:** incluir la tienda actual como opción deshabilitada con la
  etiqueta «(inactiva)», o mostrar una advertencia junto al campo.
- **No corregido en esta fase** (ver sección 4).

### 2.3 Informativo

**D-03 — Los campos numéricos de la nómina no tienen validación de formulario.**

- **Archivo:** `Nomina.razor:27-37`, `:253-270`.
- **Qué ocurre:** días y clase de riesgo son `<input type="number">` sueltos,
  sin `EditForm` ni `ValidationMessage`. Los atributos `min`/`max` no impiden
  enviar un valor tecleado fuera de rango.
- **Impacto:** ninguno sobre el cálculo: `PayrollRunService.Calculate` valida
  1..30 y 1..5 antes de calcular y el error se traduce a español. Es solo
  comodidad de captura.
- **Recomendación:** envolver la captura en un `EditForm` con anotaciones de
  rango en el Sprint 3.

**D-04 — El desglose de nómina solo se abre con clic de ratón.**

- **Archivo:** `Nomina.razor:90`, `:272`.
- **Qué ocurre:** el detalle se selecciona con `@onclick` en una fila `<tr>`, sin
  `tabindex`, rol de botón ni manejo de teclado.
- **Impacto:** una persona que navega con teclado no puede abrir el desglose.
- **Recomendación:** usar un botón dentro de la fila o añadir navegación por
  teclado y el `aria` correspondiente.

**D-05 — El aviso de guardado exitoso depende solo de la URL.**

- **Archivo:** `Empleados.razor:26-29`, `:111-120`; `EmpleadoForm.razor:196`.
- **Qué ocurre:** el mensaje «El empleado se guardó correctamente» se muestra
  cuando la URL trae `?guardado=1`; no se limpia al refrescar y cualquier usuario
  con `HrEmployeesView` puede escribir la URL y verlo.
- **Impacto:** cosmético; no confirma ni altera ningún dato.
- **Recomendación:** usar un estado temporal en el servicio o quitar el
  parámetro de la URL con `Navigation.Replace`.

## 3. Comportamientos verificados que conviene conservar

- La lista de empleados exige `HrEmployeesView` y el formulario
  `HrEmployeesManage`; un usuario con solo lectura no ve el enlace «Registrar
  empleado» ni la columna de acciones de edición (`Empleados.razor:15-18`,
  `:65-68`, `:95-101`).
- El servicio vuelve a exigir el permiso en cada operación, de modo que una
  llamada directa al servicio no depende del ocultamiento en la UI
  (`EmployeeService.cs:65`, `:78`, `:89`, `:99`, `:129`; `PayrollRunService.cs:67`).
- La nómina calcula solo empleados activos y convierte los casos problemáticos
  (sin salario, contrato desconocido, entrada inválida) en líneas visibles en
  lugar de tumbar la corrida completa (`PayrollRunService.cs:74-129`).
- Los mensajes de auditoría de RR. HH. no incluyen nombres, documentos, correos
  ni montos salariales (`EmployeeService.cs:122`, `:162-175`;
  `PayrollRunService.cs:83`).

## 4. Por qué no se corrigió ningún hallazgo en esta fase

Los cuatro hallazgos son de usabilidad o accesibilidad, no de seguridad ni de
cálculo, y las páginas Razor no tienen pruebas automatizadas: el repositorio no
incluye bUnit (`docs/sprint2/open-questions.md`, pregunta 7). La instrucción de
esta fase pide que cada corrección venga con su prueba; agregar un proyecto de
pruebas de componentes excede el alcance de una pasada de verificación y
endurecimiento. Por eso se documentan aquí y se dejan como deuda para el
Sprint 3.

**Cambios de código en esta fase:** ninguno.
