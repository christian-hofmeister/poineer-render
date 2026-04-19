# POIneer.Render

**POIneer.Render** is the rendering component of the POIneer project.  
It generates **regional, offline-ready SQLite databases** based on **OpenStreetMap data** and prepares them for consumption by the POIneer app.

The focus is on:
- reproducible builds
- clear responsibilities
- automated tests & CI
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

## 🧩 Architecture & Concepts

The project deliberately follows clear architectural principles:

- **Ports & Adapters / Clean Architecture**
- Clear separation of:
  - Domain
  - Infrastructure
  - IO (filesystem, processes, tools)
- Core logic is testable without real OSM data
- No hidden global state

### Key Components

- `POIneer.Render`
  - Orchestrates the rendering process
- `POIneer.Render.Infrastructure`
  - Filesystem access
  - External tools (e.g. Flyway)
- `POIneer.Render.TestHelpers`
  - Utilities for integration tests

---

## 🛠️ Technical Prerequisites

### Local Development

- **Required SDK**
  - .NET SDK 10.0.201

- **Additional SDKs**
  - .NET 9.x optional
  - .NET 8.x optional

> The repository uses a `global.json` file to pin the expected .NET SDK version.

- **Flyway CLI**
  - Used at runtime for database migrations
- Linux / macOS / Windows (CI runs on Linux)

### CI

- GitHub Actions
- Build, tests, formatting checks and coverage are mandatory

---

## 🔄 Build & Tests

### Restore & Build

```bash
dotnet restore
dotnet build --configuration Release

dotnet test \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura
```

### Minimum coverage:

60% line coverage (enforced by CI)

- ✅ Code Quality & CI

### This repository enforces:

- ✅ Warning-free builds (/warnaserror)

- ✅ Consistent formatting (dotnet format)

- ✅ Successful tests

- ✅ Minimum test coverage

### The develop and main branches are protected — faulty code cannot be merged.

## 🚧 Project Status

### 🟡 Actively under development

### Current focus:
- stable render pipeline MVP
- clean database initialization
- reproducible region builds (e.g. Berlin)

### Planned:
- configurable regions
- optional tile rendering
- Jenkins-based server pipelines
- delivery of build artifacts to the POIneer app

## 📜 License

This project is intended as a learning and open-source project.
The final license will be defined at a later stage.

## 🤝 Contributing

Currently a solo project — however, pull requests, feedback and ideas are welcome.

## ✨ Motivation

POIneer.Render was created out of the desire to:

provide offline-capable POI data in a controlled and privacy-friendly way

apply modern .NET architecture in practice

build a clean and reproducible rendering pipeline


---
## Releated Documents
- [Git - Branch Flow Guide](README-GIT-FLOW.md)
- [Git - Pull Requests Flow Guide](README-GIT-PR)
- [Git - Handling Dependabot Branches](README-GIT-DEPENDABOT.md)
