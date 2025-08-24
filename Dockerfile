# build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# 1) Копіюємо лише csproj'ки, щоб закешувати restore
COPY ["MissilePredictor.API/MissilePredictor.API.csproj", "MissilePredictor.API/"]
COPY ["MissilePredictor.AI/MissilePredictor.AI.csproj", "MissilePredictor.AI/"]

# 2) Restore головного (API) — підтягне залежні проєкти через ProjectReference
RUN dotnet restore "MissilePredictor.API/MissilePredictor.API.csproj"

# 3) Копіюємо увесь код і публікуємо
COPY . .
WORKDIR /src/MissilePredictor.API
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MissilePredictor.API.dll"]
