# 0004: Semilla de desarrollo

Los siete roles se aseguran en cada entorno porque son la base estable de las políticas. El administrador se siembra únicamente cuando `IHostEnvironment.IsDevelopment()` es verdadero. Su correo y contraseña proceden de `.env`, que está ignorado por Git. Producción debe aprovisionar usuarios mediante un proceso administrativo posterior.
