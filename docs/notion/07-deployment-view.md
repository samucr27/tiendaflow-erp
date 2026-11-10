# 07 — Vista de despliegue

## 7.1 Nivel 1 de infraestructura

| Nodo | Tecnología | Contenido |
|---|---|---|
| Servidor de aplicación | .NET 9, Kestrel (`http://localhost:5212` en desarrollo) | Blazor Web App `TiendaFlow.Web` con renderizado interactivo en servidor: interfaz, endpoints Minimal API y autenticación por cookie. |
| Servidor de base de datos | SQL Server 2022 en un contenedor Docker (`tiendaflow-sql`, puerto 1433) | Base de datos `TiendaFlow_OpenCode`, creada a partir de las migraciones de EF Core. |
| Integración continua | GitHub Actions (`.github/workflows/ci.yml`, .NET 9.0.x) | Restore, build y test con cobertura XPlat en cada push. |
| Infraestructura como código | Terraform con estado remoto en HCP Terraform (`.github/workflows/terraform-plan.yml`, `infra/terraform/main.tf`) | `terraform init`, `validate` y `plan` en pull requests a `develop`. El recurso actual es un *placeholder* declarado. |

## 7.2 Configuración y secretos

- La cadena de conexión y las contraseñas de los usuarios de demostración viven solo en `dotnet user-secrets` durante el desarrollo. Nunca se versionan.
- En Development, un seeder hospedado crea el administrador y un usuario de demostración por rol cuando la base está vacía.
- Las pruebas de integración solo se ejecutan si existe la variable `TIENDAFLOW_TEST_CONNECTION`. En GitHub Actions, donde no hay SQL Server, se omiten y las pruebas unitarias y de aceptación siguen ejecutándose.
- Los nombres de los secretos de desarrollo son `Seed:AdminPassword` y `Seed:DemoPassword`. Sus valores no se escriben en la documentación.

## 7.3 Flujo de despliegue

1. El equipo trabaja en una rama de feature y ejecuta build y pruebas en local.
2. El push dispara el CI: build y pruebas con cobertura.
3. `dotnet ef database update` aplica las migraciones, una por fase.
4. La automatización de infraestructura con Terraform y estado remoto es el siguiente paso; el plan se ejecuta en pull requests a `develop`.

## 7.4 Integración continua

| Paso | Comando o acción | Resultado esperado |
|---|---|---|
| Checkout | `actions/checkout@v4` | Código de la rama |
| SDK | `actions/setup-dotnet@v4` con .NET 9.0.x | SDK disponible |
| Restore | `dotnet restore` | Paquetes restaurados |
| Build | `dotnet build --no-restore` | 0 advertencias y 0 errores |
| Test | `dotnet test --no-build --collect:"XPlat Code Coverage"` | Pruebas unitarias y de aceptación en verde; integración omitida sin SQL Server |

## 7.5 Infraestructura como código

- `infra/terraform/main.tf` declara el estado remoto en la organización `Tienda-Flow`, workspace `tiendaflow-infra`.
- El proveedor `random` y el recurso `random_pet` demuestran el flujo de estado remoto y el plan automático sin necesitar credenciales de nube.
- El workflow de Terraform se ejecuta solo en pull requests a `develop` y solo cuando cambian archivos `.tf`.
- El token de HCP Terraform se toma del secreto `TF_API_TOKEN`; no se escribe en el repositorio.
- Cuando se elija el destino real de despliegue, el recurso de ejemplo se reemplaza por la base de datos y el hosting de la aplicación.

## 7.6 Limitaciones y notas de operación

- El despliegue actual es de **desarrollo local**: no hay ambiente de staging ni producción automatizada.
- La base de datos de desarrollo se recrea con el seeder; los salarios vuelven a `0` y la corrida de nómina debe recalcularse.
- La corrida de nómina no se persiste; no hay tablas de corrida ni de detalle en esta entrega.
- El contenedor `tiendaflow-sql` es el único entorno que usa la demo. No se deben borrar volúmenes ni contenedores de otros entornos.
