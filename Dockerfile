# Builds both UIs and API into ONE container for PRIVATE LOCAL PREVIEW ONLY.
# ASPNETCORE_ENVIRONMENT is Production by default. Production startup requires
# validated Entra configuration and a provisioned Azure SQL schema and membership.
FROM node:22-alpine AS webbuild
WORKDIR /src/web
COPY web/package*.json ./
RUN npm install --no-audit --no-fund
COPY web ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS apibuild
WORKDIR /src
COPY src/TradeFlow.Api/TradeFlow.Api.csproj src/TradeFlow.Api/
RUN dotnet restore src/TradeFlow.Api/TradeFlow.Api.csproj
COPY src/TradeFlow.Api/ src/TradeFlow.Api/
RUN dotnet publish src/TradeFlow.Api/TradeFlow.Api.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=apibuild /app/publish ./
COPY --from=webbuild /src/web/dist ./wwwroot/
RUN mkdir /data && chown -R app:app /data /app
USER app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "TradeFlow.Api.dll"]
