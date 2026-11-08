FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["OrderService.csproj", "./"]
RUN dotnet restore "OrderService.csproj"
COPY . .
RUN dotnet publish "OrderService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 3000
ENTRYPOINT ["dotnet", "OrderService.dll"]