# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/BlazorApp/ .
RUN dotnet publish BoardGameFanatics/BoardGameFanatics.csproj \
    -c Release \
    -o /app/publish \
    --no-self-contained

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "BoardGameFanatics.dll"]
