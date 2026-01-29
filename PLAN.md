# Plan: Fix JSON Extraction Logic

## Problem Analysis
The `gemini_debug.log` shows that `GeminiLessonPlanGenerator` fails to deserialize the JSON response because the LLM appends "chatty" text (e.g., "Multiplexing: ...") *after* the valid JSON array. The current `ExtractJson` method uses `LastIndexOf(']')`, which erroneously captures this trailing text if the text itself contains or ends with a bracket `]`.

## Goal
Make the `ExtractJson` method in `GeminiLessonPlanGenerator.cs` robust against trailing text by implementing a bracket-counting mechanism. It should identify the exact closing bracket that matches the initial opening bracket.

## Step-by-Step Plan

### Step 1: Update `ExtractJson` Logic
*   **File**: `src/CatAdaptive.Infrastructure/Generation/GeminiLessonPlanGenerator.cs`
*   **Action**: Replace the substring/index logic with a loop that:
    1.  Finds the first `[` (start index).
    2.  Iterates from `start`, maintaining a `balance` counter.
    3.  Increments `balance` on `[` and decrements on `]`.
    4.  When `balance` returns to zero, that is the correct `end` index.
    5.  Returns the substring from `start` to `end`.

### Step 2: Verification
*   **Build**: Ensure the code compiles.
*   **Run**: Run the app and process a file.
*   **Verify**: Check `gemini_debug.log` or the UI to confirm that lessons are now generated even if the LLM includes trailing text.

## Questions for User
*   None. The research confirmed the issue and the solution is standard.