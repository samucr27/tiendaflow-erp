# TiendaFlow ERP: imagen de contenedor en dos etapas.
# Etapa 1 (build): compila y publica con el SDK de .NET 9.
# Etapa 2 (final): copia solo el resultado publicado sobre el runtime de ASP.NET Core,
# que es mucho más liviano y no incluye el compilador.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Primero los proyectos, para que la restauración de paquetes quede en caché
# mientras no cambien las dependencias.
COPY src/TiendaFlow.Domain/TiendaFlow.Domain.csproj src/TiendaFlow.Domain/
COPY src/TiendaFlow.Application/TiendaFlow.Application.csproj src/TiendaFlow.Application/
COPY src/TiendaFlow.Infrastructure/TiendaFlow.Infrastructure.csproj src/TiendaFlow.Infrastructure/
COPY src/TiendaFlow.Web/TiendaFlow.Web.csproj src/TiendaFlow.Web/
RUN dotnet restore src/TiendaFlow.Web/TiendaFlow.Web.csproj

COPY src/ src/
RUN dotnet publish src/TiendaFlow.Web/TiendaFlow.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# La imagen corre con el usuario sin privilegios "app". Las claves de Data
# Protection (cookies de sesión) se guardan en una carpeta suya.
RUN mkdir -p /home/app/keys && chown app:app /home/app/keys

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DataProtection__KeysPath=/home/app/keys

COPY --from=build /app/publish .

USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "TiendaFlow.Web.dll"]
