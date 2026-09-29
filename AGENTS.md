# Instrucciones para Codex

## Fuente principal

Antes de realizar cualquier cambio, lee:

- `MASTER_PROMPT.md`
- documentación relevante dentro de `docs/`

`MASTER_PROMPT.md` es la fuente principal de requisitos del proyecto.

## Forma de trabajo

- Respeta estrictamente la fase indicada en `MASTER_PROMPT.md`.
- No implementes funcionalidades de fases posteriores.
- Antes de programar, resume qué entendiste y presenta el plan.
- Espera confirmación del usuario cuando el Master Prompt lo solicite.
- Trabaja en cambios pequeños y verificables.
- Ejecuta build, tests y lint después de realizar cambios relevantes.
- No inventes requisitos.
- Si existe una ambigüedad que realmente bloquea el trabajo, pregunta.
- Si no bloquea, toma una decisión razonable y documenta la decisión.

## Arquitectura

- Mantén separación entre Domain, Application, Infrastructure y API.
- No coloques lógica de negocio en controladores.
- Mantén frontend y backend separados.
- Usa nombres en inglés en el código.
- La interfaz de usuario debe estar en español y preparada para i18n.

## Calidad

- TypeScript debe mantenerse en `strict`.
- C# debe utilizar nullable reference types.
- No dejes código muerto.
- No dejes TODOs sin registrar.
- No des por terminada una tarea sin verificarla.

## Seguridad

- Nunca introduzcas secretos directamente en el código.
- Nunca registres contraseñas, tokens o claves.
- Valida las entradas en servidor.
- Respeta las reglas de autenticación, autorización, auditoría y manejo de errores definidas en `MASTER_PROMPT.md`.