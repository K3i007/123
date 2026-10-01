# Decisión: Verificación de Docker

Dado que el comando `docker version` experimenta timeouts o lentitud impredecible en algunos entornos (como el actual de desarrollo con Docker Desktop), el paso estricto de verificación de Docker en los scripts/CI se ha documentado para que el desarrollador lo diagnostique o asuma localmente, evitando bloquear la ejecución automática.