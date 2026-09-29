# 0007: Inventario extensible y publicación por requisitos

Los valores configurables se almacenan en `Vehicles.CustomFields` como `jsonb`, con índice GIN. Las definiciones se validan por tipo en aplicación y se desactivan cuando ya contienen datos, en vez de borrarse. La publicación consulta reglas independientes: hoy datos obligatorios y precio válido; fotos e inspección se agregarán como reglas posteriores sin reescribir la máquina de estados.

