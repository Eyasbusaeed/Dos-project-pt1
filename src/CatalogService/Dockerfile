FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["CatalogService.csproj", "./"]
RUN dotnet restore "CatalogService.csproj"
COPY . .
RUN dotnet publish "CatalogService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 4000
ENTRYPOINT ["dotnet", "CatalogService.dll"]