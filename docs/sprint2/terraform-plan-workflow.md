# Workflow de Terraform en pull requests — Sprint 2 (Tarea 3)

**Última actualización:** 16 de noviembre de 2026
**Archivo:** `.github/workflows/terraform-plan.yml`
**Infraestructura:** `infra/terraform`

La Tarea 3 de la guía pide investigar cómo configurar un workflow básico de GitHub
Actions que, al crear un pull request hacia `develop` con cambios en archivos `.tf`,
ejecute automáticamente `terraform plan`. El equipo lo implementó en dos etapas.

## 1. Disparador (igual en las dos etapas)

```yaml
on:
  pull_request:
    branches: [develop]
    paths:
      - '**/*.tf'
```

Solo se dispara en pull requests cuyo destino es `develop` y que modifican al menos un
archivo `.tf`. Un cambio que no toque Terraform no ejecuta el workflow.

## 2. Etapa 1: `terraform plan` automático (10 y 11 de noviembre)

Mientras `infra/terraform/main.tf` solo declaraba el recurso de ejemplo `random_pet`,
el workflow ejecutaba en GitHub Actions:

1. `actions/checkout@v4`.
2. `hashicorp/setup-terraform@v3` con el token de HCP Terraform del secreto
   `TF_API_TOKEN` (`cli_config_credentials_token`).
3. `terraform init -input=false` contra el backend remoto de HCP Terraform
   (organización `Tienda-Flow`, workspace `tiendaflow-infra`).
4. `terraform validate`.
5. `terraform plan -input=false`.

**Evidencia:** el job `plan` terminó en verde con los pasos *Terraform init*,
*Terraform validate* y *Terraform plan* en la corrida 42942551547 del pull request
de `feature/sprint2-erp` hacia `develop` (11 de noviembre de 2026):
<https://github.com/samucr27/tiendaflow-erp/actions/runs/42942551547>.

## 3. Etapa 2: validación en CI y `plan` en la máquina del operador (desde el 11 de noviembre)

Cuando `main.tf` pasó a describir la infraestructura real del ERP (grupo de recursos,
App Service Plan, Web App Linux, servidor y base de Azure SQL y reglas de firewall),
`terraform plan` necesita credenciales de Azure: el proveedor `azurerm` se autentica
contra la suscripción antes de calcular el plan.

La suscripción **Azure for Students** no permite crear *app registrations* ni
*service principals*, que son la forma de entregar credenciales de Azure a GitHub
Actions (secretos `ARM_*` u OIDC). Por eso el workspace de HCP Terraform quedó en modo
de ejecución **Local**: HCP guarda y bloquea el state, y el `plan` y el `apply` corren
en la máquina del integrante que tiene `az login`.

El workflow se renombró a **Terraform Validate** y ahora ejecuta en cada pull request
hacia `develop` con cambios `.tf`:

1. `terraform fmt -check -recursive`.
2. `terraform init -backend=false -input=false`.
3. `terraform validate`.

**Evidencia:** job *Format, init and validate* en verde en el pull request #7
(16 de noviembre de 2026):
<https://github.com/samucr27/tiendaflow-erp/actions/runs/43901753822>.

## 4. Estado frente a la Tarea 3

| Requisito | Estado |
|---|---|
| Workflow que se dispara en PR a `develop` con cambios `.tf` | Cumplido |
| `terraform plan` automático en ese PR | Cumplido en la etapa 1; en la etapa 2 el CI valida y el `plan` se ejecuta a mano |
| Estado remoto compartido (backend) | Cumplido: HCP Terraform, workspace `tiendaflow-infra` |

Para volver a tener `plan` automático con la infraestructura real hay dos caminos,
ambos fuera del alcance de una suscripción de estudiante:

1. **Credenciales dinámicas de HCP Terraform (OIDC)** con el workspace en modo
   *Remote*: HCP obtiene un token de Azure por cada corrida.
2. **Service principal** con los secretos `ARM_CLIENT_ID`, `ARM_CLIENT_SECRET`,
   `ARM_TENANT_ID` y `ARM_SUBSCRIPTION_ID` en GitHub, y de nuevo el paso
   `terraform plan -input=false` en el workflow.

## 5. Cómo se ejecuta el `plan` hoy

```powershell
az login
cd infra/terraform
terraform login            # token de HCP Terraform
terraform init
terraform plan -out tfplan
```

`terraform apply` nunca forma parte del workflow; el procedimiento completo está en
`docs/final/deploy-azure.md`.
