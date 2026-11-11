# Guía del video — tour de arquitectura (máximo 1 minuto)

**Duración objetivo:** 60 segundos.
**Palabras habladas:** 137 (tope: 140 en español hablado).
**Lo graba:** una persona del equipo. Este documento es el guion y la lista de verificación; no es una grabación automática.
**Páginas de Notion:** `05 — Vista de bloques de construcción` y `10 — Conceptos de arquitectura: seguridad y roles`. La página `07 — Vista de despliegue` no se muestra por límite de tiempo.

## 1. Guion por tramos

| Tiempo | Página de Notion | Qué mostrar y señalar | Texto hablado |
|---|---|---|---|
| 0:00–0:10 | 05 — Vista de bloques de construcción | Señalar la tabla **5.1** y las cuatro filas de capas. Mantener visible la frase "Domain y Application no conocen EF Core". | "Hola. TiendaFlow es un monolito modular en cuatro capas: Domain, Application, Infrastructure y Web. Domain y Application no conocen EF Core." |
| 0:10–0:30 | 05 — misma página, sección **5.2** y luego **5.4 a 5.6** | Señalar las filas `PayrollEngine` e `IPayrollStrategy` de la tabla 5.2, el recuadro **Decisión de arquitectura** y los encabezados de Compras, Facturación y CRM. | "En la sección 5 están los módulos. Nómina usa PayrollEngine: valida, elige la estrategia según el contrato y delega en interfaces, así que cumple SRP y DIP. Compras, Facturación y CRM siguen el mismo patrón: entidad en Domain, servicio de caso de uso en Application, repositorio y migración en Infrastructure, y páginas Blazor en Web." |
| 0:30–0:50 | 10 — Conceptos de arquitectura: seguridad y roles | Señalar la tabla **10.2**, las filas `hr.payroll.manage` y `sales.invoices.manage`. Luego bajar brevemente a la tabla **10.3**. | "En la sección 10, una sola matriz de permisos asigna cada función a los siete roles. Por ejemplo, hr.payroll.manage es solo para HR Officer; sales.invoices.manage cubre Administrador, Gerente Corporativo, Jefe de Tienda y Cajero." |
| 0:50–1:00 | 05 — volver a la sección **5.7** | Señalar el párrafo de coherencia con la Sección 10. | "La coherencia es esta: cada módulo de la Sección 5 exige en página, interfaz y servicio el permiso que la matriz de la Sección 10 le asigna." |

## 2. Frase de coherencia

> "La coherencia es esta: cada módulo de la Sección 5 exige en página, interfaz y servicio el permiso que la matriz de la Sección 10 le asigna."

Debe decirse completa en el último tramo, mirando la sección 5.7 de la página 05.

## 3. Antes de grabar

- [ ] Importar las páginas 05, 07 y 10 en Notion y verificar que las tablas se vean como tablas.
- [ ] Abrir dos pestañas o dos ventanas: página 05 en la sección 5.1/5.2/5.4–5.6 y página 10 en la tabla 10.2.
- [ ] Ajustar el zoom de Notion entre 125 % y 150 % para que las tablas y las filas `hr.payroll.manage` y `sales.invoices.manage` se lean sin desplazamiento horizontal.
- [ ] Cerrar notificaciones, correo, chat y cualquier pestaña con datos personales o secretos.
- [ ] Verificar que no se vea ninguna terminal, archivo `.env`, cadena de conexión ni valor de `Seed:AdminPassword` o `Seed:DemoPassword`.
- [ ] Probar el micrófono y hacer una lectura cronometrada de 60 segundos.
- [ ] Grabar en 16:9, 1080p, con el puntero visible y movimientos lentos.
- [ ] No mostrar capturas de pantalla ni diapositivas: solo las páginas de Notion.
- [ ] Confirmar que el texto hablado no supere las 140 palabras; el guion tiene 137.
- [ ] Al terminar, revisar que la frase de coherencia se escuche completa y que el video dure máximo 1 minuto.

## 4. Notas de producción

- No leer las tablas completas: señalar solo la fila o columna indicada.
- Si se comete un error, repetir el tramo completo; no cortar a mitad de una frase.
- El video no necesita música ni edición. Un solo plano de pantalla con voz es suficiente.
- Guardar el archivo como `tour-arquitectura-sprint2.mp4` y no subirlo al repositorio.
