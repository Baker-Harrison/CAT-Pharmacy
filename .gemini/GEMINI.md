# GEMINI.md

## Project Overview

This is a C# WPF desktop application that enables learners to upload lecture slide decks (PPTX) and receive personalized adaptive testing using Computerized Adaptive Testing (CAT) with Item Response Theory (IRT). The application uses the MVVM pattern and is divided into four main projects: `App`, `Application`, `Domain`, and `Infrastructure`.

The core functionality includes:
- **PPTX Upload & Parsing**: Extracts knowledge units from PowerPoint slides.
- **Active Learning Lessons**: Gemini-generated lessons with embedded prompts.
- **Embedded Post-Quizzes**: Fill-in-the-blank + open-response quizzes with mastery gating and remediation.
- **Adaptive Testing Engine (CAT)**: Uses IRT (3PL model) to select optimal questions.
- **Real-time Ability Estimation**: Updates learner ability after each response.

## Building and Running

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

## Development Conventions

### Architecture
The project follows a clean architecture pattern, with the following layers:
- **`CatAdaptive.Domain`**: Contains the core domain models and business logic.
- **`CatAdaptive.Application`**: Contains the application logic, services, and interfaces.
- **`CatAdaptive.Infrastructure`**: Contains the implementation of the interfaces defined in the `Application` layer, such as repositories and external services.
- **`CatAdaptive.App`**: The WPF presentation layer, which depends on the other three layers.

### MVVM
The `CatAdaptive.App` project uses the Model-View-ViewModel (MVVM) pattern.
- **Views**: XAML files in the `Views` folder.
- **ViewModels**: C# files in the `ViewModels` folder, using the `CommunityToolkit.Mvvm` library.
- **Models**: The domain models from the `CatAdaptive.Domain` project.

### Dependency Injection
The application uses the `Microsoft.Extensions.DependencyInjection` library for dependency injection. Services are registered in `src/CatAdaptive.App/App.xaml.cs`.

### Testing
There are currently no test projects in the solution. If tests are added, they should follow the standard `.NET` testing conventions.

### Gemini API
The application can use the Gemini API for generating questions and lesson plans. To enable this, set the `UseGemini` property to `true` in `src/CatAdaptive.App/appsettings.json` and provide an API key.

```json
{
  "Gemini": {
    "UseGemini": true,
    "ApiKey": "YOUR_API_KEY_HERE",
    "ModelName": "gemini-2.0-flash-exp"
  }
}
```

If `UseGemini` is `false` or no API key is provided, the app uses a simple rule-based item generator.
