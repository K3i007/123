# API pública del catálogo

Los endpoints `api/v1/public` exponen únicamente vehículos publicados y no eliminados. VIN, placa, auditoría, versiones de concurrencia y actores nunca pertenecen al contrato público. Los campos configurables se devuelven en detalle solo cuando su definición activa tiene `IsPublic`; la exposición es por inclusión, no por exclusión.

Los filtros configurables requieren además `IsFilterable` y usan parámetros `cf.<key>`. El catálogo responde con Problem Details cuando el filtro no coincide con una definición pública filtrable.
