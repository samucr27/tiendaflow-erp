# Guion del video: tour de arquitectura (máximo 1 minuto)

Este guion coincide con `docs/sprint2/video-guide.md`. Duración objetivo: 60 segundos. Palabras habladas: 105 (tope: 140). Lo graba una persona del equipo.

**Páginas de Notion:** `05 — Vista de bloques de construcción` y `10 — Conceptos de arquitectura: seguridad y roles`.

| Tiempo | Página y punto a señalar | Texto |
|---|---|---|
| 0:00–0:10 | Página 05, tabla **5.1**, cuatro filas de capas. | "Hola. TiendaFlow es un monolito modular en cuatro capas: Domain, Application, Infrastructure y Web. Domain y Application no conocen EF Core." |
| 0:10–0:30 | Página 05, sección **5.2**, filas `PayrollEngine` e `IPayrollStrategy` y recuadro **Decisión de arquitectura**. | "En la sección 5.2 está el módulo de nómina. PayrollEngine valida, elige la estrategia según el contrato y delega. Depende solo de interfaces, así que cumple SRP y DIP." |
| 0:30–0:50 | Página 10, tabla **10.2**, fila `hr.payroll.manage` y columna **HR**; luego tabla **10.3**. | "En la sección 10, la matriz de siete roles marca hr.payroll.manage solo para HR Officer. Por eso la nómina exige ese permiso en página, interfaz y servicio, y cada rechazo se audita." |
| 0:50–1:00 | Volver a página 05, sección **5.4**. | "La coherencia es esta: el bloque de nómina de la Sección 5 queda protegido por la matriz de roles de la Sección 10." |

**Frase de coherencia:** "La coherencia es esta: el bloque de nómina de la Sección 5 queda protegido por la matriz de roles de la Sección 10."

La lista de verificación previa a grabar está en `docs/sprint2/video-guide.md`.
