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

The version calculator supplies a release version for the enabled artifacts:
`{SchemaVersion}-{releaseHashPrefix}`. It hashes each completed artifact with
SHA-256, sorts by normalized file extension, and hashes newline-delimited records
of `extension:UPPERCASE_SHA256`. The first 16 lowercase hex characters of that
combined hash form the suffix. File paths and input order do not affect the version.
With metadata publishing enabled, each artifact is also versioned independently
with the same calculation applied to that file. Without metadata publishing,
artifact filenames use the shared release version.

Changing SQLite (including Flyway history), PMTiles, the enabled artifact set, or
SchemaVersion changes the release version. Byte-identical artifact sets retain
the same version. Versions identify concrete files, not semantic POI equality.
PBF download checkpoints remain independent. See
[Artifact-based release versions](../decisions/0009-artifact-based-release-versions.md).

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
have been published. With metadata publishing enabled, the current manifest stays
on the previous complete release. See [region metadata publishing](#region-metadata-publishing).

When publication fails, the schedule records processing as pending. The next
normal run retries the rebuild, publication, and verification, even when the
canonical files already exist. A region without a valid schedule checkpoint also
goes through full processing. `SkipIfIdentical` skips uploads for matching artifacts.

Use `--Renderer:OverwriteDatabase=true` to force an immediate rebuild and
publication for an otherwise up-to-date region. If the artifacts were verified and
only the manifest write failed, a checkpoint lets the next run reuse and verify
the existing outputs when the source and artifacts still match.

## Region metadata publishing

Set `MetadataPublisher:Target=Local` and `MetadataPublisher:DestinationDir`
to publish the catalog snapshot and v1 manifests. The metadata directory can be
the same as `Publisher:DestinationDir` or a separate directory.

```text
--MetadataPublisher:Target=Local
--MetadataPublisher:DestinationDir=data/dev/metadata
```

For Azure metadata storage, set `MetadataPublisher:Target=AzureBlob`,
`MetadataPublisher:AccountName` (or `MetadataPublisher:BlobEndpoint`), and
`MetadataPublisher:ContainerName`. Optionally set `MetadataPublisher:Prefix`
to a slash-separated prefix without a trailing slash. The renderer uses its Azure
identity for this container. The artifact publisher remains independently
configured under `Publisher` and `AzureBlobPublisher`. Omitting the metadata
target leaves artifact-only publishing enabled.

The renderer publishes the configured catalog before processing regions, even
when `Renderer:OnlyRegionId` selects a single region. Previous catalog entries
are retained for existing releases. Entries without a PBF URL are included in
the snapshot and skipped for rendering. The catalog exports display fields and public
PBF URLs. Runtime polygon paths are omitted. Source URLs containing credentials,
queries, or fragments are rejected. After verification, the renderer atomically updates
`<regionId>/manifest.json`. Repeating the same release preserves its existing
manifest and publication timestamp. Concurrent manifest changes are detected by
checking the storage revision before replacement.

When metadata is enabled, artifact keys are immutable, and Azure verification
streams blob contents back to check their size and SHA-256. For a region with
an existing schedule, use `--Renderer:OverwriteDatabase=true` to publish the
first manifest immediately. A normal run can also backfill it when the region is
next due.

The renderer marks the region schedule successful after publishing its manifest.
Failed attempts retain the local outputs and the previous published release.
The publisher keeps versioned artifacts available for existing downloads and
cached manifests. Retention is tracked separately in #190.
