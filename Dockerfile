# syntax=docker/dockerfile:1

# The SDK always runs on the build machine's architecture and cross-compiles for the target,
# so arm64 images are built without emulating the whole .NET build under QEMU
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
# Set by CI for releases (e.g. 4.0.0), otherwise the <Version> from the csproj is used
ARG VERSION=
ARG SOURCE_REVISION=
WORKDIR /src

COPY global.json ./
COPY Asterion/Asterion.csproj Asterion/
RUN dotnet restore Asterion/Asterion.csproj -a $TARGETARCH

COPY Asterion/ Asterion/
RUN dotnet publish Asterion/Asterion.csproj -c Release -a $TARGETARCH --self-contained false --no-restore \
        -o /app -p:SourceRevisionId=$SOURCE_REVISION ${VERSION:+-p:Version=$VERSION}

FROM mcr.microsoft.com/dotnet/runtime:10.0
# SkiaSharp (charts) needs fontconfig and at least one font
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# Config, database and logs all live here, mount it to keep them.
# The entrypoint starts as root only to hand /data to PUID:PGID, the bot itself runs as that user.
ENV ASTERION_DATA_DIR=/data \
    PUID=1654 \
    PGID=1654
COPY --chmod=755 docker-entrypoint.sh /usr/local/bin/
ENTRYPOINT ["docker-entrypoint.sh"]
CMD ["dotnet", "Asterion.dll"]
