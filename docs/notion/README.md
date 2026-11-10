# Paquete para Notion — Sprint 2

Este directorio contiene un archivo Markdown por sección de arc42, listo para importar en Notion. El contenido está en español, usa tablas Markdown bien formadas y no incluye HTML ni enlaces relativos.

## 1. Archivos y orden de páginas

| Orden | Archivo | Título que verá Notion | Contenido |
|---|---|---|---|
| 1 | `05-building-block-view.md` | 05 — Vista de bloques de construcción | Capas, módulo de nómina, empleados y corrida, trazabilidad |
| 2 | `07-deployment-view.md` | 07 — Vista de despliegue | Infraestructura local, secretos, CI y Terraform |
| 3 | `10-architecture-concepts-security.md` | 10 — Conceptos de arquitectura: seguridad y roles | Autenticación, matriz de 7 roles, tres capas y auditoría |

## 2. Pasos de importación

1. En Notion, abrir el espacio de trabajo del equipo.
2. Elegir **Importar** en la barra lateral y seleccionar **Markdown**.
3. Seleccionar los tres archivos de la tabla anterior, o arrastrar la carpeta completa.
4. Notion crea una página por archivo usando el primer encabezado como título.
5. Mover las páginas al lugar acordado del espacio y ordenarlas 05, 07 y 10.
6. Revisar que cada tabla se haya convertido en una tabla de Notion; si alguna fila queda como texto, volver a pegar esa tabla desde el archivo original.
7. Añadir una página índice llamada **Arquitectura TiendaFlow — Sprint 2** con enlaces internos a las tres páginas.

## 3. Diagramas

Los diagramas de nómina están en formato PlantUML:

- `docs/uml/payroll_sequence.puml`: secuencia del cálculo de nómina.
- `docs/uml/payroll_run_sequence.puml`: secuencia de la corrida mensual de empleados.

En el equipo no hay PlantUML instalado localmente (solo hay Java disponible), por lo que **no se generaron PNG en esta entrega**. Para generarlos:

1. Instalar PlantUML. Opciones habituales: `choco install plantuml`, `scoop install plantuml` o descargar `plantuml.jar` desde el sitio oficial.
2. Desde la raíz del repositorio, ejecutar:
   - Con el comando instalado: `plantuml -tpng docs/uml/payroll_sequence.puml docs/uml/payroll_run_sequence.puml`
   - Con el jar: `java -jar plantuml.jar -tpng docs/uml/payroll_sequence.puml docs/uml/payroll_run_sequence.puml`
3. Verificar que aparezcan `docs/uml/payroll_sequence.png` y `docs/uml/payroll_run_sequence.png`.
4. Arrastrar cada PNG a la página 05 de Notion, debajo de la sección 5.2.
5. Alternativa sin instalar nada: abrir el archivo `.puml` en la extensión PlantUML de Visual Studio Code y usar la vista previa para exportar la imagen.

Los demás diagramas del proyecto ya tienen su PNG en la raíz del repositorio y no forman parte de esta importación.

## 4. Recomendaciones de presentación

- Mantener las páginas 05 y 10 cerca en el árbol: son las que se muestran en el video de un minuto.
- En la página 10, fijar la tabla 10.2 como bloque destacado para que la fila `hr.payroll.manage` sea fácil de señalar.
- En la página 05, destacar la tabla 5.2 y el párrafo de decisión de arquitectura.
- No incluir contraseñas, cadenas de conexión ni valores de secretos en Notion. Los nombres de los secretos de desarrollo son `Seed:AdminPassword` y `Seed:DemoPassword`; sus valores nunca se documentan.
- Si se importa de nuevo una página, Notion puede duplicarla. Archivar la versión anterior antes de reimportar.
