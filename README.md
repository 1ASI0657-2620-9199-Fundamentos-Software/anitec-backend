# AniTec Platform Backend

Backend ASP.NET Core para AniTec.

## Tecnologias

- ASP.NET Core
- Entity Framework Core
- MySQL
- JWT para autenticacion
- BCrypt para hashing de contrasenas
- Repository, Unit of Work, Command Services, Query Services y REST Resources

## Bounded contexts

- `Shared`: base comun, repositorios, Unit of Work, EF Core, middleware y problem details.
- `Iam`: usuarios, sign-in, sign-up, JWT y hashing, basado en el repo del profesor.
- `Profiles`: perfiles de usuario, basado en el repo del profesor.
- `Livestock`: rebanos y animales.
- `Sanitary`: eventos sanitarios e historial medico de animales.
- `Financial`: ingresos y egresos.
- `Activities`: actividades de la granja.
- `Analytics`: metricas de reportes para dashboard.
- `Devices`: balanzas, collares inteligentes, camaras termicas, aretes de identificacion, estaciones meteorologicas y sensores ambientales.
- `Metrics`: lecturas generadas por los dispositivos.
- Suscripciones y pagos ya no estan en este proyecto: viven en `Anitec.Subscriptions` (servicio con base propia y pagos con Stripe en modo de prueba). Los datos existentes se mueven con `scripts/subscriptions-cutover/`.

## Base de datos

El backend usa MySQL. Antes de levantar la API, MySQL debe estar instalado y el servicio debe estar iniciado.

La cadena por defecto esta en `Anitec.Platform/appsettings.Development.json`:

```json
"DefaultConnection": "server=127.0.0.1;port=3306;user=root;password=password;database=anitec-platform"
```

Si tu usuario o contrasena de MySQL son diferentes, cambia ese valor antes de ejecutar el backend.

Para iniciar MySQL en Windows, puedes usar la aplicacion **Services** y verificar que `MySQL80` este en estado `Running`. Tambien puedes probar desde PowerShell como administrador:

```powershell
net start MySQL80
```

La base de datos esperada se llama `anitec-platform`. Si no existe, puedes crearla desde MySQL Workbench o desde consola:

```sql
CREATE DATABASE `anitec-platform`;
```

Al ejecutar la API, Entity Framework Core aplica automaticamente las migraciones pendientes con `context.Database.Migrate()` y luego inserta datos iniciales si la base esta vacia.

Tambien se puede usar la variable de entorno `ANITEC_CONNECTION_STRING` para generar migraciones en otra base.

## Migraciones

Cuando el SDK de .NET este disponible:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project Anitec.Platform --startup-project Anitec.Platform --output-dir Shared/Infrastructure/Persistence/EntityFrameworkCore/Migrations
dotnet ef database update --project Anitec.Platform --startup-project Anitec.Platform
```

Al ejecutar la API, tambien se aplican las migraciones pendientes con `context.Database.Migrate()`, siguiendo el mismo patron del backend usado en clase.

## Ejecucion Del Backend

Desde la carpeta raiz del backend:

```powershell
cd C:\Users\melga\Desktop\TrabajoFinalAppWeb\anitec-backend
dotnet run --project Anitec.Platform
```

La API quedara disponible en:

```text
http://localhost:5191/api/v1
```

Swagger quedara disponible en:

```text
http://localhost:5191/swagger
```

Si Swagger abre con HTTPS, tambien puede aparecer como:

```text
https://localhost:7003/swagger
```

## Ejecucion Completa Con Frontend

1. Iniciar MySQL.
2. Verificar la cadena de conexion en `Anitec.Platform/appsettings.Development.json`.
3. Levantar el backend:

```powershell
cd C:\Users\melga\Desktop\TrabajoFinalAppWeb\anitec-backend
dotnet run --project Anitec.Platform
```

4. En otra terminal, levantar el frontend:

```powershell
cd C:\Users\melga\Desktop\TrabajoFinalAppWeb\anitec-frontend
npm install
npm run dev
```

5. Abrir el frontend en el navegador:

```text
http://localhost:5173
```

## Usuarios Iniciales

El seed crea usuarios iniciales solo si la base de datos esta vacia. Todos usan la contrasena `anitec123`.

| Rol | Usuario |
| --- | --- |
| Rancher | `ganadero` |
| Rancher | `maria` |
| Rancher | `jose` |
| Rancher | `rosa` |
| Veterinarian | `veterinaria` |
| Veterinarian | `vetpedro` |
| Veterinarian | `vetlucia` |

Tambien se pueden crear usuarios reales desde el frontend en `/iam/sign-up`, seleccionando rol `Rancher` o `Veterinarian`.

## Validacion

Para comprobar que el backend compila:

```powershell
cd C:\Users\melga\Desktop\TrabajoFinalAppWeb\anitec-backend
dotnet build
```

## Ejecucion local con Docker Compose (balanceador, gateway y servicios)

`docker-compose.yml` levanta MySQL 8.4, dos replicas de la API (`api1` y `api2`), el servicio de suscripciones (`subscriptions`, con su propia base `anitec-subscriptions` y su propio usuario de MySQL), dos replicas del API Gateway (`gateway1` y `gateway2`, YARP) y un balanceador nginx con round-robin:

```
navegador -> lb (nginx :8080) -> gateway1 / gateway2 -> /api/v1/subscription-plans/**, /api/v1/subscriptions/** -> subscriptions
                                                     -> el resto (/api/v1/**, /uploads/**)                    -> api1 / api2
```

Las replicas y los gateways no guardan sesion (JWT); las replicas del core comparten la carpeta de imagenes subidas mediante un volumen. El servicio `db-init` crea la base y el usuario de suscripciones en cada arranque (es idempotente, tambien sirve con un volumen ya existente).

```bash
cp .env.example .env        # ajusta los valores; .env no se versiona
docker compose up --build
```

- Todo entra por `http://localhost:8080`. El frontend debe apuntar solo a esa direccion.
- Estado de cada destino segun el gateway: `http://localhost:8080/gateway/status` (solo con `Gateway__ExposeStatus=true`, activo en este entorno).
- La cabecera `X-Upstream` indica el gateway que atendio la peticion; `X-Correlation-Id` viaja hasta el servicio y vuelve en la respuesta.
- Stripe (modo de prueba): `STRIPE_SECRET_KEY` debe ser la clave secreta `sk_test_...` (no la publicable `pk_test_...`) y `STRIPE_WEBHOOK_SECRET` el `whsec_...` que imprime `stripe listen --print-secret`. Ambos pueden quedar vacios: el checkout responde entonces que Stripe no esta configurado. Para recibir el aviso de pago en local: `stripe listen --forward-to localhost:8080/api/v1/subscriptions/stripe/webhook`.
- Los servicios del core y de suscripciones corren en `Development` (usuarios de demostracion con clave `anitec123`, endpoints auxiliares de suscripciones); solo para uso local.
- Para detener un servicio y comprobar el aislamiento: `docker compose stop subscriptions` (el core sigue respondiendo) o `docker compose stop api1` / `gateway1` (el resto absorbe el trafico).
