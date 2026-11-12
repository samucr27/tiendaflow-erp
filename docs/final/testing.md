# Compilación, pruebas y cobertura

Última verificación sobre `feature/erp-final`, con .NET 9 en Windows.

## Comandos

```powershell
dotnet build TiendaFlow.slnx
dotnet test TiendaFlow.slnx --no-build
dotnet test TiendaFlow.slnx --no-build --collect:"XPlat Code Coverage" --results-directory TestResults
```

## Resultado de compilación

```text
Compilación correcta.
    0 Advertencia(s)
    0 Errores
```

## Resultado de pruebas

| Proyecto | Superadas | Omitidas | Con error | Total |
| --- | ---: | ---: | ---: | ---: |
| `TiendaFlow.UnitTests` | 339 | 0 | 0 | 339 |
| `TiendaFlow.AcceptanceTests` | 6 | 0 | 0 | 6 |
| `TiendaFlow.IntegrationTests` | 0 | 17 | 0 | 17 |

Total ejecutado: **345 pruebas superadas** y 17 omitidas.

Las pruebas de integración se omiten porque requieren una cadena de conexión a
SQL Server en `TIENDAFLOW_TEST_CONNECTION` o
`ConnectionStrings__DefaultConnection`. No se levantó la aplicación ni se
aplicaron migraciones durante esta entrega, así que esas pruebas no se
ejecutaron localmente.

## Cobertura

La cobertura se recogió con `coverlet.collector` y se agregó con
ReportGenerator usando el mismo filtro que documenta el README:

```powershell
reportgenerator -reports:"TestResults\**\coverage.cobertura.xml" `
  -targetdir:coveragereport -reporttypes:TextSummary `
  "-assemblyfilters:+TiendaFlow.Application;+TiendaFlow.Domain"
```

| Capa | Líneas cubiertas | Líneas válidas | Cobertura de línea |
| --- | ---: | ---: | ---: |
| `TiendaFlow.Application` | 1796 | 1988 | **90,3 %** |
| `TiendaFlow.Domain` | 502 | 546 | **91,9 %** |
| Aplicación + dominio | 2298 | 2534 | **90,7 %** |

La lógica de negocio supera el objetivo de 80 %. La cobertura global del
reporte es 35,0 % porque incluye `TiendaFlow.Infrastructure`: repositorios EF
Core y migraciones que solo se ejercitan con SQL Server y cuyas pruebas están
omitidas en este entorno. No se ocultó esa cifra ni se excluyó el código
generado para inflar el resultado; el filtro de Application + Domain es el que
usa la documentación del proyecto.

## Pruebas añadidas en esta fase

- `BusinessDashboardServiceTests`: ventas del día, órdenes abiertas y
  verificación de que sin permiso no se consultan los repositorios.
- `ReportingTests`: CSV con comas, comillas y saltos de línea; totales de
  inventario; rango local de ventas; nómina con empleados incompletos.
- `SupplierServiceTests`: ranking y evaluación ya existentes, ampliados por la
  pantalla de ranking.
- `CustomerSegmentationServiceTests`: regla 20/30, clientes sin compras,
  facturas anuladas y permiso.
- `ElectronicInvoiceTests`: campos del XML, escape de caracteres, digest
  determinista, factura anulada, permiso y factura inexistente.
- `FinalScreenAuthorizationTests`: políticas de las rutas nuevas, enlaces del
  menú y aviso de que el XML no es válido ante la DIAN.

## Cómo reproducir la verificación

```powershell
dotnet build TiendaFlow.slnx
dotnet test TiendaFlow.slnx --no-build
dotnet test TiendaFlow.slnx --no-build --collect:"XPlat Code Coverage" --results-directory TestResults
```

La carpeta `TestResults/` está ignorada por Git. No se deben subir los archivos
de cobertura ni los resultados de la ejecución.
