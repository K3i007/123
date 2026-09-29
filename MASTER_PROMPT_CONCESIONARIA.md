# MASTER PROMPT: Plataforma web para concesionaria de vehículos

Eres un arquitecto y desarrollador full-stack senior. Vas a construir, **por fases**, una plataforma profesional lista para producción para una concesionaria de vehículos de segunda mano. Lee todo este documento antes de escribir código. Trabaja **solo la fase que se te indique al final** y no adelantes funcionalidades de fases posteriores, pero deja la arquitectura preparada para ellas.

---

## 1. Visión

Una concesionaria digital centralizada: sitio público de venta + CRM + administración interna + flujo de fotografía (AutoShoot) + inspección + alertas de búsqueda + notificaciones. Todo el ciclo del vehículo queda trazado: **entra al inventario → se prepara (fotos, inspección) → se publica → genera interés/oportunidades → se cotiza/prueba de manejo → se vende → se entrega.**

No es un marketplace entre particulares: el inventario es de la concesionaria.

Los módulos están conectados entre sí: inventario ↔ fotos ↔ AutoShoot ↔ publicación ↔ alertas ↔ clientes ↔ CRM ↔ oportunidades/cotizaciones/pruebas de manejo/ventas.

## 2. Stack (obligatorio salvo que propongas una mejora justificada)

| Capa | Tecnología |
|---|---|
| Frontend | Next.js (App Router) + React + **TypeScript estricto** |
| Estilos | **Tailwind CSS** como sistema principal. CSS propio solo si Tailwind no resuelve un caso concreto |
| Backend | **ASP.NET Core Web API** con C# (nullable habilitado, tipado fuerte) |
| Base de datos | **PostgreSQL** con Entity Framework Core (migraciones) |
| API | REST con documentación **OpenAPI/Swagger** |
| Segundo plano | Servicios de tareas del backend (hosted services / Hangfire o Quartz, justifica tu elección) |
| Integraciones | Meta (Facebook y WhatsApp) con sincronización autorizada, correo, otras a futuro |

Frontend y backend van **separados pero en la misma solución/monorepo**.

## 3. Requisitos no funcionales (aplican a TODAS las fases)

- **Arquitectura limpia** y por capas/módulos (Domain, Application, Infrastructure, API). Separación clara de responsabilidades, sin lógica de negocio en controladores.
- **Seguridad en toda la plataforma**: autenticación, autorización por roles y permisos, validación de entradas en servidor, protección de datos personales (nunca en zonas públicas), no registrar contraseñas/tokens/claves en logs, CORS restrictivo, rate limiting, cabeceras de seguridad, secretos fuera del repositorio.
- **Errores**: manejo global; el usuario ve mensajes comprensibles con qué hacer a continuación; el detalle técnico se registra internamente con un identificador de correlación. Nunca exponer detalles internos al usuario final (usar ProblemDetails).
- **Trazabilidad y auditoría** de acciones importantes: cambios de precio, publicación, cambios de permisos, inspecciones, acciones de procesos automáticos (quién/qué, cuándo, valor anterior y nuevo).
- **Rendimiento**: paginación en servidor, índices adecuados, consultas eficientes, caché donde aporte, imágenes optimizadas.
- **Accesibilidad** (WCAG 2.1 AA como objetivo): semántica correcta, foco visible, navegación por teclado, contraste, etiquetas.
- **Responsive real**: nada debe solaparse en móvil (textos, botones, imágenes, controles). Aplica a catálogo, filtros, fichas, formularios, tablas, dashboards y perfiles.
- **Tareas en segundo plano**: reintentos para fallos temporales, registro de fallos permanentes para revisión humana, **idempotencia** (no duplicar acciones ya realizadas, ej. una publicación ya enviada).
- **Integraciones aisladas** detrás de interfaces: si un servicio externo cae, el resto funciona y el proceso pendiente se guarda para reintentar.
- **Datos que no se borran**: alertas, cotizaciones y oportunidades se conservan (estados, archivado/soft delete), porque sirven como inteligencia de demanda.
- **Preparado para crecer**: formularios y campos del vehículo configurables; financiamiento, pagos, documentación y entrega pueden llegar después sin rehacer el modelo.
- Pruebas automatizadas en lo crítico (dominio, reglas de estado, autorización), README y `.env.example` actualizados.

## 4. Roles

Cliente, Vendedor, Responsable de inventario, Fotógrafo, Inspector, Gerente, Administrador. Cada uno solo accede a lo que le corresponde. Permisos extensibles.

## 5. Reglas de dominio clave (para diseñar el modelo desde ya)

**Estados del vehículo:** `Borrador → Revisión → Fotografías → Inspección → Aprobado → Publicado` (más `Reservado`, `Vendido`, `Entregado`, `Retirado`). Un vehículo **no puede publicarse** si falta información requerida, fotos aprobadas o inspección completada. Un vehículo nuevo nunca aparece solo en el sitio público.

**Inspección:** estados `Pendiente / En proceso / Completada / Requiere nueva revisión`; registra inspector, fecha, partes revisadas, observaciones y daños. Parte puede mostrarse al cliente en la ficha.

**Fotos (AutoShoot):** cada foto tiene posición, estado (`Pendiente / Tomada / Rechazada / Repetir / Aprobada`), calidad y vehículo. Vista de revisión previa a publicar; las fotos aprobadas son condición para publicar.

**Oportunidades (CRM):** `Nueva → Contacto realizado → Cotización enviada → Prueba de manejo → Negociación → Compra en proceso → Vendida / Cerrada`. Estados configurables. Asignación automática o manual a vendedores; notas, llamadas, próximos pasos.

**Alertas de búsqueda:** `Activa → Coincidencia encontrada → Compra en proceso → Completada`, más `Cancelada` y `Expirada`. Nunca se eliminan.

**Pruebas de manejo:** no pueden existir dos para el mismo vehículo y horario; considerar disponibilidad de sucursal/personal.

**Cotizaciones:** versionadas; una nueva no reemplaza a la anterior.

**Sucursales:** dirección, teléfonos, horarios, responsables, vehículos asociados.

**Notificaciones:** preferencias por usuario y canal (correo, navegador, WhatsApp si está autorizado); registrar generada/enviada/fallida.

## 6. Sitio público (definición para fases 2 y 3)

- **Encabezado:** logotipo + accesos a catálogo, comprar, perfil, búsqueda de coche, favoritos y notificaciones. En móvil, navegación compacta.
- **Pie de página:** logotipo, redes sociales, contacto, navegación y copyright.
- **Home:** vehículos destacados, agregados recientemente, del interés del cliente y cercanos, servicios, datos de contacto.
- **Catálogo:**
  - Barra de búsqueda **grande** arriba del todo, con icono, que **no ocupe todo el ancho** de la pantalla. Búsqueda por palabra clave (marca, modelo, versión), **insensible a mayúsculas** y con coincidencias parciales.
  - **Panel de filtros lateral "sticky"**: se mantiene visible al hacer scroll arriba o abajo (pegado a la pantalla, no estático).
  - Filtros: marca, modelo, variante, año, precio, kilometraje, nuevo/seminuevo, transmisión, combustible, tracción, carrocería y campos configurables.
  - **Precio con slider doble (mín/máx)** que actualiza resultados al instante.
  - Estado vacío claro ("no se encontraron vehículos con esas condiciones") + botón **Limpiar filtros**.
  - Resultados en tarjetas: **2 columnas** en escritorio base, **3 desde 1300 px**, **4 desde 1600 px**. Separación de 24 px entre la barra lateral y los resultados/tarjetas, y ancho de 288 px para la columna de filtros. Móvil: tarjetas compactas y filtros en panel adaptado.
  - Opción de ordenar/destacar por cercanía si el cliente autoriza su ubicación.
  - Si no hay resultados, permitir **guardar la búsqueda como alerta** (fase 8).
- **Ficha de vehículo:** fotos, marca, modelo, versión, año, precio, kilometraje, transmisión, combustible, tracción, equipamiento, inspección visible, servicios, sucursal/ubicación, vehículos similares. Acciones: favorito, comparar, pedir información, cotización, prueba de manejo (cada una crea registro en el CRM cuando aplica, conservando el vehículo de origen).
- **Páginas extra:** 404, contacto, quiénes somos, sucursales, servicios, planes, artículos, comentarios, FAQ. Misma estructura visual en todo el sitio.
- **Diseño visual:** no impongo un diseño cerrado; propón una identidad limpia y profesional coherente con una concesionaria, y consúltame si hay decisiones de marca importantes.

## 7. Plan de fases

0. Fundamentos: solución, arquitectura, DB, auth/roles, auditoría base, errores, logging, Swagger, CI básico
1. Inventario y alta de vehículos (panel interno, estados, formulario por secciones, sucursales)
2. Sitio público y catálogo (búsqueda, filtros, responsive)
3. Ficha del vehículo
4. Cuentas de cliente, perfil, favoritos y comparación
5. AutoShoot (fotos) e inspección, con reglas de publicación
6. CRM y oportunidades, panel de gerente
7. Pruebas de manejo y cotizaciones
8. Notificaciones, alertas de búsqueda y tareas en segundo plano
9. Integraciones (Meta/WhatsApp, correo)
10. Venta, entrega, artículos/blog, geolocalización, dashboards, endurecimiento final

## 8. Forma de trabajar

1. Antes de programar, **resume lo que entendiste**, lista supuestos y señala ambigüedades o riesgos. Haz preguntas solo si algo bloquea de verdad; en lo demás, decide y documenta.
2. Trabaja en pasos pequeños y verificables. Compila, ejecuta pruebas y arranca la app para comprobar que funciona antes de dar algo por terminado.
3. Documenta decisiones de arquitectura en `docs/decisions/` (una nota corta por decisión).
4. Al terminar cada fase entrega: qué se hizo, cómo ejecutarlo, cómo probarlo, qué queda pendiente y qué recomiendas para la siguiente fase.
5. No inventes funcionalidades fuera de la fase actual. No dejes código muerto ni TODOs sin registrar.
6. Convenciones: nombres en inglés en el código, textos de interfaz en español (preparado para i18n), commits pequeños y descriptivos.

---

## 9. TAREA ACTUAL: FASE 0 – Fundamentos

Objetivo: dejar un esqueleto profesional, ejecutable de extremo a extremo, sobre el que se construyan las demás fases.

Entregables:

1. **Estructura de la solución** (monorepo): `/backend` (solución .NET con proyectos Domain, Application, Infrastructure, Api + proyectos de pruebas) y `/frontend` (Next.js + TypeScript estricto + Tailwind), más `/docs`.
2. **Backend:**
   - ASP.NET Core Web API con Swagger/OpenAPI, versionado de API y health checks.
   - PostgreSQL + EF Core con migraciones y configuración por entorno (desarrollo con Docker Compose).
   - Autenticación (JWT con refresh o cookies seguras; justifica la elección) y autorización por roles/políticas con los 7 roles definidos.
   - Middleware global de errores (ProblemDetails) con id de correlación, logging estructurado (Serilog o equivalente) sin datos sensibles.
   - **Auditoría base**: entidad y mecanismo (interceptor de EF) que registre quién, qué, cuándo, valores anteriores/nuevos.
   - Rate limiting, CORS restrictivo, cabeceras de seguridad, gestión de secretos.
   - Esqueleto de servicios de segundo plano (sin lógica de negocio aún).
   - Semilla de datos: roles y un usuario administrador de desarrollo.
3. **Frontend:**
   - Next.js App Router, TypeScript `strict`, ESLint/Prettier, Tailwind configurado con tokens de diseño (colores, tipografía, breakpoints incluyendo 1300 y 1600 px).
   - Layout base público (encabezado, pie) y layout de administración, ambos responsive, y página 404.
   - Cliente de API tipado (generado desde OpenAPI si es viable) y manejo de errores de usuario.
   - Flujo de login funcional contra el backend y protección de rutas por rol.
4. **Infraestructura de desarrollo:** `docker-compose` (PostgreSQL), `.env.example`, scripts para arrancar todo, CI básico (build + pruebas + lint).
5. **Pruebas:** al menos pruebas de autenticación/autorización y de la auditoría.
6. **Documentación:** README con puesta en marcha en minutos, diagrama simple de arquitectura y `docs/decisions/` con las decisiones tomadas.

Criterio de aceptación: clonar el repo, ejecutar un comando, y poder iniciar sesión como administrador en el frontend, ver la página protegida de administración, abrir Swagger y ver un registro de auditoría generado por una acción real.

**Empieza resumiendo tu comprensión y tu plan para la Fase 0 y espera mi confirmación antes de generar el código.**
