# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for layer caching
COPY Panaderia.Models/Panaderia.Models.csproj Panaderia.Models/
COPY Panaderia.Services/Panaderia.Services.csproj Panaderia.Services/
COPY Panaderia.MVC/Panaderia.MVC.csproj Panaderia.MVC/

# Restore dependencies for the MVC project (pulls in referenced projects)
RUN dotnet restore Panaderia.MVC/Panaderia.MVC.csproj

# Copy the rest of the source
COPY . .

# Publish the MVC project in Release mode
RUN dotnet publish Panaderia.MVC/Panaderia.MVC.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Railway injects PORT at runtime; sh expands ${PORT:-8080} at container start.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet Panaderia.MVC.dll"]
