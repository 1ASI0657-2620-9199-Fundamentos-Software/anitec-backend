# Build context: this folder (the project has no references to the other services).
#   docker build -t anitec-gateway:local Anitec.Gateway
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Anitec.Gateway.csproj .
RUN dotnet restore Anitec.Gateway.csproj

COPY . .
RUN dotnet publish Anitec.Gateway.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://0.0.0.0:8080

ENTRYPOINT ["dotnet", "Anitec.Gateway.dll"]
