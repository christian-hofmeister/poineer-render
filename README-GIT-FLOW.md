# 🧭 Branch Flow Guide

This document describes the recommended **Git branch workflow** for the project.

---

## 🌿 Purpose

We use a **simplified GitFlow strategy**:

| Branch        | Purpose |
|---------------|--------|
| `main`        | Production-ready, tagged releases |
| `develop`     | Integration of all completed features, stable development state |
| `feature/*`   | New features, refactorings, or tasks |
| `release/*`   | Release preparation (bug fixes, version bump, documentation) |
| `hotfix/*`    | Emergency fixes directly from `main` (e.g., production issues) |

---


## 🔄 Overview

```text
feature --> develop --> release/x.y.z --> main (tag)
                 ^            |              |
                 |            v              v
               (back) <--- bugfixes      hotfix --> main(tag) & develop
```
# 🚀 Feature Branch Flow

## 1. Branch off from develop

```
git checkout develop
git pull
git checkout -b feature/<kurzer-titel>
```

## 2. Develop & commit regularly

```
git commit -m "feat: implement xyz"
git push origin feature/<kurzer-titel>
```

## 3. Pull Request (PR) from feature/* → develop

- Use PR template
- Keep PRs small and focused
- All checks (build, tests, lint, coverage) must pass
- At least one review required

## 4. Merge-method:

- ✅ Squash Merge

- PR title = commit message (e.g. feat: add amenity filter)

# 📦 Release Branch Flow

## 1. Create once develop is stable:

```git checkout -b release/<version> develop```


## 2. Final adjustments & version bump

- Changelog, documentation, minor bug fixes
- No new features!

## 3. PR: release/<version> → main
- CI must pass
- Reviewers verify stability

## 4. After merge into main:

```bash
git tag v<version>
git push origin v<version>
```

## 5. Back-merge into develop to sync changes:

```bash
git checkout develop
git merge --no-ff main
git push
```


# 🛠️ Hotfix Branch Flow

## 1. Branch off from main

```bash
git checkout -b hotfix/<version> main
```

## 2. Fix + Tests

## 3. PR: hotfix/<version> → main

- After merge → create tag v<version>

```bash
git tag v<version>
git push origin v<version>
```

# 4. Back-merge into develop

```bash
git checkout develop
git merge --no-ff main
git push
```

# ⚙️ Merge-Rules

| Zielbranch | Quelle                  | Merge-Art    | Review       | Checks |
| ---------- | ----------------------- | ------------ | ------------ | ------ |
| `develop`  | `feature/*`, `hotfix/*` | Squash       | ✅ 1 reviewer | ✅ all |
| `main`     | `release/*`, `hotfix/*` | Merge-Commit | ✅ 1 reviewer | ✅ all |
| `develop`  | `main` (Backmerge)      | Merge-Commit | ❌            | ❌      |

# 🧹 Naming Conventions

| Typ     | Muster                       | Beispiel                     |
| ------- | ---------------------------- | ---------------------------- |
| Feature | `feature/<kurzbeschreibung>` | `feature/add-amenity-filter` |
| Release | `release/<semver>`           | `release/1.4.0`              |
| Hotfix  | `hotfix/<semver>`            | `hotfix/1.4.1`               |

🧩 Commit Convention (Conventional Commits)

Please use consistent prefixes for PR titles and commits:

| Typ         | Bedeutung                             | Beispiel                                  |
| ----------- | ------------------------------------- | ----------------------------------------- |
| `feat:`     | Neues Feature                         | `feat: add offline region download`       |
| `fix:`      | Fehlerbehebung                        | `fix: prevent crash when amenity is null` |
| `refactor:` | Code-Umstrukturierung                 | `refactor: extract tile parser`           |
| `test:`     | Tests                                 | `test: add coverage for renderer`         |
| `docs:`     | Dokumentation                         | `docs: update setup instructions`         |
| `chore:`    | Sonstige Änderungen (z. B. Build, CI) | `chore: update CI pipeline`               |

✅ CI & PR Checks

Every pull request must:

- pass the build
- successfully run unit tests
- pass linter & formatter
- have coverage ≥ 60% (configurable)
- have at least one reviewer

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

