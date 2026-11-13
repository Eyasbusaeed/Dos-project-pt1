FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["FrontendService.csproj", "./"]
RUN dotnet restore "FrontendService.csproj"
COPY . .
RUN dotnet publish "FrontendService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENTRYPOINT ["dotnet", "FrontendService.dll"]
