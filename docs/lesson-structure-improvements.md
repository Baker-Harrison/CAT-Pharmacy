# Lesson Structure Review - Section-Level Progress Tracking

## Executive Summary

After reviewing the CAT Pharmacy lesson structure, I've identified that section-level progress tracking would be a valuable improvement to enhance the learning experience by allowing users to track their reading progress within lessons, not just overall completion.

## Recommended Improvement: Section-Level Progress Tracking

### Current State

`ProgressPercent` in `LessonPlan` is binary (0% or 100%), with no tracking of individual section completion.

### Improvement

Implement granular progress tracking that:

- Marks sections as "read" when user scrolls through them
- Persists reading position within lessons
- Allows users to resume where they left off
- Updates `ProgressPercent` based on sections read, not just quiz completion

### Implementation Impact

Medium - Requires adding `SectionProgress` to the domain model and implementing scroll tracking in the UI.

## Technical Implementation

### Domain Model Changes

Add to `LessonPlan`:

```csharp
public sealed record SectionProgress(
    Guid SectionId,
    bool IsRead,
    double ReadPercent,
    DateTimeOffset? LastReadAt);

public sealed record LessonPlan(
    // ... existing fields
    IReadOnlyList<SectionProgress> SectionProgress, // New field
    // ... rest of existing fields
);
```

### UI Changes

In `LessonsView.xaml`:

- Add scroll detection for each section
- Visual indicators for read/unread sections
- Progress bar showing actual reading progress

In `LessonsViewModel.cs`:

- Track scroll position
- Update section progress as user reads
- Persist progress to repository

### Service Layer

Update `ILessonPlanRepository` to support:

- Saving section progress
- Loading lesson with progress
- Querying partially read lessons

## Benefits

1. **Better User Experience** - Users can see exactly where they left off
2. **Accurate Progress Tracking** - Reflects actual engagement, not just quiz completion
3. **Resume Capability** - Seamless continuation of learning sessions
4. **Analytics Insights** - Understanding which sections are most challenging

## Implementation Priority

High - This improvement directly addresses user experience and provides immediate value with minimal disruption to existing functionality.

## Conclusion

Section-level progress tracking builds upon the existing architecture while providing significant value to learners. The modular design of the CAT Pharmacy project makes this enhancement achievable with focused changes to the domain model, UI, and persistence layers.
