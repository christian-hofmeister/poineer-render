# Changelog

All notable changes to POIneer.Render will be documented in this file.

## [0.4.0]

### Added

- Defined the region manifest contract and added region catalog and manifest publishing for local filesystem and Azure Blob storage. Metadata and dataset artifacts can use separate storage roots.
- Added publishing and verification for PMTiles vector tile artifacts alongside SQLite datasets.
- Added a configurable minimum free disk space check before processing regions.
- Added optional cleanup of local render files after verified remote publishing.

### Changed

- Aligned region ID validation with the region manifest contract.
- Geofabrik sources now use the published MD5 to verify PBF files before rendering. Scheduled checks skip download and rendering when the checksum matches the last successfully published render, even after local cleanup.
- Updated .NET, Azure Storage, SQLite, and test dependencies.

## [0.3.0] - 2026-09-06

### Added

- Added Azure Blob dataset publishing and infrastructure provisioning.
- Added remote PBF change detection based on HTTP metadata.

### Changed

- Switched configured region IDs to hierarchical paths, such as `geofabrik/europe/germany/berlin`.
- Bumped the published dataset schema version to `2`.
- Moved scheduled VPS renders from cron to a systemd timer.
- Separated local development and production renderer configuration and improved local path resolution.

## [0.2.1] - 2026-08-28

### Added

- Added Jenkins promotion for verified release Docker images to versioned and `production` tags.
- Added Jenkins build parameters for release Docker tool checksums.
- Added release Docker image pruning that keeps the two newest semantic release tags.

### Changed

- Updated Docker renderer workflow documentation for production image usage and release promotion.

## [0.2.0] - 2026-08-27

### Added

- Added production Docker image for POIneer.Render.
- Added Jenkins Docker image build and verification.
- Added Docker Flyway migration smoke test against temporary SQLite.
- Added regression coverage for the production single-instance lock path.

### Changed

- Pinned the Docker runtime image to .NET `10.0.11-noble`.
- Moved the production single-instance lock into the shared data mount.
- Updated Microsoft/.NET dependencies to the current patch line.
- Documented Docker logging via stdout/stderr and host logging drivers.

### Removed

- Removed the ineffective Docker logs volume recommendation.
