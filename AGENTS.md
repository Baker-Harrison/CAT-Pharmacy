# AGENTS.md

## Scope
- Repository: Adaptive CAT-Based Study System (C# WPF)
- Primary solution: `src/CatAdaptive.sln`
- Target frameworks: `net9.0` and `net9.0-windows`
- Nullable reference types: enabled in all projects
- Implicit global usings: enabled

## Quick Start Commands
- Restore: `dotnet restore src/CatAdaptive.sln`
- Build: `dotnet build src/CatAdaptive.sln`
- Run app: `dotnet run --project src/CatAdaptive.App/CatAdaptive.App.csproj`
- Clean: `dotnet clean src/CatAdaptive.sln`
- Open in Visual Studio: `src/CatAdaptive.sln`
- Run in Visual Studio: F5 / Debug > Start
- Runtime requirement: Windows OS (WPF)

## Tests
- Test projects: none detected in `src/` currently
- Standard test command (when tests exist): `dotnet test src/CatAdaptive.sln`
- Run a single test by name: `dotnet test src/CatAdaptive.sln --filter "FullyQualifiedName~Namespace.Class.Method"`
- Run a single test by category: `dotnet test src/CatAdaptive.sln --filter "TestCategory=Unit"`
- Run a single test project: `dotnet test path/to/Project.Tests.csproj`

## Linting / Formatting
- No explicit lint configuration detected (`.editorconfig` not found)
- Use Visual Studio/VS Code formatting defaults for C# and XAML
- Optional (if available): `dotnet format src/CatAdaptive.sln`
- Keep formatting consistent with existing files (spacing, line breaks)

## Project Layout
- `src/CatAdaptive.App`: WPF UI (MVVM, Views, ViewModels, Converters)
- `src/CatAdaptive.Application`: application services and abstractions
- `src/CatAdaptive.Domain`: domain models and aggregates
- `src/CatAdaptive.Infrastructure`: I/O, persistence, parsing, generators
- `docs/`: supplemental documentation

## Architecture Rules
- Domain contains pure models; no UI or infrastructure dependencies
- Application defines interfaces and orchestrates use cases
- Infrastructure implements repositories, parsers, generators
- App references Application/Infrastructure but not vice versa
- Prefer dependency injection for service wiring

## Dependency Injection
- Register services in the App startup/bootstrapping layer
- Use constructor injection for ViewModels and services
- Keep service lifetimes explicit (singleton/scoped/transient)
- Avoid Service Locator patterns

## C# Style Conventions
- Use file-scoped namespaces (`namespace Foo.Bar;`)
- Prefer `sealed` for services and records unless inheritance required
- Favor `record` for immutable data carriers and value objects
- Keep public APIs explicit and small; internal helpers private
- Use nullable annotations intentionally (`string?`, `object?`)
- Prefer early returns; avoid deep nesting
- Use braces for multi-line conditionals and loops

## Clean Code / DRY
- Remove duplication by extracting helpers or shared defaults
- Keep ViewModel navigation/state changes centralized
- Avoid commented-out code unless actively debugging
- Prefer clear method names over inline comments

## Imports and Usings
- Keep `using` list minimal; rely on implicit usings
- Order `System.*` usings first, then external, then internal
- Group external, internal, and framework namespaces logically
- Avoid unused usings; let tooling clean them up
- Avoid aliases unless resolving conflicts

## Naming Conventions
- PascalCase for types, methods, properties, events
- camelCase for locals and parameters
- `_camelCase` for private fields
- Use clear domain names (`AbilityEstimate`, `TerminationCriteria`)
- Avoid single-letter names except in short lambdas

## Types and Nullability
- Enable nullable reference types in new projects
- Use `?` for optional values and check for nulls early
- Prefer `IReadOnlyList<T>` / `IReadOnlyDictionary<TKey,TValue>` for read-only APIs
- Favor `var` when the type is obvious from the RHS
- Use explicit types for public API clarity

## Async and Tasks
- Prefer async APIs for I/O and repository operations
- Pass `CancellationToken` through call chains
- Use `Task`/`Task<T>`; avoid `async void` except event handlers
- Await repository saves after mutations

## Error Handling
- Throw `InvalidOperationException` for invalid state or missing data
- Validate inputs at boundaries and fail fast
- Avoid empty catch blocks; log or rethrow with context
- Keep exception messages user-meaningful in UI paths

## MVVM / WPF Guidelines
- Use `CommunityToolkit.Mvvm` attributes (`[ObservableProperty]`, `[RelayCommand]`)
- Keep UI logic in ViewModels; keep Views thin
- Use code-behind only for UI-specific concerns
- Use `ObservableCollection<T>` for bindable lists
- Keep view model constructors DI-friendly

## Domain Modeling
- Keep aggregates cohesive (`AdaptiveSession`, `KnowledgeGraph`)
- Use methods on aggregates to enforce invariants
- Favor immutable value objects where possible
- Keep enums in dedicated files (`ContentEnums`, `KnowledgeEnums`)

## Repositories and Persistence
- Repositories live in `Infrastructure/Repositories`
- Use async JSON persistence helpers where available
- Keep file paths/config in Infrastructure, not Domain
- Ensure data changes are persisted after writes

## Parsing and Generation
- `Infrastructure/Parsing` handles PPTX ingestion
- AI generator uses Gemini if configured
- Gracefully fall back to rule-based generator when `UseGemini=false`

## Configuration
- App config: `src/CatAdaptive.App/appsettings.json`
- Env var: `GEMINI_API_KEY` overrides config
- Keep secrets out of source control

## Data Storage
- Local data path: `%LOCALAPPDATA%\CatAdaptive\data\`
- Store generated JSON through Infrastructure repositories
- Avoid persisting UI state in Domain models

## XAML Guidelines
- Keep XAML files focused; prefer small, composable views
- Use converters in `Converters/` for binding transforms
- Prefer binding over code-behind for data flow

## Testing Guidance (Future)
- Mirror project structure for tests (`CatAdaptive.Domain.Tests`, etc.)
- Keep tests deterministic and independent
- Use `dotnet test --filter` for targeted runs

## Tools and Rules
- Cursor rules: none found (`.cursor/rules/`, `.cursorrules` missing)
- Copilot instructions: none found (`.github/copilot-instructions.md` missing)

## Agent Tips
- Stay within existing architecture boundaries
- Do not add new dependencies without clear need
- Avoid large refactors unless requested
- Keep changes minimal and localized
- Update documentation only when it changes behavior
