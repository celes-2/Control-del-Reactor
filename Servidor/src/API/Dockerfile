# Contexto de construcción: la carpeta Servidor/ (ver docker-compose.yml).
# Etapa 1: compilar y publicar con el SDK de .NET 10.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ src/
# Publicar la carpeta del proyecto API (restaura y compila los otros 4 proyectos referenciados).
RUN dotnet publish src/API -c Release -o /app/publish

# Etapa 2: imagen final, solo con el runtime (más liviana).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Datos iniciales (avatares y escenarios) y carpeta de datos del volumen.
COPY Data/seed/ /app/seed/
RUN mkdir -p /app/data && chown -R app:app /app/data

# Arranque: busca el .runtimeconfig.json del proyecto API y ejecuta su .dll,
# así no importa cómo se llame el ensamblado.
RUN printf '#!/bin/sh\nexec dotnet "$(ls /app/*.runtimeconfig.json | head -n 1 | sed "s/\\.runtimeconfig\\.json$/.dll/")"\n' > /app/start.sh \
    && chmod +x /app/start.sh

ENV ASPNETCORE_URLS=http://+:8080 \
    DataPath=/app/data \
    SeedPath=/app/seed

# La imagen de .NET trae el usuario "app"; no se corre como root.
USER app
EXPOSE 8080
ENTRYPOINT ["/app/start.sh"]
