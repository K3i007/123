# Matriz de permisos de inventario

| Acción | Responsable de inventario | Gerente | Administrador | Vendedor | Cliente |
| --- | --- | --- | --- | --- | --- |
| Ver inventario | Sí | Sí | Sí | Sí | No |
| Ver VIN y placa | Sí | Sí | Sí | No | No |
| Crear/editar borradores | Sí | Sí | Sí | No | No |
| Draft → InReview → Photography | Sí | Sí | Sí | No | No |
| Photography → Inspection (override manual) | No | Sí, motivo obligatorio | Sí, motivo obligatorio | No | No |
| Inspection → Approved (override manual) | No | Sí, motivo obligatorio | Sí, motivo obligatorio | No | No |
| Approved → Published | No | Sí | Sí | No | No |

El backend aplica las restricciones; el panel solo refleja las acciones que el rol puede intentar.

