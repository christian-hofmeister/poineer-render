# ADR 0006: Integrated Vector Tile Generation for Regional Render Outputs

## Status

Proposed

## Context

POIneer.Render currently produces a POI dataset (`poi.sqlite`) for a rendered region. This
contains individual points with attributes, but no road, building, land-use, or other basemap
geometry. That is enough to place POI pins, but not enough for an offline map view in the
mobile application.

Pre-rendering raster tiles for full offline coverage was considered and rejected: tile count
and storage grow roughly 4x per zoom level, so covering a region at the zoom levels a
navigation-style app needs is not realistic to ship as a downloadable offline artifact.

The preferred basemap artifact is a regional vector tile archive:

- Vector tiles keep geometry compact and allow the mobile app to style the map at render time.
- PMTiles is a single-file artifact that matches POIneer's region download model.
- MapLibre Native for Android has documented support for local PMTiles files via
  `pmtiles://file://<path>`. iOS support remains a separate mobile-side concern.

Generating vector tiles inside POIneer.Render from first principles would require substantial
GIS-specific implementation:

- OSM feature processing
- zoom-dependent generalization
- MVT encoding
- tile indexing
- PMTiles archive generation

[Planetiler](https://github.com/onthegomap/planetiler) already provides this functionality and
can generate PMTiles directly from OSM PBF input. POIneer.Render should therefore orchestrate
Planetiler instead of implementing a custom vector tile pipeline.

An earlier direction in this ADR favored a separate `MapTileBuilder` pipeline with independent
scheduling and later manifest-based coordination. That approach reduced coupling, but it also
introduced a release coordination problem: `poi.sqlite` and the basemap could be produced from
different OSM snapshots, fail independently, and require extra publishing state before the
mobile app could safely consume both artifacts as one regional release.

For the current MVP, consistency between the POI dataset and the offline basemap is more useful
than independent scheduling. The regional render should produce both artifacts from the same
regional OSM source during the same render execution.

## Decision

1. **Generate vector tiles as part of the existing regional render pipeline.** The scheduled
   render process remains the single entry point. For each rendered region, POIneer.Render
   should generate `poi.sqlite` and, when vector tile generation is enabled, `map.pmtiles` in
   the same render execution.

2. **Introduce an `IVectorTileGenerator` abstraction.** Render orchestration coordinates vector
   tile generation through an application-level interface, for example:

   ```csharp
   public interface IVectorTileGenerator
   {
       Task GenerateAsync(
           string pbfPath,
           string outputPath,
           CancellationToken cancellationToken);
   }
   ```

   `RenderRegion` may coordinate when this operation runs, but it must not contain
   Planetiler-specific process execution logic.

3. **Use Planetiler as the first vector tile generator implementation.**
   `PlanetilerVectorTileGenerator` invokes Planetiler as an external process, passes the
   regional OSM PBF input path and the target `map.pmtiles` output path, captures stdout and
   stderr, forwards relevant output to `ILogger`, propagates cancellation where possible, and
   fails if Planetiler exits with a non-zero exit code.

4. **Publish regional artifacts as one render output.** For a region such as Berlin, the render
   output should contain:

   ```text
   berlin/
   |-- poi.sqlite
   `-- map.pmtiles
   ```

   The PMTiles artifact name is always `map.pmtiles`. Both files belong to the same regional
   dataset generation and originate from the same regional OSM input.

5. **Make vector tile generation configurable.** Planetiler-specific values must be supplied
   through configuration rather than hard-coded. Initial options may be limited to what the MVP
   implementation needs, such as:

   ```text
   Enabled
   JavaExecutablePath
   PlanetilerJarPath
   Profile
   MinZoom
   MaxZoom
   ```

6. **Fail the render when requested vector tile generation fails.** If vector tile generation is
   enabled for a render, the render must fail when Planetiler cannot be started, exits
   unsuccessfully, generation is cancelled, or the expected `map.pmtiles` file is not created.
   Partial or failed PMTiles artifacts must not be treated as successful regional output.

7. **Defer independent tile scheduling and remote publishing.** A future larger-scale pipeline
   may split POI and tile generation again if Planetiler runtime, memory, or scheduling pressure
   becomes too high for the shared render job. That future design will need explicit artifact
   versioning or manifest coordination. It is not part of the current MVP decision.

## Consequences

- `poi.sqlite` and `map.pmtiles` are generated from the same regional OSM source, so the mobile
  app can treat them as one coherent regional dataset release.
- Scheduling remains simple: the existing scheduled render remains the entry point, and
  scheduling does not move into individual dataset builders.
- Render failures are easier to reason about because POI and map artifacts succeed or fail as
  part of one regional execution.
- Vector tile generation remains isolated from POI dataset generation behind
  `IVectorTileGenerator`.
- POIneer.Render gains a JVM and Planetiler dependency when vector tile generation is enabled.
- A slow or failed Planetiler run can block completion of the regional render. This is accepted
  for the MVP in exchange for simpler coordination and artifact consistency.
- Running POI and tile generation independently, selecting regions by artifact staleness, and
  manifest-based "whoever finishes last publishes" coordination are deferred until there is a
  concrete scaling need.

## Testing Guidance

Tests should cover the orchestration boundary without requiring Java or Planetiler for ordinary
unit tests.

At minimum, verify:

- vector tile generation is invoked with the expected regional PBF path
- the expected `map.pmtiles` output path is passed
- cancellation is propagated
- a failed generator causes the render to fail
- vector tile generation can be disabled through configuration, if the MVP introduces an
  `Enabled` option
- Planetiler process exit failures are handled correctly
- the Planetiler implementation verifies that the expected output file was created

Process execution should stay behind an abstraction where useful so tests can simulate exit
codes, stderr/stdout, cancellation, and missing output files deterministically.

## Out of Scope

This decision does not include:

- mobile MapLibre integration
- map styling
- online tile serving
- POI image handling
- custom implementation of PMTiles
- custom implementation of vector tile generation
- publishing `map.pmtiles` to remote storage
- validation of PMTiles contents beyond basic artifact existence
- independent `MapTileBuilder` scheduling
- manifest-based coordination between independently produced POI and tile artifacts

## References

- ADR 0002 - Local Dataset Publisher
- ADR 0005 - Automated VPS Deployment
- [Planetiler](https://github.com/onthegomap/planetiler) - OSM PBF to PMTiles generator
- [MapLibre Native Android - PMTiles support](https://maplibre.org/maplibre-native/android/examples/data/PMTiles/) -
  local `pmtiles://file://` sources since v11.7.0
