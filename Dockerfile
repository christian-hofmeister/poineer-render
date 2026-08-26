# syntax=docker/dockerfile:1

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.201
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime:10.0

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src

COPY POIneerRender.sln ./
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/POIneer.Render/POIneer.Render.csproj src/POIneer.Render/
COPY src/POIneer.Render/packages.lock.json src/POIneer.Render/
RUN dotnet restore src/POIneer.Render/POIneer.Render.csproj --nologo

COPY migrations migrations
COPY src src
RUN dotnet publish src/POIneer.Render/POIneer.Render.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    --nologo

FROM ${DOTNET_RUNTIME_IMAGE} AS runtime

ARG APP_UID=10001
ARG APP_GID=10001
ARG FLYWAY_VERSION=11.8.2
ARG FLYWAY_SHA1=
ARG PLANETILER_VERSION=0.10.2
ARG PLANETILER_SHA256=

ENV DOTNET_ENVIRONMENT=Production \
    PATH="/opt/flyway/current:${PATH}"

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        openjdk-21-jre-headless \
        osmium-tool \
        tar \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p \
        /opt/flyway \
        /opt/poineer-render/app \
        /opt/poineer-render/data \
        /opt/poineer-render/logs \
        /opt/poineer-render/tools/planetiler

RUN groupadd --gid "${APP_GID}" poineer \
    && useradd \
        --uid "${APP_UID}" \
        --gid "${APP_GID}" \
        --home-dir /opt/poineer-render \
        --shell /usr/sbin/nologin \
        --no-create-home \
        poineer

RUN curl -fsSL \
        "https://repo1.maven.org/maven2/org/flywaydb/flyway-commandline/${FLYWAY_VERSION}/flyway-commandline-${FLYWAY_VERSION}-linux-x64.tar.gz" \
        -o /tmp/flyway.tar.gz \
    && if [ -n "${FLYWAY_SHA1}" ]; then \
        echo "${FLYWAY_SHA1}  /tmp/flyway.tar.gz" | sha1sum -c -; \
    else \
        curl -fsSL \
            "https://repo1.maven.org/maven2/org/flywaydb/flyway-commandline/${FLYWAY_VERSION}/flyway-commandline-${FLYWAY_VERSION}-linux-x64.tar.gz.sha1" \
            -o /tmp/flyway.tar.gz.sha1; \
        echo "$(cut -d ' ' -f 1 /tmp/flyway.tar.gz.sha1)  /tmp/flyway.tar.gz" | sha1sum -c -; \
        rm /tmp/flyway.tar.gz.sha1; \
    fi \
    && tar -xzf /tmp/flyway.tar.gz -C /opt/flyway \
    && ln -s "/opt/flyway/flyway-${FLYWAY_VERSION}" /opt/flyway/current \
    && rm /tmp/flyway.tar.gz

RUN curl -fsSL \
        "https://github.com/onthegomap/planetiler/releases/download/v${PLANETILER_VERSION}/planetiler.jar" \
        -o /opt/poineer-render/tools/planetiler/planetiler.jar \
    && if [ -n "${PLANETILER_SHA256}" ]; then \
        echo "${PLANETILER_SHA256}  /opt/poineer-render/tools/planetiler/planetiler.jar" | sha256sum -c -; \
    else \
        curl -fsSL \
            "https://github.com/onthegomap/planetiler/releases/download/v${PLANETILER_VERSION}/planetiler.jar.sha256" \
            -o /tmp/planetiler.jar.sha256; \
        echo "$(cut -d ' ' -f 1 /tmp/planetiler.jar.sha256)  /opt/poineer-render/tools/planetiler/planetiler.jar" | sha256sum -c -; \
        rm /tmp/planetiler.jar.sha256; \
    fi

COPY --from=build /app/publish/ /opt/poineer-render/app/

RUN chown -R poineer:poineer /opt/poineer-render

WORKDIR /opt/poineer-render/app
VOLUME ["/opt/poineer-render/data", "/opt/poineer-render/logs"]

USER poineer

ENTRYPOINT ["dotnet", "/opt/poineer-render/app/POIneer.Render.dll"]
