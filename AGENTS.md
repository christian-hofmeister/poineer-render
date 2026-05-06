# AGENTS.md — POIneer

## Project Goal

POIneer is a .NET-based MVP for rendering OpenStreetMap POI data into downloadable SQLite region databases.

Current MVP focus:

- Render Berlin as the first hardcoded region.
- Import OSM nodes with relevant POI tags.
- Store rendered POIs in SQLite.
- Keep the solution simple, testable, and suitable for later CI/CD and Azure/VPS deployment.

Out of scope for the current MVP:

- Ways and relations.
- Full admin UI.
- Mobile app implementation.
- Azure production deployment.
- Complex multi-region orchestration.

## Tech Stack

- .NET 10
- C#
- SQLite
- Flyway migrations
- xUnit
- FluentAssertions
- Jenkins
- Linux/VPS-compatible tooling
- Windows development support where practical

## Repository Guidelines

Prefer small, focused changes.

Before changing code:

1. Inspect existing structure and naming.
2. Follow current conventions.
3. Avoid large architectural rewrites unless explicitly requested.
4. Keep MVP scope in mind.

Do not introduce new frameworks or heavy dependencies without a clear reason.

## Coding Style

- Use file-scoped namespaces.
- Prefer clear, explicit names over abbreviations.
- Prefer dependency injection over static service access.
- Keep domain/application logic independent from infrastructure details.
- Keep comments in English.
- Keep Markdown documentation in English.
- User-facing explanations may be German, but repository content should be English.
- Avoid clever code when simple code is sufficient.

## Architecture Principles

Use a pragmatic Clean Architecture style:

- Domain should not depend on infrastructure.
- Infrastructure may depend on application/domain abstractions.
- CLI/composition root wires dependencies together.
- Configuration should use strongly typed options.
- File system, process execution, Flyway, and external tools should be behind abstractions where useful.

Prefer explicit boundaries:

- Rendering orchestration
- OSM input/parsing
- SQLite persistence
- Flyway migration execution
- Temporary filesystem handling

## Testing Guidelines

Use xUnit and FluentAssertions.

Tests should:

- Be deterministic.
- Use isolated temporary directories.
- Avoid relying on developer machine state.
- Prefer integration tests for Flyway/SQLite behavior.
- Keep test names descriptive.
- Use Arrange / Act / Assert structure where helpful.

For temporary directories:

- Prefer `ITemporaryDirectoryFactory`.
- Use `NullLogger<T>.Instance` in tests unless real logging output is needed.
- Do not pass loggers through factory methods unless there is a strong reason.

## Temporary Directory Rules

Temporary directories are managed through:

- `TemporaryDirectory`
- `ITemporaryDirectoryFactory`
- `TemporaryDirectoryFactory`
- `TempOptions`

The factory is responsible for:

- Building temp paths.
- Applying configured root folder names.
- Applying `KeepOnDispose`.
- Providing logger dependencies.

Do not duplicate temp path creation logic across tests or production code.

## Flyway / SQLite Rules

Flyway is used to migrate SQLite database files.

When changing Flyway-related code:

- Keep paths robust across Windows, Linux, VS Code, CLI, and Jenkins.
- Do not assume the current working directory unless already established.
- Prefer content-root/repository-root based path resolution.
- Add or update integration tests for path-sensitive behavior.
- Ensure migrations are actually discovered and executed, not only the Flyway schema history table.

## CI / Jenkins Guidelines

Jenkins should remain lightweight for the MVP.

Current CI intent:

- Restore
- Build
- Test
- Avoid heavy OSM rendering in normal CI

Do not add expensive render jobs to Jenkins unless explicitly requested.

Comments in Jenkinsfiles and scripts should be English.

## Git Workflow

Prefer:

- Small commits.
- Clear commit messages.
- Feature branches for larger changes.
- Rebase only when requested or when it keeps local work clean.
- Do not rewrite shared history without explicit instruction.

For Dependabot-style updates:

- Review changes carefully.
- Watch for package version skew.
- Run tests locally when possible.
- Avoid blindly accepting tool version bumps.

## Documentation

When adding or changing architecture-relevant behavior, update docs where useful.

Recommended locations:

- `docs/architecture/`
- `docs/decisions/`
- `docs/workflows/`

Use concise Markdown.

Document decisions when they affect:

- Architecture boundaries
- CI/CD behavior
- Azure/VPS deployment strategy
- Database schema/migration strategy
- Security/secrets handling

## Security and Secrets

Never commit secrets.

Do not place client secrets, connection strings, tenant IDs with secrets, SAS tokens, or production credentials in source files.

Prefer:

- Environment variables
- User secrets for local development
- External secret storage
- Clear documentation of required variables without real values

Use placeholders like:

```text
POINEER_STORAGE_CONNECTION_STRING=<set locally>