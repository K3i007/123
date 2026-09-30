# Estrategia de búsqueda pública

La búsqueda resuelve primero IDs de marca, modelo y variante y después filtra vehículos publicados. PostgreSQL usa `ILIKE` con escape explícito para `%`, `_` y barra inversa. Un índice parcial del catálogo cubre el acceso publicado y `SearchStrategyExplainTests` verifica de manera reproducible su presencia y un plan de índice cuando `ConnectionStrings__Default` está configurada.
