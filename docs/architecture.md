# Arquitectura

```text
Browser -> Next.js route handlers -> ASP.NET Core API -> Application -> Domain
                                      |                    |
                                      +-> PostgreSQL <----- Infrastructure
```

El navegador recibe únicamente un JWT de acceso de corta vida mantenido en memoria. Los route handlers de Next.js guardan y rotan el refresh token en una cookie `HttpOnly`; la API almacena solo su hash.

