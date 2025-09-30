# POIneer.Render – Tests

Dieses Verzeichnis enthält alle automatisierten Tests für **POIneer.Render**.  
Die Tests sind nach Typ getrennt, um klare Verantwortlichkeiten zu haben.

## Struktur

```text
tests/
 ├─ POIneer.Render.UnitTests/        # klassische Unit-Tests (kleine, schnelle Tests ohne externe Abhängigkeiten)
 ├─ POIneer.Render.IntegrationTests/ # Integrationstests (Datenbanken, Filesystem, externe Prozesse)
 ├─ POIneer.Render.ContractTests/    # Schnittstellen- und Vertrags-Tests (z. B. API-Schema, Migrationen, DB-Verträge)
 └─ POIneer.Render.TestHelpers/      # gemeinsame Hilfsklassen für Tests (TempDir, ListLogger, Fixtures, Mocks)
 ```

## Hinweise

- **UnitTests**  
  Prüfen die Kernlogik isoliert. Schnell, deterministisch, laufen in CI/CD bei jedem Commit.

- **IntegrationTests**  
  Validieren Zusammenspiel mit Infrastruktur (z. B. SQLite, Dateisystem). Können länger laufen, aber sind wichtig für die Release-Qualität.

- **ContractTests**  
  Stellen sicher, dass Verträge eingehalten werden (z. B. Datenbank-Schema, APIs). Dienen auch als „Safety Net“ bei Refactorings.

- **TestHelpers**  
  Enthält nur Hilfsklassen (z. B. `TempDir`, `ListLogger<T>`).  
  - Keine eigenen Tests  
  - Referenziert von allen Testprojekten  
  - Wird in der Code-Coverage ausgeschlossen

## Test-Ausführung

Alle Tests laufen zusammen über die Solution:

```bash
dotnet test POIneerRender.sln

In der CI/CD-Pipeline werden zusätzlich Coverage-Berichte erzeugt (Cobertura), die in Jenkins ausgewertet werden.

ℹ️ Tipp: Neue Tests bitte immer im passenden Projekt anlegen.
Wenn ein Test nicht eindeutig zuordenbar ist, UnitTests bevorzugen.

