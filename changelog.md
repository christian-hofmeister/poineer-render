# Changelog

All notable changes to POIneer.Render will be documented in this file.

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
