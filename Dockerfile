# Usar la imagen base de .NET 8.0
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar los archivos del proyecto y restaurar dependencias
COPY ["Licitaciones.csproj", "./"]
RUN dotnet restore

# Copiar el resto del código
COPY . .

# Publicar la aplicación
RUN dotnet publish -c Release -o /app/publish

# Construir la imagen final
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Exponer el puerto 8080 (el puerto que Render usa por defecto)
EXPOSE 8080

# Variable de entorno para el puerto de ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080

# Iniciar la aplicación
ENTRYPOINT ["dotnet", "Licitaciones.dll"] 