# ADR 0003: Dataset Artifact Metadata

## Status

Accepted

## Context

Issue #130 asks for basic technical metadata about each generated regional dataset -
region ID, dataset version, file name, file size, a creation timestamp, and a SHA-256
checksum - as "a stable foundation for publishing datasets to different storage backends
and for allowing clients to determine which dataset version is available."

This metadata is deliberately generic: the acceptance criteria explicitly call for
"metadata generation independent from the publishing implementation," and out-of-scope
items include Azure-specific metadata, client update logic, and a dataset download API.
It is meant to be reused by later work in the dataset-publishing epic rather than tied to
any one of it - most directly by #135 (verifying the integrity of a published dataset),
which needs a checksum computed from the same, already-validated canonical artifact to
compare against.

## Decision

Introduce `DatasetArtifactMetadata` (`Application/Contracts`) as a plain, publisher-
independent record, with `IDatasetArtifactMetadataFactory` (`Application/Ports`) as the
abstraction that produces it and `FileDatasetArtifactMetadataFactory`
(`Infrastructure/FileSystem`) as its first implementation:

- `DatasetArtifactMetadata` carries `RegionId`, `Version`, `FileName`, `FileSizeBytes`,
  `CreatedUtc`, and `Sha256Checksum` - exactly the fields the issue asked for, nothing
  Azure- or publisher-specific.
- `FileDatasetArtifactMetadataFactory` reads the file name/size directly from the
  filesystem and streams the artifact through `SHA256.HashDataAsync` (the same streaming
  approach `FileHashDatasetVersionCalculator` already uses, so a large regional SQLite
  database is never read fully into memory). It has no dependency on `PublisherOptions`
  or `IDatasetPublisher`, unlike `FileHashDatasetVersionCalculator`, which does depend on
  `PublisherOptions.SchemaVersion` - the version calculator's job is specifically to
  produce a value that determines whether a publish is skipped, while the metadata
  factory's job is only to describe an artifact that already exists.
- `RenderRegion` calls `IDatasetArtifactMetadataFactory.CreateAsync` right after promoting
  the validated dataset to its canonical `outDir` location and after
  `IDatasetVersionCalculator` has computed the dataset version, reusing that same version
  value so the metadata and the eventual `DatasetPublishRequest` always agree. The
  resulting metadata is logged; nothing yet persists it or threads it into
  `IDatasetPublisher`/`DatasetPublishRequest` - the publish contract is intentionally left
  unchanged so this stays a small, independent addition, and consumers that need the
  metadata (starting with #135) can call `IDatasetArtifactMetadataFactory` themselves
  against the same canonical path.
- Metadata generation only runs for a dataset that has already passed
  `IDatasetValidator` and been promoted - an invalid, quarantined dataset never gets
  metadata, and a run that skips rendering (output already exists, overwrite disabled)
  never calls the factory either.

## Consequences

- `IDatasetArtifactMetadataFactory` is a clean, reusable seam: #135 can call it a second
  time against a publish destination and compare `Sha256Checksum` values without
  introducing its own hashing logic, and any future consumer (a dataset download API, an
  Azure publisher) can depend on `DatasetArtifactMetadata` without depending on how a
  particular publish target works.
- `RenderRegion` gains one more constructor dependency; existing callers (`Program.cs`,
  tests) needed to be updated to provide it, same as when `IDatasetPublisher` and
  `IDatasetVersionCalculator` were introduced.
- Testable: `FileDatasetArtifactMetadataFactory` is covered by integration tests against
  the real filesystem (correct region/version/file name/size, a checksum verified against
  an independently computed SHA-256 hash, checksum stability across repeated calls,
  missing-artifact and invalid-argument handling); `RenderRegion`'s call into
  `IDatasetArtifactMetadataFactory` is covered by unit tests with a mocked dependency
  (correct region ID/version/canonical path, no call when validation fails or rendering is
  skipped, cancellation token forwarded, and the step's place in the pipeline's call
  order).

## References

- #130 - Add dataset artifact metadata
- #135 - Verify published dataset integrity
- ADR 0002 - Local Dataset Publisher
