# Adaptive CAT-Based Study System

A C# WPF desktop application that enables learners to upload lecture slide decks (PPTX) and receive personalized adaptive testing using Computerized Adaptive Testing (CAT) with Item Response Theory (IRT).

## Features

- **PPTX Upload & Parsing**: Extract knowledge units from PowerPoint slides
- **Active Learning Lessons**: Gemini-generated lessons (15–20 minute reads) with embedded prompts
- **Embedded Post-Quizzes**: Fill-in-the-blank + open-response quizzes with 80% mastery gating and remediation
- **Adaptive Testing Engine (CAT)**: Uses IRT (3PL model) to select optimal questions
- **Real-time Ability Estimation**: Updates learner ability (θ) after each response

## Requirements

- .NET 9.0 SDK
- Windows OS (WPF application)
- Google Gemini API Key (optional, for AI-powered item generation)

## Getting Started

### Build

```bash
cd src
dotnet build CatAdaptive.sln
```

### Run

```bash
dotnet run --project CatAdaptive.App/CatAdaptive.App.csproj
```

Or open `src/CatAdaptive.sln` in Visual Studio and press F5.

## Project Structure

```text
src/
├── CatAdaptive.sln
├── CatAdaptive.App/           # WPF desktop UI (MVVM)
│   ├── Views/                 # XAML views (Upload, Lessons, CAT)
│   ├── ViewModels/            # View models
│   └── Converters/            # Value converters
├── CatAdaptive.Application/   # Application services & abstractions
│   ├── Abstractions/          # Repository interfaces
│   └── Services/              # Learning flow + CAT services
├── CatAdaptive.Domain/        # Domain models & aggregates
│   ├── Models/                # Lesson plans, items, evidence
│   └── Aggregates/            # ContentGraph/KnowledgeGraph/AdaptiveSession
└── CatAdaptive.Infrastructure/ # Infrastructure implementations
    ├── Parsing/               # PPTX parser
    ├── Repositories/          # JSON-based persistence
    └── Generation/            # Gemini generators
```

## Key Files

- `src/CatAdaptive.App/App.xaml.cs`: DI registration and application startup wiring.
- `src/CatAdaptive.App/MainWindow.xaml`: DataTemplates that map ViewModels to Views.
- `src/CatAdaptive.App/ViewModels/MainViewModel.cs`: main navigation state (`CurrentView`).
- `src/CatAdaptive.App/ViewModels/LessonsViewModel.cs`: lessons list/detail + quiz submission.
- `src/CatAdaptive.Application/Services/LearningFlowService.cs`: default upload → lesson → quiz flow.
- `src/CatAdaptive.Infrastructure/Repositories`: JSON persistence for lessons, items, graphs.
- `src/CatAdaptive.Domain/Models/LessonPlan.cs`: lesson + quiz domain model.

## UI Composition

Main view navigation is driven by `MainViewModel.CurrentView` and the DataTemplates in `src/CatAdaptive.App/MainWindow.xaml`, which map each ViewModel type to its corresponding View. When adding a new screen, register the ViewModel with DI and add a matching `<DataTemplate DataType="{x:Type vm:NewViewModel}">` entry that instantiates the View.

## Default Learning Flow

1. **Upload PPTX** → content graph rebuilt and knowledge graph reset.
2. **Initial lessons** → Gemini selects simplest concepts and generates 15–20 minute lessons with active-learning prompts.
3. **Embedded quiz** → fill-in-the-blank + open response questions, scored with Gemini.
4. **Next steps** → scores below 80% trigger remediation lessons; 80%+ triggers next lessons.

## Build Outputs and Ignore Patterns

Build outputs land in `src/**/bin` and `src/**/obj`, and WPF design-time builds can create `*wpftmp*` artifacts (for example `CatAdaptive.App_*_wpftmp.csproj`) under `src/CatAdaptive.App/obj`. If you initialize git for this repo, consider ignoring:

- `**/bin/`
- `**/obj/`
- `.vs/`
- `*.user`
- `*.suo`

Use `dotnet clean src/CatAdaptive.sln` to remove build artifacts locally.

## Configuration

### Enabling Gemini AI (Optional)

To use Google Gemini for enhanced item generation:

1. **Get API Key**: Visit [Google AI Studio](https://aistudio.google.com/apikey) to create a free API key
2. **Configure the App**: Edit `src/CatAdaptive.App/appsettings.json`:

```json
{
  "Gemini": {
    "ApiKey": "YOUR_API_KEY_HERE",
    "ModelName": "gemini-2.0-flash-exp",
    "UseGemini": true
  }
}
```

Alternatively, set the `GEMINI_API_KEY` environment variable.

**Note**: If `UseGemini` is `false` or no API key is provided, the app uses a simple rule-based item generator.

## How to Use

1. **Upload Content**: Navigate to "Upload Content" and select a PPTX file.
2. **Open Lessons**: Go to "Lessons" to view generated lessons and progress.
3. **Complete Quiz**: Finish the embedded quiz at the end of each lesson.
4. **Review Progress**: Return to the lessons list to see scores and new lessons.
5. **Run CAT**: Use the separate CAT view for adaptive testing.

## CAT Algorithm

The adaptive testing engine implements:

- **Item Selection**: Maximizes Fisher Information at current ability estimate
- **Ability Estimation**: Maximum Likelihood Estimation (MLE) with Newton-Raphson
- **IRT Model**: 3-Parameter Logistic (3PL) model with discrimination (a), difficulty (b), and guessing (c) parameters
- **Termination Criteria**: Standard error threshold, maximum items, or mastery level

## Data Storage

Data is stored locally in JSON format at:
- Windows: `%LOCALAPPDATA%\CatAdaptive\data\`

## License

MIT License
