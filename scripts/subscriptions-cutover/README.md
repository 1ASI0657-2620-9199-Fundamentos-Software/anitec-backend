# Corte de datos de Subscriptions

El core ya no contiene el contexto de Subscriptions: planes, suscripciones y pagos viven en el servicio `Anitec.Subscriptions` con su propia base de datos. Esta carpeta tiene lo necesario para mover los datos existentes sin perderlos.

| Archivo | Para que sirve |
|---|---|
| `copy-subscriptions-data.sh` | Compara el origen (core) y el destino (Subscriptions) y copia suscripciones y pagos. Por defecto solo comprueba; con `--apply` copia. Nunca borra. |
| `drop-core-subscriptions-tables.sql` | Borra las tres tablas del core. Manual e irreversible; no es una migracion automatica a proposito. |

## Por que el borrado no es una migracion
El core aplica sus migraciones al arrancar. Una migracion que borre las tablas las eliminaria en produccion en el siguiente despliegue, sin comprobar la copia. Por eso el modelo del core ya no incluye las tablas (el snapshot esta actualizado y no hay cambios pendientes), pero las tablas siguen existiendo en las bases ya creadas hasta que alguien ejecute el SQL a mano.

## Que comprueba el script antes de copiar
1. Que el origen y el destino se puedan leer.
2. Que el destino no tenga ya suscripciones ni pagos (evita copiar dos veces).
3. Que no haya `stripe_subscription_id` ni `provider_payment_id` repetidos en el origen: el destino tiene indices unicos y rechazaria la copia. El core no tenia esos indices, asi que en una base real puede haber repetidos; el script los lista.
4. Que no haya suscripciones sin plan ni pagos sin suscripcion.
5. Que cada plan del origen exista igual en el destino (id, nombre, precio y maximo de animales). Los planes no se copian: el servicio crea los suyos (ids 1 a 3) y el campo `stripe_price_id` del destino queda vacio.

Si algo falla, no copia nada. Despues de copiar compara, tabla por tabla, el numero de filas y una suma de control de todas las columnas entre origen y destino.

## Uso local (Docker Compose)
```bash
set -a; source .env; set +a
export SOURCE_HOST=db SOURCE_USER=root SOURCE_PASSWORD="$MYSQL_ROOT_PASSWORD" SOURCE_SSL_MODE=DISABLED
export TARGET_HOST=db TARGET_USER=anitec_subscriptions TARGET_PASSWORD="${SUBSCRIPTIONS_DB_PASSWORD:-change-me-local-only}" TARGET_SSL_MODE=DISABLED
export DOCKER_NETWORK=anitec_default

./scripts/subscriptions-cutover/copy-subscriptions-data.sh           # solo comprueba
./scripts/subscriptions-cutover/copy-subscriptions-data.sh --apply   # copia
```

## Uso en la nube
Las variables `SOURCE_*` apuntan a la base del core (Aiven) y `TARGET_*` a la base de Subscriptions (Azure Database for MySQL), con `*_SSL_MODE=REQUIRED`. Las contrasenas se pasan solo por variables de entorno; no se escriben en comandos, archivos ni capturas. Puede hacer falta permitir la IP de la maquina que ejecuta el script en ambos servidores.

## Antes de borrar (checklist)
1. Hay un respaldo reciente del core (`mysqldump` de `anitec-platform`) guardado fuera de la base.
2. `copy-subscriptions-data.sh --apply` termino con "copia completada y verificada".
3. El gateway ya enruta `/api/v1/subscriptions/**` y `/api/v1/subscription-plans/**` al servicio y un usuario con suscripcion ve su plan y su historial de pagos.
4. Paso un periodo de observacion sin incidencias (a decidir por el equipo).

Solo entonces se ejecuta `drop-core-subscriptions-tables.sql` contra la base del core.

## Limitaciones
- Entre la copia y el cambio de rutas del gateway, una suscripcion o un pago registrados en el core no se copiarian (el script se niega a repetirse). Con pagos solo de prueba el riesgo es bajo; con datos reales haria falta una ventana de mantenimiento.
- Los usuarios borrados en el core siguen teniendo sus filas en Subscriptions: no hay llaves foraneas entre servicios.
- El script se probo contra el MySQL local del Compose (datos de demostracion, rechazo de conflictos y de una segunda copia). No se ejecuto contra Aiven ni Azure.
