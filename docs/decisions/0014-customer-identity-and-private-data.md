# Identidad de clientes y datos privados

Las cuentas de cliente reutilizan `User`, pero se distinguen con `AccountType=Customer`; los JWT incluyen `account_type` y las políticas de personal exigen `Staff` además del rol. El registro nunca recibe un rol desde el cliente.

Las contraseñas nuevas usan PBKDF2-HMAC-SHA256 mediante `PasswordHasher` con 600.000 iteraciones. Los tokens de verificación (24 horas) y restablecimiento (20 minutos) son aleatorios, se almacenan como HMAC-SHA256, invalidan los pendientes al emitir uno nuevo y se consumen con una actualización condicional `UsedAt IS NULL` dentro de una transacción.

`PasswordWorkService` es singleton: genera una sola vez al iniciar el hash ficticio con los mismos parámetros PBKDF2 que los hashes reales. Un login con cuenta inexistente verifica contra ese hash; cuentas sin verificar, desactivadas o anonimizadas verifican antes de devolver la misma respuesta genérica ante una contraseña incorrecta. Una contraseña correcta de una cuenta sin verificar permite inicio de sesión limitado; las operaciones persistentes exigen verificación y devuelven `403` con el código `email_not_verified`.

En Development el correo se escribe en `development-emails/` sin registrar tokens en logs. Fuera de Development, si no hay proveedor configurado, el registro no se habilita. El trabajo de entrega se encola y no forma parte del tiempo de respuesta de registro, recuperación o reenvío. Las respuestas son genéricas.

Registro, login, recuperación y reenvío combinan límite por IP y por cuenta. La clave de cuenta es HMAC-SHA256 del correo normalizado con `Security__AccountHashKey`, distinto de `Jwt__SigningKey` y obligatorio fuera de Development. Los contadores expiran a los diez minutos, se podan a 10.000 claves activas y añaden demoras progresivas acotadas; no se guarda un evento permanente por intento ni un correo en claro.

La auditoría omite completamente entidades de identidad, tokens y eventos de seguridad, y no serializa correo, nombre, teléfono ni hashes. Favoritos se limitan a 100 por cliente, solo aceptan vehículos publicados, eliminan duplicados al fusionar y las comparaciones a cuatro. Los endpoints administrativos requieren el claim `account_type=Staff`.

Al eliminar una cuenta se revocan sesiones, se eliminan favoritos, comparaciones y tokens de un solo uso. Se conservan el identificador técnico y los eventos de seguridad sin PII; correo, nombre, teléfono, consentimiento y hash se anonimizarán. El correo pasa a `deleted+{id}@invalid.local`, valor único que no bloquea un registro futuro con el correo original. Las exportaciones requieren contraseña e incluyen solo el perfil y los IDs propios de favoritos/comparaciones, nunca hashes, tokens, sesiones ni datos de terceros.
