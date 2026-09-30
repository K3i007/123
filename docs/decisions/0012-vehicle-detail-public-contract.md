# Ficha pública del vehículo

La ficha solo consulta endpoints públicos y publicados. La fecha publicada se fija en el dominio al transicionar a `Published`; el contrato expone únicamente la fecha (`publishedOn`), nunca historial ni actores. La respuesta 404 es deliberadamente idéntica para un identificador existente no publicable y uno inexistente; por la restricción `:guid`, un identificador con formato inválido tampoco alcanza el controlador y ASP.NET devuelve su 404 de enrutamiento.

Los similares se seleccionan en SQL sobre un conjunto de hasta 120 candidatos publicados, se puntúan por marca/modelo, carrocería, precio, año y kilometraje, y se desempatan por ID. Las imágenes siguen siendo una lista vacía hasta la Fase 5; la interfaz usa un reemplazo estático. La página y sus metadatos comparten una lectura memoizada, revalidada como máximo cada 60 segundos. JSON-LD escapa `<` para impedir el cierre de `script`.
