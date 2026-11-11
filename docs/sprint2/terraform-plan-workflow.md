# Workflow de `terraform plan` — Sprint 2

**Fecha:** 10 de noviembre de 2026
**Archivo:** `.github/workflows/terraform-plan.yml`
**Infraestructura:** `infra/terraform`

## Qué hace

El workflow `Terraform Plan` valida y planifica la infraestructura como código en cada
pull request. Se ejecuta en GitHub Actions sobre `ubuntu-latest` con estos pasos:

1. `actions/checkout@v4` descarga el repositorio.
2. `hashicorp/setup-terraform@v3` instala Terraform y toma el token de HCP Terraform
   desde el secreto `TF_API_TOKEN`.
3. `terraform init -input=false` inicializa el directorio de trabajo y descarga el
   proveedor declarado.
4. `terraform validate` comprueba la sintaxis y la coherencia de la configuración.
5. `terraform plan -input=false` calcula el plan sin aplicar cambios.

Todos los comandos se ejecutan con `working-directory: infra/terraform`.

## Disparador

```yaml
on:
  pull_request:
    branches: [develop]
    paths:
      - '**/*.tf'
```

Solo se dispara en pull requests cuyo destino es `develop` y que modifican al menos un
archivo `.tf`. Un cambio que no toque Terraform no ejecuta el workflow.

## Credenciales y backend

El plan no necesita credenciales de un proveedor de nube: la configuración actual solo
usa el proveedor `random` y un recurso de ejemplo (`random_pet`), sin crear recursos en
AWS, Azure ni GCP.

Sin embargo, **sí requiere el token `TF_API_TOKEN`** porque `infra/terraform/main.tf`
declara un backend remoto de HCP Terraform:

```hcl
cloud {
  organization = "Tienda-Flow"
  workspaces {
    name = "tiendaflow-infra"
  }
}
```

`terraform init` se conecta a ese backend para leer y bloquear el state. Si el secreto
`TF_API_TOKEN` no existe o no tiene acceso al workspace, el workflow falla en `init`;
no llega a `validate` ni a `plan`.

## Limitación documentada

- El backend remoto **ya está declarado**, pero no se implementó ni se aprovisionó en
  esta entrega. No se creó el workspace, no se configuraron variables ni se migró un
  state local.
- El workflow depende de un secreto de HCP Terraform. No se debe asumir que el plan
  funciona en un fork sin configurar ese secreto.
- El recurso `random_pet` es un marcador de posición para demostrar el plan; no
  representa la infraestructura real del ERP.
- `terraform apply` no forma parte del workflow: solo se valida y se planifica.

## Verificación

El contenido del workflow y de `main.tf` se revisó el 10 de noviembre de 2026. No se
ejecutó el workflow en GitHub Actions durante esta entrega, porque requiere un pull
request hacia `develop` y el secreto `TF_API_TOKEN`; la validación local de Terraform
tampoco se ejecutó.
