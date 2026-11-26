# Despliegue en AWS Elastic Beanstalk

Guía para publicar TiendaFlow en **AWS Elastic Beanstalk** con la base de datos en
**Amazon RDS for SQL Server**. Elastic Beanstalk es un servicio PaaS: crea la
instancia EC2, el grupo de seguridad, nginx como proxy y el monitoreo de salud a
partir de un paquete `.zip` con la aplicación.

> Los pasos que piden la cuenta de AWS los hace un integrante del equipo con su
> propia cuenta. Ningún secreto (contraseñas, cadenas de conexión) se guarda en el
> repositorio: se escriben en la consola de AWS.

## 1. Arquitectura

| Pieza | Servicio | Configuración para el curso |
|---|---|---|
| Aplicación | Elastic Beanstalk, plataforma **.NET sobre Amazon Linux 2023** (.NET 8 o superior) | Entorno *Single instance*, `t3.micro` (capa gratuita) |
| Base de datos | Amazon RDS for SQL Server **Express** | `db.t3.micro`, 20 GB, sin acceso público |
| Configuración | Propiedades de entorno de Elastic Beanstalk | `DB_CONNECTION_STRING` y contraseñas del sembrado |

La aplicación se publica *self-contained* para `linux-x64`: el paquete trae su
propio runtime de .NET 9, así que funciona con cualquier versión de la plataforma .NET sobre Amazon Linux 2023.

## 2. Generar el paquete

Desde la raíz del repositorio, en Windows:

```powershell
powershell -ExecutionPolicy Bypass -File deploy\aws\build-beanstalk-package.ps1
```

El resultado es `artifacts\tiendaflow-beanstalk.zip` (unos 53 MB) con:

- La aplicación publicada (`TiendaFlow.Web` y sus archivos).
- `Procfile`: `web: ./TiendaFlow.Web --urls http://0.0.0.0:5000`. Elastic Beanstalk
  configura nginx para reenviar el puerto 80 al 5000.
- `.ebextensions/01-tiendaflow.config`: variables sin secretos
  (`ASPNETCORE_ENVIRONMENT=Production`, carpeta de claves, `Authentication__RequireHttps=false`,
  `Database__MigrateOnStartup=true`), la URL de salud `/healthz` y los comandos que
  crean la carpeta de claves y dan permiso de ejecución al binario.

## 3. Crear la base de datos en RDS

1. Consola de AWS → **RDS** → **Create database** → *Standard create*.
2. Motor: **Microsoft SQL Server**, edición **Express**. Plantilla: **Free tier**.
3. Identificador: `tiendaflow-db`. Usuario maestro: `tiendaflow_admin`; contraseña propia.
4. Clase: `db.t3.micro`; almacenamiento 20 GB.
5. Conectividad: misma VPC por defecto que usará Elastic Beanstalk; **Public access: No**.
6. Crear y esperar el estado *Available*. Copiar el **Endpoint** (por ejemplo
   `tiendaflow-db.xxxxxxxx.us-east-1.rds.amazonaws.com`).

No hace falta crear la base `TiendaFlow`: la aplicación la crea y aplica las
migraciones al arrancar (`Database__MigrateOnStartup=true`).

## 4. Crear la aplicación y el entorno en Elastic Beanstalk

1. Consola → **Elastic Beanstalk** → **Create application**.
2. Entorno: **Web server environment**. Nombre de la aplicación: `tiendaflow`.
   Nombre del entorno: `tiendaflow-env`.
3. Plataforma: **.NET Core on Linux**, rama **.NET sobre 64bit Amazon Linux 2023** más reciente.
4. Código de la aplicación: **Upload your code** → `artifacts\tiendaflow-beanstalk.zip`,
   etiqueta de versión `v1`.
5. Presets: **Single instance (free tier eligible)**.
6. Acceso al servicio: crear o usar el rol de servicio y el perfil de instancia
   EC2 que propone el asistente (`aws-elasticbeanstalk-service-role`,
   `aws-elasticbeanstalk-ec2-role`).
7. Instancia: `t3.micro`.
8. **Environment properties** (paso *Configure updates, monitoring, and logging*):

   | Nombre | Valor |
   |---|---|
   | `DB_CONNECTION_STRING` | `Server=<endpoint de RDS>,1433;Database=TiendaFlow;User Id=tiendaflow_admin;Password=<contraseña>;Encrypt=True;TrustServerCertificate=True` |
   | `Seed__Enabled` | `true` (solo la primera vez, para cargar los datos de demostración) |
   | `Seed__AdminPassword` | Contraseña del usuario `admin@tiendaflow.local` |
   | `Seed__DemoPassword` | Contraseña de los demás usuarios demo |

9. Crear el entorno y esperar a que la salud quede en **Ok** (5 a 10 minutos).

## 5. Permitir que la instancia llegue a RDS

1. RDS → `tiendaflow-db` → pestaña *Connectivity & security* → grupo de seguridad.
2. **Edit inbound rules** → *Add rule*: tipo **MSSQL** (1433), origen: el grupo de
   seguridad de la instancia de Elastic Beanstalk (se llama
   `awseb-...-tiendaflow-env-...`).
3. Guardar. Si el entorno quedó en rojo porque no pudo conectarse, usar
   **Actions → Restart app server(s)**.

## 6. Verificar

1. Abrir la URL del entorno: `http://tiendaflow-env.eba-xxxxxxxx.us-east-1.elasticbeanstalk.com`.
2. `http://<url>/healthz` debe responder `200`.
3. Iniciar sesión con `admin@tiendaflow.local` y abrir Inventario, Proveedores,
   Cotizaciones y Órdenes de compra.
4. Si algo falla: **Logs → Request logs → Last 100 lines** y buscar en
   `web.stdout.log`.

Después de la primera publicación se puede cambiar `Seed__Enabled` a `false`.

## 7. Publicar una versión nueva

1. Generar otra vez el paquete con el script.
2. Elastic Beanstalk → entorno → **Upload and deploy** → elegir el `.zip` con una
   etiqueta nueva (`v2`, `v3`...). Las migraciones nuevas se aplican al arrancar.

## 8. Alternativa: AWS Toolkit for Visual Studio

Con el AWS Toolkit instalado: clic derecho sobre `TiendaFlow.Web` → **Publish to
AWS** → destino **AWS Elastic Beanstalk on Linux**. El asistente pide el nombre del
entorno, el tipo de instancia (`t3.micro`) y permite escribir las mismas
propiedades de entorno de la sección 4. El paquete del script de la sección 2 y el
del Toolkit son equivalentes; el script deja además la configuración de
`.ebextensions`.

## 9. Costos y limpieza

- `t3.micro` y `db.t3.micro` con SQL Server Express son las opciones más baratas y
  entran en la capa gratuita o en los créditos de una cuenta nueva. Las condiciones
  de la capa gratuita cambian; revisarlas en la consola de facturación antes de crear
  los recursos.
- Al terminar la evaluación: **Elastic Beanstalk → Actions → Terminate environment**
  y **RDS → Delete** (sin snapshot final), para no generar cobros.

## 10. Notas de seguridad

- El entorno de instancia única se sirve por **HTTP** porque no tiene certificado;
  por eso `.ebextensions` define `Authentication__RequireHttps=false`, que permite
  la cookie de sesión sin HTTPS. En un entorno real se usa un balanceador con un
  certificado de AWS Certificate Manager y se quita esa variable.
- La cadena de conexión usa `Encrypt=True`: el tráfico hacia RDS va cifrado.
- RDS no tiene acceso público; solo la instancia de la aplicación entra por la
  regla del grupo de seguridad.
