FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0.102 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Commerce.sln", "./"]
COPY ["Commerce.Api/Commerce.Api.csproj", "Commerce.Api/"]
COPY ["Commerce.Application/Commerce.Application.csproj", "Commerce.Application/"]
COPY ["Commerce.Contracts/Commerce.Contracts.csproj", "Commerce.Contracts/"]
RUN dotnet restore "Commerce.Api/Commerce.Api.csproj" \
    /p:EnableDefaultEmbeddedResourceItems=false

COPY . .
WORKDIR "/src/Commerce.Api"
RUN dotnet publish "Commerce.Api.csproj" \
    -c "$BUILD_CONFIGURATION" \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:EnableDefaultEmbeddedResourceItems=false

FROM runtime AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Commerce.Api.dll"]
