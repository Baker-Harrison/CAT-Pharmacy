# LessonsView Styling

## Overview

`LessonsView.xaml` defines its own resource dictionary for colors, typography, and component styles. Keep changes localized to avoid unintended styling side effects across the app.

## Key Color Brushes

- `PrimaryTextBrush` `#1E293B`
- `SecondaryTextBrush` `#64748B`
- `PrimaryBlueBrush` `#2563EB`
- `PrimaryBlueHoverBrush` `#1D4ED8`
- `SurfaceBrush` `White`
- `BackgroundBrush` `#F8FAFC`
- `BorderBrush` `#E2E8F0`
- `SuccessBrush` `#10B981`
- `WarningBackgroundBrush` `#FEFCE8`
- `WarningBorderBrush` `#FDE68A`
- `WarningTextBrush` `#92400E`
- `ErrorTextBrush` `#DC2626`

## Key Styles

- `HeaderTextStyle`
- `SubHeaderTextStyle`
- `SectionHeaderStyle`
- `CardStyle`
- `PrimaryButtonStyle`
- `OutlineButtonStyle`
- `ModernProgressBar`
- `InputTextBoxStyle`
- `SectionContainerStyle`
- `SectionHeaderContainerStyle`
- `SectionProgressIndicatorStyle`
- `ViewTransitionStyle`

## Guidelines

- Prefer updating existing brushes over introducing new one-off colors.
- Keep typography sizes consistent with the header/subheader hierarchy.
- When adjusting layout spacing, validate both list and detail views.
