# 0002: Sesión con access token en memoria y refresh token en cookie

El access token JWT dura diez minutos y solo reside en memoria React. Next.js recibe el refresh token desde la API en sus route handlers, lo guarda como cookie `HttpOnly`, `SameSite=Strict` y, fuera de desarrollo, `Secure`; el cliente nunca puede leerlo. La API persiste un HMAC del token, rota el token en cada refresh y revoca toda la familia al detectar uno reutilizado. En producción la cookie siempre es `Secure`; `AUTH_COOKIE_SECURE=true` permite forzarla en otros entornos.
