# TODO: Fix JSON Extraction Logic

## Implementation
- [x] Refactor `ExtractJson` in `GeminiLessonPlanGenerator.cs` [backend]
  - [x] Implement bracket-counting logic to find the first complete balanced array `[...]`.
  - [x] Ensure it skips potential brackets inside string literals (optional but safer).
  - [x] Update the method to return only the correctly balanced substring.

## Verification
- [ ] Build the application to ensure no syntax errors [test]
- [ ] Run the application and trigger a file process [test]
- [ ] Verify `gemini_debug.log` shows successful parsing without "trailing data" errors [analysis]
