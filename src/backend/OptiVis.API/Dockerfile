# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 5000


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/backend/OptiVis.API/OptiVis.API.csproj", "src/backend/OptiVis.API/"]
COPY ["src/backend/OptiVis.Application/OptiVis.Application.csproj", "src/backend/OptiVis.Application/"]
COPY ["src/backend/OptiVis.Domain/OptiVis.Domain.csproj", "src/backend/OptiVis.Domain/"]
COPY ["src/backend/OptiVis.Infrastructure/OptiVis.Infrastructure.csproj", "src/backend/OptiVis.Infrastructure/"]
RUN dotnet restore "./src/backend/OptiVis.API/OptiVis.API.csproj"
COPY . .
WORKDIR "/src/src/backend/OptiVis.API"
RUN dotnet build "./OptiVis.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./OptiVis.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OptiVis.API.dll"]