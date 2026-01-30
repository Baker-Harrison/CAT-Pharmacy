# Adaptive CAT-Based Study System Architecture

## Overview

This is a modular C# WPF desktop application. The primary user flow is **Upload → Lessons → Embedded Quiz → Next Lessons**, with CAT retained as a separate assessment view.

```
Solution
├── src
│   ├── CatAdaptive.sln
│   ├── CatAdaptive.App            (WPF desktop UI)
│   ├── CatAdaptive.Domain         (Domain models + aggregates)
│   ├── CatAdaptive.Application    (Use cases + orchestration)
│   └── CatAdaptive.Infrastructure (Parsing, persistence, generators)
└── docs
```

## Layers

### Presentation (CatAdaptive.App)
- Views: Upload, Lessons, Adaptive Session (CAT), Debug
- ViewModels wired via `MainWindow.xaml` DataTemplates
- Section-level progress tracking for lesson reading (scroll-based)

### Application (CatAdaptive.Application)
- `ContentIngestionService` parses PPTX, builds graphs, generates initial lessons
- `AssessmentService` evaluates quizzes, updates the knowledge graph, and generates remediation or next lessons
- `LearningFlowService` is the thin orchestration layer used by the UI

### Domain (CatAdaptive.Domain)
- Aggregates: `ContentGraph`, `KnowledgeGraph`, `AdaptiveSession`
- Models: `LessonPlan`, `LessonQuiz`, `EvidenceRecord`, `AbilityEstimate`, etc.
- Lesson progress is captured in `SectionProgress` and rolled into `LessonPlan.ProgressPercent`

### Infrastructure (CatAdaptive.Infrastructure)
- Parsers: `PptxParser`
- Generators: Gemini-backed lesson/quiz generation and evaluation
- Repositories: JSON persistence under `%LOCALAPPDATA%\CatAdaptive\data\`

## Default Learning Flow

1. **Upload PPTX** → knowledge units extracted.
2. **Item generation** → CAT items produced (Gemini or simple generator).
3. **Graphs** → content graph created; knowledge graph ensured for default learner.
4. **Initial lessons** generated from the content graph.
5. **Lesson delivery** with embedded quiz.
6. **Quiz evaluation** → lesson progress updated, knowledge graph updated.
7. **Next step**
   - Score < 80% → remediation lesson generated.
   - Score ≥ 80% → next lessons generated.

## CAT Flow

- Separate view using `AdaptiveTestService` and item repositories.
- IRT-based ability estimation and adaptive item selection.

## Data Storage

Stored as JSON under `%LOCALAPPDATA%\CatAdaptive\data\`:

- `lesson-plans.json`
- `content-graph.json`
- `knowledge-graph-<learnerId>.json`
- `knowledge-units.json`
- `item-bank.json`

## Configuration

`appsettings.json` provides Gemini settings. `GEMINI_API_KEY` overrides the config value. Lesson plan generation and quiz evaluation are Gemini-backed in the current implementation.
