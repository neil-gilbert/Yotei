FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /workspace

COPY backend/src/Api/Api.csproj backend/src/Api/Api.csproj
RUN dotnet restore backend/src/Api/Api.csproj

COPY backend/src/Api/ backend/src/Api/
RUN dotnet publish backend/src/Api/Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]
