# CAT-Pharmacy (Adaptive CAT-Based Study System)

A WPF desktop application for adaptive learning. Users ingest PPTX content, receive generated lessons with embedded quizzes, and can run a separate CAT (computerized adaptive testing) flow.

## Requirements

- Windows (WPF)
- .NET 9 SDK
- Gemini API key for lesson generation and quiz evaluation

## Quick start

```bash
dotnet restore src/CatAdaptive.sln
dotnet build src/CatAdaptive.sln
dotnet run --project src/CatAdaptive.App/CatAdaptive.App.csproj
```

## Configuration

`src/CatAdaptive.App/appsettings.json`:

- `Gemini:UseGemini` toggles item generation (CAT items).
- `Gemini:ApiKey` can be set here or via the `GEMINI_API_KEY` env var.
- `Gemini:ModelName` selects the model used by Gemini-backed services.

Note: Lesson plan generation and quiz evaluation are currently Gemini-backed regardless of `UseGemini`.

## Data storage

Local JSON data is stored under `%LOCALAPPDATA%\CatAdaptive\data\`:

- `lesson-plans.json`
- `content-graph.json`
- `knowledge-graph-<learnerId>.json`
- `knowledge-units.json`
- `item-bank.json`

## Architecture

- App (WPF UI) → Application services → Domain models → Infrastructure (parsing, persistence, AI)
- Default learning flow: Upload → Lessons → Embedded Quiz → Next Lessons
- CAT flow is separate and uses item repositories and IRT-based estimation

## More docs

See `docs/README.md` for detailed documentation.
-e 
```
   +-------+
  /       /|
 /       / |
+-------+  |
|       |  +
|       | /
|       |/
+-------+
```
