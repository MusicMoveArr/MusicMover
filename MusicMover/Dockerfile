FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base

# Install ffmpeg and fpcalc
USER root
RUN apt-get update
RUN apt-get install -y ffmpeg libchromaprint-tools libchromaprint1

USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MusicMover/MusicMover.csproj", "MusicMover/"]
RUN dotnet restore "MusicMover/MusicMover.csproj"
COPY . .
WORKDIR "/src/MusicMover"
RUN dotnet build "./MusicMover.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MusicMover.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MusicMover.dll"]
