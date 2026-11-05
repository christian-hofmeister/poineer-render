# PBF download and render checkpoints

Each region work directory contains two independent checkpoints:

- `download-state.json` records the source URL, region ID, ETag/Last-Modified,
  content length, and completion time of a successful PBF download.
- `render-state.json` records the source metadata only after the render use case
  finishes successfully.

The renderer checks remote metadata with HEAD on each run. It reuses an existing
PBF when its download checkpoint matches the remote ETag (or Last-Modified when
no ETag is available), even if the previous render failed. Rendering checks still
run after a download is skipped. Missing PBF files, changed metadata, and
`Renderer:OverwritePbf=true` trigger a download. Without a reliable remote
validator, the renderer conservatively downloads again.

Before downloading, the previous download checkpoint is removed. A replacement
checkpoint is written atomically only after downloading completes. Interrupted
or failed downloads therefore cannot reuse a stale checkpoint for a partial file.
The last successful render checkpoint is preserved if a later attempt fails.

Existing installations without a download checkpoint perform one initial download,
even if a PBF or render checkpoint already exists. No checkpoint is inferred from
an unverified local file. Rebuild the Docker image to use this behavior, and keep
the region work directory on a persistent mount across container runs.
