# Region update interval configuration

The update interval is expressed in whole days. The built-in default is seven days.
Set a different default in application configuration:

```json
{
  "Renderer": {
    "DefaultUpdateIntervalDays": 7
  }
}
```

Environment-specific application settings may override the default. Environment
variables and CLI arguments can also override it:

```text
POINEER_RENDER__Renderer__DefaultUpdateIntervalDays=14
--Renderer:DefaultUpdateIntervalDays=14
```

An individual entry in the configured regions JSON file can override this value:

```json
[
  {
    "Id": "geofabrik/europe/germany/berlin",
    "Name": "Berlin",
    "PbfUrl": "https://download.geofabrik.de/europe/germany/berlin-latest.osm.pbf",
    "UpdateIntervalDays": 3
  }
]
```

Omitting `UpdateIntervalDays` or setting it to `null` inherits the global default.
Both the global default and explicit region overrides must be positive integers.
Invalid defaults fail options validation at startup. Non-positive region overrides
are rejected individually before region work files or remote requests are made.
The error log identifies the region and `UpdateIntervalDays`; other selected
regions continue processing. The overall run returns exit code 1 if any selected
region fails validation or processing. Unselected regions are not validated.
Malformed JSON or values that cannot deserialize as integers still fail loading
the configuration as a whole.
Existing region files without the new property remain compatible.

`RegionUpdateIntervalPolicy` resolves the effective value as the region override
followed by the global default.

## Persisted due dates

The daily systemd timer remains unchanged. The renderer reads
`<WorkDir>/<RegionId>/schedule-state.json` before making a remote request.
Regions without a valid schedule checkpoint are immediately eligible. Other
regions are skipped before HTTP, download, or rendering until `NextDueUtc` is
reached (the boundary is inclusive). Logs identify skipped regions and their next
due time. Times are stored and compared in UTC using an injected `TimeProvider`.

For a due region:

1. Check remote metadata using the existing download checkpoint.
2. If the source is unchanged, a previous check/processing succeeded, all enabled
   canonical outputs exist, and no processing is pending, skip download and
   rendering. Persist a successful check and its next due time.
3. Otherwise persist `ProcessingPending=true` before downloading or rendering.
   Reuse an unchanged local PBF when possible. Perform rendering, publishing, and
   verification, then update the render checkpoint.
4. Only after all required processing succeeds, store `LastSuccessfulCheckUtc`
   and `NextDueUtc = completion time + effective interval`, and clear the pending
   flag. A later unchanged check also advances the schedule from its completion
   time, without modifying the last successful render checkpoint.

Failures retain the previous successful check and next due time. Pending
processing is always due, even if a previous next due time is still in the future.
The next daily run retries failed processing, including publishing failures after
canonical outputs were promoted; existing files do not count as processing success.
Cancellation also leaves pending processing due. Download checkpoints remain
independent, so completed downloads are reused on retries.

Schedule files are written through a temporary file and replaced atomically.
Missing, malformed, or incomplete schedule checkpoints, or checkpoints belonging
to a different region/source URL, are treated as absent. Existing installations
perform an initial processing run to establish the schedule; a previous render
checkpoint alone does not establish a successful scheduled check. Keep WorkDir
on the persistent Docker data mount. Changing an interval affects the next
successful check's due date; it does not recalculate an existing persisted date.

Existing `OverwriteDatabase` or `OverwritePbf` flags bypass the due-date gate and
retain their existing rebuild/download effects. `OnlyRegionId` selects a region
but does not bypass its due date. A separate manual check override that retains
the normal download decision, and spreading updates across different days, remain
subsequent steps.
