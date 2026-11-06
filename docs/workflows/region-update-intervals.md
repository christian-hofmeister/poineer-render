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

## Implementation status

This change provides configuration, validation, and interval resolution only.
The runner does not yet use the interval to skip regions. Persisting due dates,
advancing them after successful checks/processing, distributing updates across
days, and a manual scheduling override are subsequent steps. The existing daily
systemd timer and current download/render behavior are unchanged by these settings.
