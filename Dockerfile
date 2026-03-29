FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MissilePredictor.API/MissilePredictor.API.csproj", "MissilePredictor.API/"]
COPY ["MissilePredictor.AI/MissilePredictor.AI.csproj", "MissilePredictor.AI/"]
RUN dotnet restore "MissilePredictor.API/MissilePredictor.API.csproj"
COPY . .
WORKDIR /src/MissilePredictor.API
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/AppData
VOLUME /app/AppData
EXPOSE 8080
ENTRYPOINT ["dotnet", "MissilePredictor.API.dll"]