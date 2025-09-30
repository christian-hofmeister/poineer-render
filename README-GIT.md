
---

## Benefits

- Clear, professional history
- Easy to scan in `git log --oneline`
- Tools can auto-generate changelogs
- Recruiters see structured workflow

---

## Quick Reference

- **feat:** new feature  
- **fix:** bug fix  
- **docs:** documentation only  
- **test:** add/modify tests  
- **ci:** CI/CD related  
- **chore:** config/deps/maintenance  
- **refactor:** restructure code  
- **style:** formatting only  
- **perf:** performance improvements


# Git Commit Message Cheat Sheet (Conventional Commits)

Conventional Commits help to keep history clean, readable, and automatable.  
Format: `<type>(<scope>): <short summary>`


Optional body with more details.

---

## Types

### feat
**New feature** for the user.

<code>feat(domain): add PropertyListing aggregate and Incident entity</code>

### fix
**Bug fix**.

<code>fix(infrastructure): correct DbContext mapping for owned types</code>

### docs
Documentation changes only.

<code>docs(readme): add Swagger setup and usage instructions</code>

### test
Add or modify **tests**.

<code>test(domain): add unit tests for Money and Address validation</code>

### ci
Changes to CI/CD configuration.

<code>chore(ci): add GitHub Actions workflow</code>

### chore
General tasks, dependencies, config updates (no production code change).

<code>chore: bump Swashbuckle.AspNetCore to 6.6.2</code>

### refactor
Code change that doesn’t add features or fix bugs (improves structure).

<code>refactor(webapi): simplify endpoint mapping and response objects</code>

### style
Code style/formatting (no logic change).

<code>style(domain): apply consistent naming to value objects</code>

### perf
Performance improvement.

<code>perf(repository): optimize query for listing with incidents</code>


# Scopes

- core – Domain-/Kernlogik (Use-Cases, Services)
- adapters – Adapter & IO (Dateisystem, OSM-Reader, CLI)
- osm – Spezifisch OSM-Parsen/Modelle
- tiles – Tile-/Chunk-/Grid-Logik
- db – SQLite, Schema, Migrations, Writer
- config – Konfiguration/Settings/Validation
- logging – Logging, Diagnostics, Telemetry
- cli – Kommandozeile/Runner/Program
- build – Buildsystem (csproj, SDK, TargetFramework)
- deps – Dependency-Updates (NuGet)
- tests – Tests allgemein
- unit-tests, integration-tests, contract-tests – falls du feiner trennen willst
- test-helpers – euer neues Helpers-Projekt
- ci – Jenkinsfile, Pipeline, Coverage
- docs – README, Doku
- scripts – Repo-Skripte (scripts/...)