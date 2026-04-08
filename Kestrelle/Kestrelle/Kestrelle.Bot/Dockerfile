FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN ls -la Kestrelle/Kestrelle/ && \
    ls -la Kestrelle/Kestrelle/Kestrelle.Shared/ || true
RUN dotnet restore Kestrelle/Kestrelle/Kestrelle.Bot/Kestrelle.Bot.csproj
RUN dotnet publish Kestrelle/Kestrelle/Kestrelle.Bot/Kestrelle.Bot.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg libopus0 libsodium23 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_EnableDiagnostics=0
ENTRYPOINT ["dotnet", "Kestrelle.Bot.dll"]
