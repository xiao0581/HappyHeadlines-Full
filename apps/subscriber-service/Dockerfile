
# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY SubscriberService.csproj .

RUN dotnet restore

COPY . .

RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false


# Stage 2: Run the application
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "SubscriberService.dll"]
