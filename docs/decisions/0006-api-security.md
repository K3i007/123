# 0006: Protección de la superficie HTTP

La API versiona rutas bajo `/api/v1`, restringe CORS al origen del frontend configurado, aplica límite de cinco intentos de login por minuto y añade cabeceras básicas contra contenido MIME ambiguo, framing y referencias. Los errores se devuelven como `ProblemDetails` con correlación, mientras que los detalles técnicos quedan fuera de las respuestas.
