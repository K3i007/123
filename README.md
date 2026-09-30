# Concesionaria

Base de la plataforma de concesionaria. La Fase 0 entrega autenticación, autorización, auditoría, una API versionada y el esqueleto público/administrativo.

## Requisitos

- Node.js 22+ y npm
- .NET SDK 8
- Docker Desktop con Docker Compose

## Arranque

1. Copia `.env.example` a `.env` y sustituye sus secretos de desarrollo.
2. Ejecuta `npm --prefix frontend install`.
3. En PowerShell ejecuta `./scripts/start.ps1`.

El script inicia PostgreSQL, API en `http://localhost:5080` (Swagger en `/swagger`) y frontend en `http://localhost:3000`. La API aplica la migración y crea el administrador solo en Development. Usa `SeedAdmin__Email` y `SeedAdmin__Password` de `.env` para iniciar sesión. El panel protegido está en `/admin`; la acción de semilla genera registros de auditoría que se consultan en `GET /api/v1/admin/audit` como administrador. Docker Compose fue verificado con Docker Desktop durante la Fase 2.

Docker Compose sigue **sin verificarse** en este equipo: aunque Docker Desktop está iniciado, `docker version` agotó el tiempo de espera al contactar el daemon el 30 de septiembre de 2026. Cuando el daemon responda, ejecuta `docker compose up -d` y `docker compose ps`; PostgreSQL quedará disponible en el puerto configurado por `POSTGRES_PORT` (por defecto `5433`). En un despliegue tras proxy inverso, configura únicamente sus IP en `ForwardedHeaders__KnownProxies__0`, etc.; la API no confía en cabeceras reenviadas de clientes directos.

## Verificación y Scripts de Prueba

- **Compilación y pruebas de unidad:**
  - Backend: `dotnet build backend/Dealership.sln && dotnet test backend/Dealership.sln`
  - Frontend: `npm --prefix frontend run lint && npm --prefix frontend run format:check && npm --prefix frontend run build`

- **Pruebas de aceptación e integración HTTP (Fase 1):**
  - `./scripts/test-inventory-http.ps1`: Valida autorización por roles, concurrencia optimista con cabecera `If-Match`, código `428 Precondition Required` ante ausencia de versión y `409 Conflict` ante versiones obsoletas.
  - `./scripts/demo-phase1-acceptance.ps1`: Ejecuta el flujo completo de aceptación de inventario: creación de vehículos, avance por estados de máquina (`InReview` -> `Photography`), bloqueo por permisos de avance manual a `Inspection` para inventario, override manual auditado con motivo obligatorio por Gerente, validación de publicación con precio obligatorio y creación/baja lógica de catálogos técnicos.

## Seguridad de desarrollo

No subas `.env`. `AUTH_COOKIE_SECURE=false` permite cookies HTTP únicamente para `localhost`; en todo entorno con HTTPS debe ser `true`. La API no escribe secretos en auditoría ni en los logs estructurados.

## Estado de verificación local y Docker

El CLI de Docker y Docker Compose se encuentran instalados en el sistema (`Docker version 29.8.1`, `Docker Compose version v5.5.1`). Si el demonio de Docker Desktop no se encuentra corriendo en segundo plano durante la sesión local, la plataforma se ejecuta directamente contra una instancia local de PostgreSQL (configurada en `ConnectionStrings__Default` en `.env`, típicamente puerto `5432` o `5433`). La configuración de `docker-compose.yml` mapea el puerto `POSTGRES_PORT` (por defecto `5433`) para evitar colisiones con instancias existentes.

Docker Compose requiere que Docker Desktop tenga el daemon activo. En la verificación más reciente el CLI estuvo disponible, pero el daemon `dockerDesktopLinuxEngine` no estaba iniciado, por lo que la ruta con Docker quedó sin verificar. Se debe ejecutar `docker compose up -d` antes de declarar esa ruta verificada.

Consulta [arquitectura](docs/architecture.md), la [matriz de permisos](docs/permissions-matrix.md) y las decisiones en `docs/decisions/`.
