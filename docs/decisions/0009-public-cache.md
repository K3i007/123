# Caché del catálogo público

Las respuestas públicas incluyen `Cache-Control` de corta duración (45 a 120 segundos según el recurso). El proxy de Next no almacena las llamadas de usuario y el sitemap se revalida cada cinco minutos. Esto evita que precios y disponibilidad publicados queden obsoletos durante periodos largos sin introducir invalidación distribuida en esta fase.
