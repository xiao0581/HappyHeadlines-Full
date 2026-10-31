
# Stage 1: Build NewsletterService
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY NewsletterService.csproj .

RUN dotnet restore

COPY . .

RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false


# Stage 2: Run NewsletterService
FROM mcr.microsoft.com/dotnet/runtime:8.0

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "NewsletterService.dll"]
