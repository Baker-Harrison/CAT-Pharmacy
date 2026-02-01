# Contributing to CAT Pharmacy

Thank you for your interest in contributing to the CAT Pharmacy adaptive learning system!

## Development Setup

### Prerequisites

- **.NET 9 SDK** - Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
- **Windows OS** - Required for WPF application
- **Visual Studio 2022** or **Visual Studio Code** with C# Dev Kit
- **Git** - For version control

### Initial Setup

1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd "CAT Pharmacy"
   ```

2. Verify SDK version (managed by `global.json`):
   ```bash
   dotnet --version
   # Should match version in global.json
   ```

3. Restore dependencies:
   ```bash
   dotnet restore src/CatAdaptive.sln
   ```

4. Build the solution:
   ```bash
   dotnet build src/CatAdaptive.sln
   ```

5. Run tests:
   ```bash
   dotnet test src/CatAdaptive.sln
   ```

6. Run the application:
   ```bash
   dotnet run --project src/CatAdaptive.App/CatAdaptive.App.csproj
   ```

## Project Structure

```
src/
├── CatAdaptive.App/                 # WPF UI layer
├── CatAdaptive.Application/         # Use cases and orchestration
├── CatAdaptive.Domain/              # Domain models and aggregates
├── CatAdaptive.Infrastructure/      # Persistence, parsing, generators
├── CatAdaptive.Domain.Tests/        # Domain unit tests
├── CatAdaptive.Application.Tests/   # Application service tests
├── CatAdaptive.Infrastructure.Tests/# Infrastructure tests
└── CatAdaptive.sln
```

## Coding Standards

### C# Style

- Use **file-scoped namespaces** (`namespace Foo.Bar;`)
- Prefer `sealed` classes unless inheritance is required
- Use `record` for immutable data carriers
- Follow **PascalCase** for types, methods, properties
- Follow **camelCase** for locals and parameters
- Use **_camelCase** for private fields

### Formatting

The project uses `.editorconfig` for consistent formatting. Run formatting before committing:

```bash
dotnet format src/CatAdaptive.sln
```

### Nullability

All projects have nullable reference types enabled. Always:
- Use `string?` for nullable strings
- Check for null early and throw `InvalidOperationException` for invalid state
- Use `!` operator only when absolutely certain

## Testing

### Running Tests

```bash
# Run all tests
dotnet test src/CatAdaptive.sln

# Run with verbose output
dotnet test src/CatAdaptive.sln --verbosity normal

# Run specific project
dotnet test src/CatAdaptive.Domain.Tests

# Run specific test method
dotnet test src/CatAdaptive.sln --filter "FullyQualifiedName~KnowledgeUnitTests"
```

### Writing Tests

- Use **xUnit** for testing framework
- Use **FluentAssertions** for assertions
- Use **Moq** for mocking in Application/Infrastructure tests
- Follow the Arrange-Act-Assert pattern
- Name tests descriptively: `MethodName_StateUnderTest_ExpectedBehavior`

Example:
```csharp
[Fact]
public void Create_WithEmptyTopic_UsesDefaultValue()
{
    // Arrange
    var keyPoints = new[] { "Point 1" };

    // Act
    var unit = KnowledgeUnit.Create("", "", "slide-1", "", keyPoints);

    // Assert
    unit.Topic.Should().Be("General");
}
```

## Architecture Guidelines

### Dependency Direction

```
App → Application → Domain
  ↘ Infrastructure ↗
```

- **Domain** has no dependencies
- **Application** depends only on Domain
- **Infrastructure** depends on Application and Domain
- **App** references all layers but doesn't expose internals

### Adding New Features

1. Start with domain models in `CatAdaptive.Domain`
2. Define abstractions in `CatAdaptive.Application/Abstractions`
3. Implement services in `CatAdaptive.Application/Services`
4. Implement repositories in `CatAdaptive.Infrastructure/Repositories`
5. Register services in `ServiceCollectionExtensions.cs`
6. Create ViewModels in `CatAdaptive.App/ViewModels`
7. Add unit tests

## Environment Configuration

### Local Development

Create a `.env` file in the repository root:

```env
GEMINI_API_KEY=your_api_key_here
```

**Never commit this file!** It's already in `.gitignore`.

### appsettings.json

Default configuration is in `src/CatAdaptive.App/appsettings.json`:

```json
{
  "Gemini": {
    "UseGemini": true,
    "ApiKey": "",
    "ModelName": "gemini-3-flash-preview"
  }
}
```

Environment variable `GEMINI_API_KEY` overrides the config value.

## Build & CI

The project uses GitHub Actions for CI. The workflow:
1. Restores NuGet packages
2. Checks code formatting
3. Builds the solution
4. Runs all tests
5. Uploads build artifacts

## Commit Guidelines

Use conventional commit messages:

- `feat: add new lesson generation feature`
- `fix: resolve null reference in repository`
- `test: add unit tests for KnowledgeUnit`
- `docs: update architecture documentation`
- `refactor: simplify error handling`

## Data Storage

Local data is stored at:
```
%LOCALAPPDATA%\CatAdaptive\data\
```

Files:
- `lesson-plans.json`
- `content-graph.json`
- `knowledge-graph-<learnerId>.json`
- `knowledge-units.json`
- `item-bank.json`

## Getting Help

- Check existing documentation in `/docs`
- Review `AGENTS.md` for detailed coding conventions
- Open an issue for bugs or feature requests

## License

[Add your license information here]
