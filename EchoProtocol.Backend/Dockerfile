FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EchoProtocol.sln ./
COPY src/EchoProtocol.Api/EchoProtocol.Api.csproj src/EchoProtocol.Api/
RUN dotnet restore src/EchoProtocol.Api/EchoProtocol.Api.csproj

COPY . ./
RUN dotnet publish src/EchoProtocol.Api/EchoProtocol.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

EXPOSE 5042
ENTRYPOINT ["dotnet", "EchoProtocol.Api.dll"]
