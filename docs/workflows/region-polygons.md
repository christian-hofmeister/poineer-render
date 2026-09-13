# Create a Region Polygon

A custom region uses a source OSM PBF and a polygon that defines the area to extract.
Keep the editable GeoJSON and the matching Osmosis `.poly` file together in
`src/POIneer.Render/Cli/config/polygons/`.

## Draw the Boundary

1. Open [geojson.io](https://geojson.io/) and navigate to the region.
2. Select the polygon drawing tool and click around the desired area.
3. Close the outline by clicking its starting point.
4. Check the boundary at a closer zoom, especially near coasts and access roads.
5. Save/export as GeoJSON and name the file using the conventions below.
6. To revise it later, open the saved GeoJSON in the editor, adjust the boundary,
   and regenerate the matching `.poly` file.

Use one Feature containing a `Polygon` with one outer ring for a simple region.
A `LineString` is a line, not an extraction area. `.poly` is a separate file format,
not a GeoJSON geometry type. Coordinates use longitude first, latitude second,
in degrees. The GeoJSON ring must repeat its first point at the end.

Choose the area for the intended offline experience. For islands, leave room around
the coastline and include relevant harbors, access roads, and arrival points. For
Rügen and Hiddensee, this can include Stralsund's harbor, old town, railway station,
and bridge approaches. Review any additional mainland deliberately included.
An extraction boundary is not necessarily an administrative boundary or an exact
coastline. It also does not guarantee that ferry routes will be rendered; that
depends on the source data and tile profile.

## Naming Conventions

- Use a stable lowercase ASCII slug with hyphens: `ruegen-hiddensee`.
- Transliterate German characters: `ä` -> `ae`, `ö` -> `oe`, `ü` -> `ue`, `ß` -> `ss`.
- Avoid spaces, dates, and suffixes such as `final` or `v2`; Git tracks revisions.
- Give both files the same basename: `ruegen-hiddensee.geojson` and
  `ruegen-hiddensee.poly`. Use the slug as the first line of the `.poly` file.
- Keep readable display names separate, for example `Rügen and Hiddensee`.
- Region IDs follow [ADR 0007](../decisions/0007-hierarchical-region-identifiers.md).
  A proposed ID for this custom extract is
  `geofabrik/europe/germany/mecklenburg-vorpommern/ruegen-hiddensee`.
  This identifies a custom cut from the parent PBF, not a separate Geofabrik download.
- If a basename would collide with another region, add a meaningful geographic
  qualifier before introducing the new files.

## Convert GeoJSON to `.poly`

For the simple single-ring case, conversion is a direct text transformation:

1. Read `features[0].geometry.coordinates[0]` from the saved GeoJSON.
2. Write the region slug on the first line and `1` on the second line.
3. Write each coordinate pair as `longitude latitude`, separated by whitespace,
   without JSON brackets or commas. Preserve the coordinate precision and order.
4. Keep the repeated starting coordinate at the end.
5. Add `END` to close the ring, then another `END` to close the file.
6. Save as UTF-8 text with the `.poly` extension.

For example, this illustrative triangle shows the file structure only; it is not
the Rügen boundary:

```text
example-region
1
  13.0 54.3
  13.1 54.3
  13.1 54.4
  13.0 54.3
END
END
```

See the committed [GeoJSON example](../../src/POIneer.Render/Cli/config/polygons/ruegen-hiddensee.geojson)
and [matching polygon](../../src/POIneer.Render/Cli/config/polygons/ruegen-hiddensee.poly).
Do not merely rename a GeoJSON file to `.poly`. When copying JSON from chat or HTML,
remove encoded whitespace such as `&#x20;` and check that it still parses as JSON.

This procedure covers one outer ring only. Do not discard extra features or holes
silently. Multiple outer rings need separate sections; excluded rings use section
names prefixed with `!`. Consult the
[Osmosis polygon format reference](https://wiki.openstreetmap.org/wiki/Osmosis/Polygon_Filter_File_Format)
before converting more complex geometry.

## Review and Integration

Before using the polygon:

- Check that the ring is closed, has at least three distinct vertices, and does
  not cross itself.
- Check the outline on the map; geometric validity alone does not prove geographic
  coverage. Verify the intended islands and arrival points are inside.
- Check that GeoJSON and `.poly` contain the same coordinates.
- Select a source PBF covering the entire desired area. The Rügen example uses
  `https://download.geofabrik.de/europe/germany/mecklenburg-vorpommern-latest.osm.pbf`.
  A polygon cannot add data outside the source extract.

The region JSON uses `PbfUrl` for that source and `Poly` for the local polygon path.
The polygon assets alone do not register or enable a region.

Current implementation limitations to account for when wiring a region:

- `Poly` is passed directly to the cutter. Relative paths currently depend on the
  process working directory, not the region JSON directory. Use a verified absolute
  runtime path until robust path resolution is implemented.
- The project currently copies `Cli/config/*.json` during build/publish, but does
  not automatically include the `polygons/` directory. Add packaging support or
  explicitly provide the polygon at the configured runtime location.
- A missing polygon file currently causes the cutter to use the entire source PBF.
  Verify that the file exists before rendering; fail-fast handling remains pending.

Commit changes to both geometry files together and describe any meaningful coverage
change, such as adding a mainland arrival area, in the commit or pull request.
