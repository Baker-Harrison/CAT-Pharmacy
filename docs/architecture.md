# Adaptive CAT-Based Study System Architecture

## Overview

The system is a modular C# WPF desktop application. The default user flow is **Upload → Lessons → Embedded Quiz → Next Lessons**, with CAT retained as a separate assessment view.

```text
Solution
├── src
│   ├── CatAdaptive.sln
│   ├── CatAdaptive.App           (WPF desktop UI)
│   ├── CatAdaptive.Domain        (Domain models + graphs)
│   ├── CatAdaptive.Application   (Learning flow + CAT services)
│   └── CatAdaptive.Infrastructure (PPTX ingestion, persistence, Gemini)
└── docs
    └── architecture.md
```

## Core Modules

### 1. Presentation Layer (CatAdaptive.App)

- Three views: **Upload Content**, **Lessons**, **CAT**.
- Lessons view contains a list page and a lesson detail page with embedded quiz.
- ViewModel-to-View wiring lives in `src/CatAdaptive.App/MainWindow.xaml` DataTemplates.
- Lessons list shows progress + quiz score, and generation status when new lessons are created.

### 2. Application Layer (CatAdaptive.Application)

- `LearningFlowService` orchestrates the default learning flow.
- `AdaptiveTestService` handles CAT (untouched).
- Interfaces for lesson generation and evaluation:
  - `ILessonPlanGenerator`
  - `ILessonQuizEvaluator`
  - `ILessonPlanRepository`

### 3. Domain Layer (CatAdaptive.Domain)

Key domain models and aggregates:

- `ContentGraph` (content supply from PPTX)
- `KnowledgeGraph` (learner mastery state)
- `LessonPlan` (long-form lesson + embedded quiz)
- `EvidenceRecord` (updates KnowledgeGraph after quiz)
- `AdaptiveSession` (CAT)

### 4. Infrastructure Layer (CatAdaptive.Infrastructure)

- `PptxParser` extracts knowledge units from slides.
- `Json*Repository` implementations persist lessons, graphs, and items.
- `GeminiLessonPlanGenerator` + `GeminiLessonQuizEvaluator` implement AI lesson creation and scoring.

## Default Learning Flow (Active Learning)

1. **Upload PPTX** → parse into knowledge units.
2. **Content graph rebuilt** + knowledge graph reset for the default learner.
3. **Gemini initial lessons** choose the simplest concepts when KG is empty.
4. **Lesson delivery** (15–20 minute reads) with embedded active-learning prompts.
5. **Embedded quiz** (fill-in-the-blank + open response).
6. **Scoring** via Gemini rubric evaluation.
7. **Next step**
   - Score < 80% → remediation lesson.
   - Score ≥ 80% → next lesson batch based on graphs.

## CAT Flow

- CAT remains a separate view.
- Uses `AdaptiveTestService`, item repositories, and IRT logic for ability estimation.

## Persistence Strategy

- Lessons stored as JSON under `%LOCALAPPDATA%\CatAdaptive\data\lesson-plans.json`.
- Content graph stored as JSON under `%LOCALAPPDATA%\CatAdaptive\data\content-graph.json`.
- Knowledge graph stored as JSON per learner.
- Items and knowledge units stored as JSON for CAT.
