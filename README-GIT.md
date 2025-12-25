# 🧭 Branch Flow Guide

Dieses Dokument beschreibt den empfohlenen **Git-Branch-Workflow** für das Projekt.

---

## 🌿 Hauptzweck

Wir verwenden eine **vereinfachte GitFlow-Strategie**:

| Branch        | Zweck |
|----------------|-------|
| `main`         | Produktionsreife, getaggte Releases |
| `develop`      | Integration aller fertigen Features, stabiler Entwicklungsstand |
| `feature/*`    | Neue Features, Refactorings oder Aufgaben |
| `release/*`    | Vorbereitung eines Releases (Bugfixes, Version bump, Doku) |
| `hotfix/*`     | Notfall-Fixes direkt von `main` (z. B. Produktionsfehler) |

---

## 🔄 Übersicht

```text
feature --> develop --> release/x.y.z --> main (tag)
                 ^            |              |
                 |            v              v
               (back) <--- bugfixes      hotfix --> main(tag) & develop
```
# 🚀 Feature Branch Flow

## 1. Abzweigen von develop

```
git checkout develop
git pull
git checkout -b feature/<kurzer-titel>
```

## 2. Entwickeln & regelmäßig committen

```
git commit -m "feat: implement xyz"
git push origin feature/<kurzer-titel>
```

## 3. Pull Request (PR) von feature/* → develop

- PR-Template nutzen

- Kleine, fokussierte PRs

- Alle Checks (Build, Tests, Lint, Coverage) grün

- mind. ein Review erforderlich

## 4. Merge-Methode:

- ✅ Squash Merge

- R-Titel = Commit-Message (z. B. ```feat: add amenity filter```)

# 📦 Release Branch Flow

## 1. Erstellen, sobald develop stabil ist:

- ```git checkout -b release/<version> develop```


## 2. Letzte Korrekturen & Version bump

- Changelog, Doku, kleinere Bugfixes

- Keine neuen Features mehr!

## 3. PR: release/<version> → main

- CI muss grün sein

- Reviewer prüfen Stabilität

## 4. Nach Merge in main:

```
git tag v<version>
git push origin v<version>
```

## 5. Backmerge in develop, um Änderungen zu synchronisieren:

```
git checkout develop
git merge --no-ff main
git push
```


# 🛠️ Hotfix Branch Flow

## 1. Abzweigen von main

git checkout -b hotfix/<version> main


## 2. Fix + Tests

## 3. PR: hotfix/<version> → main

- Nach Merge → Tag v<version> erstellen

```
git tag v<version>
git push origin v<version>
```

# 4. Backmerge in develop

```
git checkout develop
git merge --no-ff main
git push
```

# ⚙️ Merge-Regeln

| Zielbranch | Quelle                  | Merge-Art    | Review       | Checks |
| ---------- | ----------------------- | ------------ | ------------ | ------ |
| `develop`  | `feature/*`, `hotfix/*` | Squash       | ✅ 1 Reviewer | ✅ Alle |
| `main`     | `release/*`, `hotfix/*` | Merge-Commit | ✅ 1 Reviewer | ✅ Alle |
| `develop`  | `main` (Backmerge)      | Merge-Commit | ❌            | ❌      |

# 🧹 Benennungsrichtlinien

| Typ     | Muster                       | Beispiel                     |
| ------- | ---------------------------- | ---------------------------- |
| Feature | `feature/<kurzbeschreibung>` | `feature/add-amenity-filter` |
| Release | `release/<semver>`           | `release/1.4.0`              |
| Hotfix  | `hotfix/<semver>`            | `hotfix/1.4.1`               |

🧩 Commit-Konvention (Conventional Commits)

Bitte nutze für PR-Titel und Commits einheitliche Präfixe:Bitte nutze für PR-Titel und Commits einheitliche Präfixe:

| Typ         | Bedeutung                             | Beispiel                                  |
| ----------- | ------------------------------------- | ----------------------------------------- |
| `feat:`     | Neues Feature                         | `feat: add offline region download`       |
| `fix:`      | Fehlerbehebung                        | `fix: prevent crash when amenity is null` |
| `refactor:` | Code-Umstrukturierung                 | `refactor: extract tile parser`           |
| `test:`     | Tests                                 | `test: add coverage for renderer`         |
| `docs:`     | Dokumentation                         | `docs: update setup instructions`         |
| `chore:`    | Sonstige Änderungen (z. B. Build, CI) | `chore: update CI pipeline`               |

✅ CI & PR Checks

Jeder Pull Request muss:

- den Build bestehen
- Unit Tests erfolgreich ausführen
- Linter & Formatter bestehen
- Coverage ≥ 60 % (konfigurierbar)
- mind. einen Reviewer haben

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


spickzettel:

git push -d <remote_name> <branchname>   # Delete remote
git branch -d <branchname>               # Delete local