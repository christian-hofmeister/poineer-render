# Publish SQLite and PMTiles to the Filesystem

With `Publisher:Target=Local`, a successful render publishes the SQLite dataset
and, when `VectorTiles:Enabled=true`, the generated PMTiles dataset. Tiles remain
disabled by default. Java and Planetiler must be available to generate them; see
[Docker Renderer Image](docker-renderer.md).

## Names and Metadata

Both artifacts use the configured destination root and hierarchical region ID:

```text
{Publisher:DestinationDir}/{regionId}/{regionLeaf}.{version}.sqlite
{Publisher:DestinationDir}/{regionId}/{regionLeaf}.{version}.pmtiles
```

For example:

```text
geofabrik/europe/germany/berlin/berlin.2-252c39aba2cb1705.sqlite
geofabrik/europe/germany/berlin/berlin.2-252c39aba2cb1705.pmtiles
```

Canonical renderer outputs remain `poi.sqlite` and `map.pmtiles`. Source metadata
records that canonical filename, region ID, version, artifact type (`Sqlite` or
`Pmtiles`), byte size, file last-write timestamp as `CreatedUtc`, and full lowercase
SHA-256 checksum. Metadata is storage-independent. Logs use lowercase type names
`sqlite` and `pmtiles`, matching the [manifest contract](../../contracts/region-manifest/README.md).
The publisher reports the versioned destination path separately.

The existing version calculator supplies the same version for both artifacts,
based on the cut PBF and `Publisher:SchemaVersion`. A tile profile or generator
change does not independently change that version: deliberately bump the configured
schema version and force rendering when artifact semantics change. Independent
artifact/release versioning and manifest publication remain separate work in #202.

## Verification and Size Assessment

After generation and promotion, the renderer publishes and verifies SQLite first,
then PMTiles when enabled. Verification compares type, byte size, and SHA-256 of
the destination with source metadata. This is byte integrity verification, not
semantic validation of the PMTiles archive or map content.

Each source artifact has a metadata log entry with its individual size. Only after
all required artifacts pass verification does the renderer log
`Release artifacts verified` with `artifactCount` and `totalSizeBytes`. The total
is the sum of artifact file sizes, not allocated disk space, working files, or
historical versions. Use these logs to record reference-region storage measurements.
SQLite-only runs report one artifact; a stale `map.pmtiles` in the output directory
is not published when vector tiles are disabled.

## Existing Files and Failures

- `SkipIfIdentical` skips an existing byte-identical file; differing bytes fail.
- `Skip` skips an existing file, but verification still rejects differing bytes.
- `Fail` rejects an existing destination.
- `Overwrite` replaces an existing destination through a staging file.

Each artifact copy is staged separately. A PMTiles copy or verification failure
fails the run and suppresses the successful release summary. SQLite may already
have been published: this is not an atomic transaction across artifacts. No release
manifest or catalog is written here; #202 owns advertising completed releases.
No Azure or S3 adapter changes are included in this filesystem workflow.

The existing render shortcut still skips a region when canonical outputs already
exist and no overwrite/input change requires rendering. Merely enabling tiles
triggers rendering when `map.pmtiles` is missing, but does not republish outputs
when both canonical files already exist. For an initial publication of existing
outputs or a retry after publishing failed, force a run with
`--Renderer:OverwriteDatabase=true`. It rebuilds the outputs before publishing;
`SkipIfIdentical` will only skip destinations whose resulting bytes are identical.
Automatic publication-only recovery remains separate work.
