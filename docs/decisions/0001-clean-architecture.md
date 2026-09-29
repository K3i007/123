# 0001: Arquitectura por capas

Se separan `Domain`, `Application`, `Infrastructure` y `Api`. El dominio no depende de infraestructura; los controladores únicamente traducen HTTP a contratos de aplicación. Esta separación permite añadir módulos de inventario, CRM y publicaciones sin concentrar la lógica en la API.

