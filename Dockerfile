# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/BoardGameFanatics/BoardGameFanatics.csproj src/BoardGameFanatics/
RUN dotnet restore src/BoardGameFanatics/BoardGameFanatics.csproj

COPY src/BoardGameFanatics/ src/BoardGameFanatics/
WORKDIR /src/src/BoardGameFanatics
RUN dotnet publish BoardGameFanatics.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "BoardGameFanatics.dll"]
