# -------- BUILD STAGE --------
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy project files
COPY Relevantz.ExitManagement.Api/Relevantz.ExitManagement.Api.csproj Relevantz.ExitManagement.Api/
COPY Relevantz.ExitManagement.Core/Relevantz.ExitManagement.Core.csproj Relevantz.ExitManagement.Core/
COPY Relevantz.ExitManagement.Data/Relevantz.ExitManagement.Data.csproj Relevantz.ExitManagement.Data/
COPY Relevantz.ExitManagement.Common/Relevantz.ExitManagement.Common.csproj Relevantz.ExitManagement.Common/

# Restore only API project
RUN dotnet restore Relevantz.ExitManagement.Api/Relevantz.ExitManagement.Api.csproj

# Copy everything
COPY . .

WORKDIR /src/Relevantz.ExitManagement.Api

# Publish
RUN dotnet publish -c Release -o /app/publish

# -------- RUNTIME STAGE --------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 5100

ENTRYPOINT ["dotnet", "Relevantz.ExitManagement.Api.dll"]