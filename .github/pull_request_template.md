## ¿Qué cambia este PR?

<!-- Describa en 2-3 líneas qué se hizo y por qué. -->

## Historia de usuario / tarea

<!-- Enlace a Azure Boards con el formato AB#<id>. Ejemplo: AB#12 -->
AB#

## Módulo (rama de origen)

- [ ] feature/registro
- [ ] feature/perfil-avatar
- [ ] feature/config-partida
- [ ] feature/logica-juego
- [ ] feature/arena-unity
- [ ] feature/tiempo-real
- [ ] feature/hardware
- [ ] feature/devops
- [ ] feature/docs

## Tipo de cambio

- [ ] Nueva funcionalidad
- [ ] Corrección de error
- [ ] Refactorización (sin cambio de comportamiento)
- [ ] Configuración / DevOps
- [ ] Documentación

## Lista de verificación

- [ ] El proyecto compila localmente (`dotnet build Servidor/ControlDelReactor.slnx`)
- [ ] Las pruebas pasan (`dotnet test`) y agregué pruebas para lo nuevo
- [ ] El pipeline de Azure Pipelines está en verde
- [ ] Respeta las capas (Dominio no depende de Infraestructura ni de API) y los principios SOLID
- [ ] No hay secretos, contraseñas ni archivos `.env` en el commit
- [ ] No se subieron `bin/`, `obj/` ni datos de ejecución (`Servidor/Data/*.json`)
- [ ] Los mensajes de commit siguen Conventional Commits (`feat:`, `fix:`, `chore:`, ...)

## Evidencia

<!-- Capturas, salida de pruebas o pasos para probar manualmente. -->
