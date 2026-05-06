# POIneer.Render

**POIneer.Render** is the rendering component of the POIneer project.  
It generates **regional, offline-ready SQLite databases** based on **OpenStreetMap data** and prepares them for consumption by the POIneer app.

The focus is on:

- reproducible builds
- clear responsibilities
- automated tests and CI
- learning and experimentation with the modern .NET ecosystem

---

## 🧠 Overview

POIneer consists of several clearly separated components:

- OpenStreetMap (PBF)
- POIneer.Render
- region.sqlite (Flyway)
- POIneer App (Android)

**POIneer.Render** is neither a server nor an app.  
It is a **deterministic renderer** that transforms raw OSM data into a usable SQLite database.

---

## 🎯 Responsibility of POIneer.Render

The renderer is responsible for:

- 📥 Downloading and processing OSM PBF files
- 🧱 Initializing a SQLite database
- 🛠️ Schema creation and migrations via **Flyway**
- 🗺️ Extracting and preparing POIs (Points of Interest)
- 📦 Producing a final `.sqlite` file per region

**Important:**  
The renderer is **idempotent** — identical input produces identical output.

---

## 🚧 Current MVP Scope

The current MVP intentionally focuses on a small and deterministic scope:

- Berlin as the initial render region
- OSM nodes only
- amenity-focused POI extraction
- SQLite output generation
- reproducible offline builds

Out of scope for the current MVP:

- ways and relations
- full admin UI
- mobile app implementation
- Azure production deployment
- complex multi-region orchestration

---

## 🧩 Architecture & Concepts

The project deliberately follows clear architectural principles:

- Ports & Adapters / Clean Architecture
- Clear separation of:
  - Domain
  - Infrastructure
  - IO (filesystem, processes, tools)
- Core logic is testable without real OSM data
- No hidden global state

### Key Components

- `POIneer.Render`
  - orchestrates the rendering process

- `POIneer.Render.Infrastructure`
  - filesystem access
  - external tools (e.g. Flyway)

- `POIneer.Render.TestHelpers`
  - utilities for integration tests

---

## 🤖 AI / Agent Guidance

This repository contains an `AGENTS.md` file with repository-specific guidance for AI-assisted development tools such as Codex.

Please review it before making larger architectural or workflow-related changes.

---

## 🛠️ Technical Prerequisites

### Local Development

#### Required SDK

- .NET SDK 10.0.201

#### Additional SDKs

- .NET 9.x optional
- .NET 8.x optional

> The repository uses a `global.json` file to pin the expected .NET SDK version.

#### Additional Tools

- Flyway CLI
  - used at runtime for database migrations

#### Supported Platforms

- Linux
- macOS
- Windows

> CI currently runs on Linux.

---

## 🔄 Build & Tests

### Restore

```bash
dotnet restore
```

### Build

```bash
dotnet build --configuration Release
```

### Test & Coverage

```bash
dotnet test \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura
```

---

## ✅ Code Quality & CI

CI pipelines enforce:

- ✅ warning-free builds (`/warnaserror`)
- ✅ consistent formatting (`dotnet format`)
- ✅ successful tests
- ✅ minimum test coverage

### Minimum Coverage

- 60% line coverage (enforced by CI)

### Branch Protection

The `develop` and `main` branches are protected — faulty code cannot be merged.

---

## 🚧 Project Status

### 🟡 Actively Under Development

### Current Focus

- stable render pipeline MVP
- clean database initialization
- reproducible region builds (e.g. Berlin)

### Planned

- configurable regions
- optional tile rendering
- Jenkins-based server pipelines
- delivery of build artifacts to the POIneer app

---

## 📜 License

This project is intended as a learning and open-source project.

The final license will be defined at a later stage.

---

## 🤝 Contributing

Currently a solo project — however, pull requests, feedback, and ideas are welcome.

---

## ✨ Motivation

POIneer.Render was created out of the desire to:

- provide offline-capable POI data in a controlled and privacy-friendly way
- apply modern .NET architecture in practice
- build a clean and reproducible rendering pipeline

---

## 📚 Related Documents

- [Git - Branch Flow Guide](README-GIT-FLOW.md)
- [Git - Pull Requests Flow Guide](README-GIT-PR.md)
- [Git - Handling Dependabot Branches](README-GIT-DEPENDABOT.md)