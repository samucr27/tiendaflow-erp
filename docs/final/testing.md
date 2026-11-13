# Compilación, pruebas y cobertura

Última verificación sobre `feature/erp-complete`, con .NET 9 en Windows.

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
| `TiendaFlow.UnitTests` | 432 | 0 | 0 | 432 |
| `TiendaFlow.AcceptanceTests` | 6 | 0 | 0 | 6 |
| `TiendaFlow.IntegrationTests` | 20 | 0 | 0 | 20 |

Total ejecutado: **458 pruebas superadas**, sin omisiones ni errores.

Las pruebas de integración se ejecutaron contra un contenedor SQL Server
desechable (`tiendaflow-sql-test`, puerto 14333) mediante
`TIENDAFLOW_TEST_CONNECTION`. El fixture crea una base única, aplica todas las
migraciones en orden y ejecuta las pruebas; no toca la base de desarrollo. Sin
esa variable las pruebas se omiten, pero en esta verificación se ejecutaron.

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
| `TiendaFlow.Application` | 2100 | 2324 | **90,3 %** |
| `TiendaFlow.Domain` | 558 | 591 | **94,4 %** |
| Aplicación + dominio | 2658 | 2915 | **91,1 %** |

La lógica de negocio supera el objetivo de 85 %. La cobertura global del
reporte incluye `TiendaFlow.Infrastructure`: repositorios EF Core y migraciones
que solo se ejercitan con SQL Server. No se ocultó esa cifra ni se excluyó el
código generado para inflar el resultado; el filtro de Application + Domain es
el que usa la documentación del proyecto.

## Pruebas añadidas en esta fase

- `SystemParameterTests`: normalización de enteros, decimales, booleanos y
  texto; claves, alcances, versiones y fechas inválidas.
- `ParameterServiceTests`: catálogo con valores de código, valor global y por
  tienda, versionado, fecha duplicada o anterior, rangos, tienda inactiva,
  historial con autor, auditoría y negación sin permiso antes de consultar.
- `ParameterValuesTests`: valor por defecto, valor global, anulación por tienda,
  versión futura y clave desconocida.
- `ParameterVersioningIntegrationTests`: versiones reales sobre SQL Server,
  lectura de la última vigente, historial y auditoría persistida, más el índice
  único que rechaza un duplicado global.
- `RolePermissionsTests`: solo el Administrador administra parámetros.
- `FinalScreenAuthorizationTests`: política de las dos rutas de parámetros y
  enlace del menú.
- `StoreServiceTests`: alta con nombre único, edición, activación y
  desactivación con los tres bloqueos, reasignación de usuarios y empleados,
  auditoría y negación sin permiso antes de tocar repositorios.
- `StoreDirectoryServiceTests`: listado y detalle con `security.stores.view`,
  negación sin consultar y tienda inexistente.
- `StoreDirectoryIntegrationTests`: contadores reales de usuarios, empleados,
  stock y órdenes abiertas sobre SQL Server, más `ExistsName` y `NextId`.
- `RolePermissionsTests`: el Administrador administra tiendas y el Gerente
  corporativo solo consulta; ningún otro rol recibe esos permisos.
- `FinalScreenAuthorizationTests`: política de las tres rutas de tiendas y
  enlace del menú.
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
