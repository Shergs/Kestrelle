FROM mcr.microsoft.com/dotnet/sdk:10.0 AS libdave-build
RUN apt-get update \
    && apt-get install -y --no-install-recommends git build-essential cmake pkg-config python3 curl zip unzip \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /tmp
RUN git clone --depth 1 --branch v1.1.1/cpp https://github.com/discord/libdave.git
WORKDIR /tmp/libdave/cpp
RUN git submodule update --init --recursive \
    && ./vcpkg/bootstrap-vcpkg.sh \
    && make cclean \
    && make install BUILD_SHARED_LIBS=ON

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Kestrelle/Kestrelle/Kestrelle.Bot/Kestrelle.Bot.csproj
RUN dotnet publish Kestrelle/Kestrelle/Kestrelle.Bot/Kestrelle.Bot.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg libopus0 libopus-dev libsodium23 libsodium-dev \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=libdave-build /tmp/libdave/cpp/build/install/lib/ /usr/local/lib/
RUN cp -a /usr/local/lib/libdave.so* /app/ 2>/dev/null || true \
    && ldconfig
ENV LD_LIBRARY_PATH=/usr/local/lib:/app
ENV DOTNET_EnableDiagnostics=0
ENTRYPOINT ["dotnet", "Kestrelle.Bot.dll"]
