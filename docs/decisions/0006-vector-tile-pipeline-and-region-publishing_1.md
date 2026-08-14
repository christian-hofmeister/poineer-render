# ADR 0006: Vector Tile Pipeline, Region Publishing & Update Scheduling

## Status

Proposed

## Context

POIneer.Render currently only produces a POI dataset (`poi.sqlite`) - individual points
(lat/lon plus attributes) with no road, building, or land-use geometry. That is enough to
place pins, but not enough to render an actual offline basemap: without the underlying OSM
ways/relations, a map view offline is just markers floating on an empty background.

Pre-rendering raster tiles for full offline coverage was considered and rejected: tile count
(and therefore storage) grows roughly 4x per zoom level, so covering a region at the zoom
levels a navigation-style app needs is not realistic to ship as a downloadable offline
artifact.

The alternative - vector tiles rendered on-device - avoids that storage blowup, because the
same compact geometry data is restyled at render time instead of being baked into per-zoom
raster images. This requires:

- A **vector tile generator** that turns a raw OSM PBF extract into tiles. We evaluated
  building this ourselves versus using an established tool, and chose
  [Planetiler](https://github.com/onthegomap/planetiler) - it is purpose-built for exactly
  this (OSM PBF -> vector tiles), actively maintained, and outputs **PMTiles** directly
  (a single flat file, not a tile server).
- A **client-side renderer** that can read that output. MapLibre Native for Android has
  documented, built-in support for local PMTiles files since v11.7.0
  (`pmtiles://file://<path>`), which matches our distribution model - a fully pre-built file
  downloaded once, not a live tile server. No equivalent confirmation exists yet for iOS,
  which is a separate, mobile-side concern tracked outside this ADR.

Planetiler's own guidance puts regional (non-planet) memory needs at roughly 0.5x the input
`.osm.pbf` size in RAM and 5-10x in disk - modest for a single region, but the VPS running
POIneer.Render's existing pipeline is not provisioned generously, and this is materially
heavier (and requires a JVM) than the existing .NET-only POI extraction. Coupling the two
into one pipeline would mean a slow or failed tile build blocks POI updates, and vice versa.

Once there are more than a handful of regions, a further problem appears: if a pipeline run
simply iterates `regions.production.json` from the top each time and does not have the
capacity to get through the whole list, regions further down the file are starved - they
never get processed while the run keeps restarting at position 1.

Finally, splitting POI and tile generation into independent pipelines creates a coordination
gap: something needs to decide when a region has *both* artifacts and is safe to expose to
the mobile app, without a window where a consumer could see one artifact but not the other.

## Decision

1. **Add a second, independent build pipeline: `MapTileBuilder`.** It invokes Planetiler as
   an external process (JVM dependency, separate from the existing .NET toolchain) against
   the same per-region OSM PBF extract `PoiDatasetBuilder` uses, producing
   `<region>.pmtiles`. It is triggered and scheduled independently of `PoiDatasetBuilder` -
   nothing requires the two to run together, and either can be moved to different compute
   (e.g. a temporary higher-memory build host) without touching the other.

2. **Coordinate publishing through a manifest, not direct artifact checks.** A single
   `manifest.json`, published to Azure Blob Storage, is the sole source of truth for what's
   available. Per region, it tracks `poi: { lastBuiltAt }`, `tiles: { lastBuiltAt }`, and a
   `published` flag. Neither builder sets `published` unilaterally: on successfully finishing
   its own artifact for a region, a builder updates only its own field, then checks whether
   the sibling artifact's field is already present for that region. If so, *that* builder
   flips `published: true`. This "whoever finishes last flips the switch" pattern needs no
   separate watcher/poller and is correct regardless of which pipeline happens to run first.
   The mobile app (and any future consumer) reads only this manifest - it never probes Blob
   paths directly - which removes the race window entirely.

3. **Select regions to (re)build by staleness, not list position.** Each pipeline run sorts
   regions by `lastBuiltAt` for its own artifact type (a region never built counts as
   infinitely stale, so new regions are always prioritized) and processes oldest-first until
   either the list is exhausted or a per-run **time budget** is spent - not until a fixed
   count N is reached, since region extract sizes vary enough that a fixed N could either
   waste capacity or blow the budget. `lastBuiltAt` is only updated on success, so a run that
   fails or is interrupted partway leaves the affected regions stale and automatically
   eligible for immediate retry next run, with no separate failure-handling path required.

4. **Explicitly out of scope for now: version-pairing between POI and tile artifacts.** A
   region is "published" once both artifact types have completed at least once; later,
   independent updates to either side do not require rebuilding the other. This accepts a
   known, low-severity inconsistency window (e.g. a newly-opened venue's pin appearing before
   its building shows up on the basemap) as an acceptable tradeoff rather than a problem worth
   solving up front.

## Consequences

- POI and tile generation can run on independent schedules and independent hardware; a slow
  or failing Planetiler run no longer blocks POI updates.
- New or previously-failed regions are always prioritized on the next run regardless of their
  position in `regions.production.json`, so the "only the top 40 ever get updated" starvation
  scenario cannot happen.
- No special recovery logic is needed for a failed/interrupted run - staleness-based
  selection self-heals on the next run.
- Adds a JVM runtime dependency (for Planetiler) wherever `MapTileBuilder` runs, alongside the
  existing .NET toolchain.
- `manifest.json` is a new shared piece of state both pipelines depend on. This design does
  not yet handle two overlapping runs of the *same* artifact type writing to it concurrently;
  if run frequency increases to the point that's a real risk, add optimistic-concurrency
  writes (e.g. Azure Blob conditional `ETag` writes) rather than assuming it away.
- No consistency is enforced between POI and basemap versions (see Decision point 4) - revisit
  only if real usage shows this is a noticeable problem, not preemptively.
- The manifest's per-region availability list is a natural foundation for a future in-app
  region picker, though building that UI is not part of this decision.

## References

- ADR 0002 - Local Dataset Publisher (the existing POI distribution pattern this mirrors)
- ADR 0005 - Automated VPS Deployment (existing pipeline/VPS constraints this extends)
- [Planetiler](https://github.com/onthegomap/planetiler) - OSM PBF -> PMTiles generator
- [MapLibre Native Android - PMTiles support](https://maplibre.org/maplibre-native/android/examples/data/PMTiles/) -
  local `pmtiles://file://` sources since v11.7.0
