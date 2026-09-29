# 0003: Auditoría mediante interceptor EF Core

Un interceptor de `SaveChanges` registra entidad, actor, fecha, acción, valores anteriores/nuevos y correlación. La lista de exclusión contiene hashes, contraseñas y tokens; estos valores jamás llegan a `AuditLogs` ni a logs de aplicación.

Como comprobación funcional de Fase 0, un administrador puede revocar sus sesiones de renovación en `POST /api/v1/admin/sessions/revoke`. La actualización se audita, pero `TokenHash` queda excluido de los valores registrados.
