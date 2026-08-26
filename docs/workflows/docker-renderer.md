# Docker Renderer Image

The renderer can be packaged as a Docker image that contains the published .NET runtime
artifact plus the external tools required by production rendering:

- .NET runtime
- Java 21 runtime
- Flyway CLI
- pinned Planetiler JAR
- `osmium-tool` for polygon extraction when a region provides a `.poly` file

The image stores application files below `/opt/poineer-render/app` and bundled tools below
`/opt/poineer-render/tools`. Runtime data, rendered outputs, published artifacts, and logs
should be mounted from the host instead of baked into the image.

## Build

```bash
docker build \
  --build-arg PLANETILER_VERSION=0.10.2 \
  -t poineer-render:local .
```

For release builds, also pass the expected Planetiler checksum:

```bash
docker build \
  --build-arg PLANETILER_VERSION=0.10.2 \
  --build-arg PLANETILER_SHA256=<sha256> \
  -t poineer-render:<version> .
```

The image runs as a non-root `poineer` user. Its default UID/GID is `10001`, and both can be
overridden at build time:

```bash
docker build \
  --build-arg APP_UID=10001 \
  --build-arg APP_GID=10001 \
  -t poineer-render:local .
```

## Verify

Run a dry start against the production configuration:

```bash
docker run --rm poineer-render:local --Renderer:DryRun=true
```

Check the bundled external tools:

```bash
docker run --rm --entrypoint sh poineer-render:local -c "id && java -version && flyway -v && osmium --version && test -s /opt/poineer-render/tools/planetiler/planetiler.jar"
```

## Run On The VPS

Mount the production data and log directories:

```bash
docker run --rm \
  --name poineer-render \
  -v /opt/poineer-render/data:/opt/poineer-render/data \
  -v /opt/poineer-render/logs:/opt/poineer-render/logs \
  poineer-render:local
```

When using bind mounts on Linux, make sure the mounted directories are writable by the
container user:

```bash
sudo mkdir -p /opt/poineer-render/data /opt/poineer-render/logs
sudo chown -R 10001:10001 /opt/poineer-render/data /opt/poineer-render/logs
```

Configuration is still loaded from `appsettings.Production.json`, environment variables with
the `POINEER_RENDER__` prefix, and command-line arguments. For example:

```bash
docker run --rm \
  -v /opt/poineer-render/data:/opt/poineer-render/data \
  -v /opt/poineer-render/logs:/opt/poineer-render/logs \
  poineer-render:local \
  --Renderer:OnlyRegionId=berlin \
  --VectorTiles:Enabled=true
```

Vector tile generation remains disabled by default in production configuration until a release
explicitly enables it or passes `--VectorTiles:Enabled=true`.
