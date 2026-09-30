# Límite público y proxy inverso

Los endpoints públicos usan una ventana fija de un minuto particionada por `RemoteIpAddress`. Al rechazar una solicitud devuelven `429` como ProblemDetails y `Retry-After: 60`, para que clientes y pruebas puedan esperar explícitamente en vez de reintentar en bucle.

La API procesa `X-Forwarded-For` y `X-Forwarded-Proto` antes de HTTPS, pero vacía las listas predeterminadas y solo confía en las IP declaradas en `ForwardedHeaders__KnownProxies__0`, `ForwardedHeaders__KnownProxies__1`, etc. En despliegue se deben declarar las IP del proxy inverso; no se deben habilitar redes amplias ni proxies no verificados, porque permitiría que un cliente externo suplante su IP o protocolo.
