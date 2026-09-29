# 0005: Cola hospedada inicial

Se usa una cola acotada `Channel` con `BackgroundService` por ahora. No introduce infraestructura adicional antes de que existan tareas de negocio; registra los fallos permanentes y deja el contrato listo para una implementación persistente con reintentos/idempotencia en la fase de notificaciones.

