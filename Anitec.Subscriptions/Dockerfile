# Build context: this folder (the project has no references to the core).
#   docker build -t anitec-subscriptions:local Anitec.Subscriptions
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Anitec.Subscriptions.csproj .
RUN dotnet restore Anitec.Subscriptions.csproj

COPY . .
RUN dotnet publish Anitec.Subscriptions.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://0.0.0.0:8080

ENTRYPOINT ["dotnet", "Anitec.Subscriptions.dll"]
