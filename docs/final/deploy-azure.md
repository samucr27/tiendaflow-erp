# Despliegue de TiendaFlow en Azure

Esta guía deja la aplicación lista para que la aprovisiones y despliegues tú.
No crea recursos por sí sola: todos los comandos de `apply`, `az` y migración
los ejecutas en tu máquina, con tu sesión de Azure.

## 1. Contexto y restricciones reales

- Suscripción: **Azure for Students** dentro de un tenant universitario.
- Regiones permitidas por la política de la suscripción:
  `canadacentral`, `chilecentral`, `spaincentral`, `belgiumcentral` y
  `francecentral`.
- La cuenta no puede crear *app registrations* ni *service principals*
  (`Insufficient privileges`). Por eso **no hay OIDC, ni federación de
  identidades, ni credenciales `ARM_*`** en este proyecto.
- Terraform se ejecuta en tu máquina con `az login`. El workspace de HCP
  Terraform (`Tienda-Flow/tiendaflow-infra`) se usa en modo **Local** y
  **solo guarda el state**.
- El plan de App Service es Linux **B1** y la base de datos es **Azure SQL
  Basic**, los tamaños pequeños que caben en el crédito de estudiante.

## 2. Qué crea Terraform

| Recurso | Nombre aproximado | Notas |
| --- | --- | --- |
| Resource group | `rg-tiendaflow-<sufijo>` | Contiene todo |
| App Service plan | `plan-tiendaflow-<sufijo>` | Linux, B1, `always_on` y WebSockets |
| Linux Web App | `app-tiendaflow-<sufijo>` | .NET 9, HTTPS obligatorio, health check `/healthz` |
| Azure SQL Server | `sql-tiendaflow-<sufijo>` | TLS 1.2 mínimo, endpoint público |
| Azure SQL Database | `TiendaFlow` | Tier Basic, 2 GB |
| Regla de firewall | `AllowAzureServices` | Permite que la Web App llegue a SQL |
| Regla de firewall opcional | `AllowClientIp` | Solo si defines `client_ip_address` |

El sufijo aleatorio (`random_string`) hace únicos los nombres globales. La
salida `app_url` es la URL HTTPS final.

## 3. Requisitos en tu máquina

- Azure CLI y sesión iniciada.
- Terraform 1.6 o superior.
- .NET 9 SDK.
- `dotnet-ef` para el bundle de migraciones.
- Cuenta de HCP Terraform con acceso al workspace `tiendaflow-infra`.

Comprueba las versiones:

```powershell
az version
terraform version
dotnet --version
```

## 4. Preparar la sesión de Azure y HCP

```powershell
az login
az account list --output table
az account set --subscription "<id-de-la-suscripcion>"
```

El workspace debe estar en **Execution Mode: Local** en HCP Terraform. En ese
modo el state vive en HCP, pero el plan y el apply corren en tu máquina y usan
tu sesión de `az login`.

Para que Terraform pueda leer y escribir el state remoto, inicia sesión en HCP:

```powershell
terraform login
```

No hace falta ningún token de Azure en HCP ni en GitHub.

## 5. Variables de Terraform

Las variables sensibles se pasan localmente. La forma más simple es crear
`infra/terraform/terraform.tfvars`, que está ignorado por Git (`*.tfvars`) y
**nunca debe commitearse**:

```hcl
location          = "canadacentral"
sql_admin_login   = "tiendaflowadmin"
sql_admin_password = "<contraseña-larga-y-unica>"
client_ip_address = "<tu-ip-publica>"

# Déjalo en false para un despliegue normal.
seed_enabled = false
```

Si vas a habilitar los usuarios demo, añade también:

```hcl
seed_enabled        = true
seed_admin_password = "<contraseña-del-admin>"
seed_demo_password  = "<contraseña-de-los-demo>"
```

`seed_enabled = true` solo crea usuarios cuando la base ya tiene el esquema
migrado. En producción se recomienda `false`; la aplicación no crea cuentas
demo si no se pide explícitamente.

Para obtener tu IP pública:

```powershell
(Invoke-RestMethod https://api.ipify.org)
```

Si tu IP cambia, actualiza `client_ip_address` y vuelve a aplicar. La regla
`AllowClientIp` es la que permite ejecutar el bundle de migraciones desde tu
máquina. `AllowAzureServices` es la que usa la Web App.

### Regiones y capacidad

Si `terraform apply` falla porque la región no tiene capacidad para B1 o para
Azure SQL, cambia `location` a otra de las cinco permitidas y vuelve a
planificar. No intentes regiones fuera de la lista: la validación de la
variable las rechaza con un mensaje explícito.

## 6. Primer aprovisionamiento

```powershell
cd infra/terraform
terraform init
terraform fmt -check -recursive
terraform validate
terraform plan -out=tfplan
terraform apply tfplan
```

`terraform init` descarga los proveedores y conecta el state de HCP. El plan
muestra todos los recursos antes de crear nada. Revisa que no aparezca ningún
recurso fuera de la suscripción esperada.

Al terminar:

```powershell
terraform output app_url
terraform output app_service_name
terraform output resource_group_name
terraform output sql_server_fqdn
```

## 7. Migraciones de base de datos

La aplicación **no** migra sola en producción: `Database:MigrateOnStartup` es
`false` por defecto y Terraform lo fija en `false`. El esquema se aplica con un
bundle de EF Core.

### 7.1 Generar el bundle en tu máquina

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations bundle `
  --project src/TiendaFlow.Infrastructure/TiendaFlow.Infrastructure.csproj `
  --startup-project src/TiendaFlow.Web/TiendaFlow.Web.csproj `
  --configuration Release `
  --self-contained `
  --runtime win-x64 `
  --output efbundle.exe `
  --force
```

El ejecutable queda en `efbundle.exe` (no lo commitees). El comando no aplica
nada todavía.

### 7.2 Aplicar el bundle contra Azure SQL

Construye la cadena de conexión con los datos de `terraform output` y la
contraseña que pusiste en `sql_admin_password`. Guárdala en una variable de
PowerShell para que no quede en el historial:

```powershell
$cs = Read-Host "Connection string de Azure SQL"
.\efbundle.exe --connection $cs
```

La cadena debe incluir `Encrypt=True` y el servidor
`<sql_server_fqdn>,1433`. Si el bundle falla por timeout, revisa que
`client_ip_address` sea tu IP actual y que la regla `AllowClientIp` exista en
el servidor SQL.

Después de migrar, la aplicación puede arrancar con el esquema completo. No
hace falta activar `Database:MigrateOnStartup`.

### 7.3 Migraciones futuras

Cuando agregues una migración nueva:

```powershell
dotnet ef migrations add NombreDeLaMigracion `
  --project src/TiendaFlow.Infrastructure/TiendaFlow.Infrastructure.csproj `
  --startup-project src/TiendaFlow.Web/TiendaFlow.Web.csproj
```

Genera el bundle otra vez y ejecútalo con `--connection`. No apliques
migraciones contra la base de desarrollo desde esta guía.

## 8. Despliegue de la aplicación

### 8.1 Camino principal: manual con `az webapp deploy`

```powershell
dotnet publish src/TiendaFlow.Web/TiendaFlow.Web.csproj `
  --configuration Release `
  --output publish

Compress-Archive -Path publish\* -DestinationPath tiendaflow.zip -Force

az webapp deploy `
  --resource-group "<resource_group_name>" `
  --name "<app_service_name>" `
  --src-path tiendaflow.zip `
  --type zip
```

La Web App tiene `webdeploy_publish_basic_authentication_enabled = true` para
que funcione el perfil de publicación. Ese ajuste habilita la autenticación
básica de despliegue; no la uses para tráfico de usuarios y rota el perfil de
publicación si se filtra.

### 8.2 Camino secundario: GitHub Actions con publish profile

El workflow `.github/workflows/azure-deploy.yml` es **solo manual**
(`workflow_dispatch`). No usa OIDC ni `azure/login`.

1. En Azure Portal, abre la Web App y descarga **Get publish profile**.
2. En GitHub, crea el secreto `AZURE_WEBAPP_PUBLISH_PROFILE` con el contenido
   completo del archivo descargado.
3. Crea la variable de repositorio `AZURE_WEBAPP_NAME` con el nombre de la Web
   App (por ejemplo `app-tiendaflow-abc123`).
4. Ejecuta el workflow desde **Actions → Azure Deploy → Run workflow**.

El workflow compila, prueba, publica, genera el bundle de migraciones
`tiendaflow-efbundle` como artefacto y despliega con `azure/webapps-deploy`.
El bundle no se ejecuta solo: descárgalo y aplícalo desde tu máquina con la
regla de firewall, igual que en el apartado 7.

No se necesitan `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
ni ninguna credencial `ARM_*`.

## 9. Verificación después del despliegue

```powershell
$app = terraform output -raw app_url
Invoke-WebRequest "$app/healthz" | Select-Object StatusCode, Content
```

Respuesta esperada: `200` y un JSON con `"status":"healthy"`. El health check
consulta la base; si devuelve `503`, revisa la cadena de conexión, las reglas de
firewall y que el bundle de migraciones se haya ejecutado.

Comprueba también:

- `https://<app>/login` carga por HTTPS y redirige correctamente.
- El menú solo muestra módulos para los que el usuario tiene permiso.
- Los datos demo aparecen solo si `seed_enabled = true` y el seed se ejecutó.
- Los logs de la Web App muestran el arranque y las peticiones en consola
  (App Service → Log stream).

El workflow de validación de Terraform solo ejecuta `fmt`, `init -backend=false`
y `validate`. No ejecuta `plan` porque no hay credenciales de Azure en CI y la
suscripción no permite crear un service principal. El plan real es el paso
manual de esta guía.

## 10. Costos y límites de una cuenta de estudiante

- El crédito de Azure for Students es finito. B1 y Azure SQL Basic consumen
  crédito mientras existan, aunque la app no reciba tráfico.
- Los precios y la disponibilidad cambian por región y por mes. Consúltalos en
  la calculadora de precios de Azure antes de aprovisionar; esta guía no fija
  tarifas.
- La suscripción puede tener cuotas y restricciones de capacidad por región.
  Si una región falla, prueba otra de las cinco permitidas.
- Para dejar de gastar, elimina el resource group completo con
  `terraform destroy` cuando ya no necesites la demo. Un App Service detenido
  puede seguir generando costo de plan; la base de datos Basic no se pausa sola.
- No uses esta configuración para datos reales de una empresa: los usuarios
  demo, el rating manual de proveedores y los reportes son funcionalidad
  académica.

## 11. Problemas frecuentes

| Síntoma | Causa probable | Acción |
| --- | --- | --- |
| `location` rechazada | Región fuera de la política | Usa una de las cinco permitidas |
| `terraform apply` sin capacidad | Región saturada para B1/SQL Basic | Cambia de región y replanifica |
| `terraform init` pide token | Workspace HCP sin sesión | Ejecuta `terraform login` |
| `az webapp deploy` pide login | Sesión de Azure expirada | `az login` y reintenta |
| `/healthz` devuelve 503 | SQL inaccesible o sin migrar | Revisa firewall, cadena y bundle |
| El bundle de migración no conecta | IP pública cambió | Actualiza `client_ip_address` y aplica |
| El workflow no despliega | Falta el secreto o la variable | Revisa `AZURE_WEBAPP_PUBLISH_PROFILE` y `AZURE_WEBAPP_NAME` |
