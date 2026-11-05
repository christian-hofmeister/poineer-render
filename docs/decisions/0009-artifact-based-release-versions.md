# Artifact-based release versions

## Decision

A release identifies the exact completed artifact set, including SQLite's Flyway
history. Calculate a shared version from Publisher:SchemaVersion and the content
hashes of SQLite plus PMTiles when enabled, after generation and promotion and
before publishing. Do not derive this version from the source PBF.

Hash each artifact with SHA-256 using streaming reads. Sort by normalized file
extension and hash newline-delimited records of extension, colon, and uppercase
SHA-256 hex. Use the first 16 lowercase hex characters of that aggregate hash,
prefixed with SchemaVersion and a hyphen. Reject empty sets and duplicate types.
Directory paths, filenames, and caller ordering do not affect identity.

## Consequences

- A forced rebuild with changed Flyway timestamps or execution times gets a new
  version even when the POIs are unchanged.
- Changes in either artifact, or enabling/disabling PMTiles, change the shared
  release identity. Individual metadata still includes full file checksums.
- SkipIfIdentical remains appropriate for identical releases. Normal publication
  does not need to replace a previously published version.
- Existing PBF-derived published filenames remain untouched. New renders use the
  new algorithm; clients must treat versions as opaque identifiers.
- PBF download-state and render-state checkpoints are unaffected.
- This does not add semantic content comparisons, publication-only retry, atomic
  multi-artifact publication, or release manifests.

This supersedes the input-based versioning portion of decision 0002.
