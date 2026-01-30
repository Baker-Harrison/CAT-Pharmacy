# WPF Converters

## Overview

Converters live in `src/CatAdaptive.App/Converters/` and are registered in `App.xaml` for XAML bindings.

## Available Converters

### BoolConverters.cs

- `BoolToVisibilityConverter`
  - `true` → `Visible`, `false` → `Collapsed`
- `InverseBoolToVisibilityConverter`
  - `true` → `Collapsed`, `false` → `Visible`
- `InverseBoolConverter`
  - `true` → `false`, `false` → `true`
- `NullToVisibilityConverter`
  - not null → `Visible`, null → `Collapsed`
- `NullToCollapsedConverter`
  - null → `Visible`, not null → `Collapsed`
- `ZeroToVisibilityConverter`
  - value > 0 → `Visible`, else `Collapsed`

### ZeroToCollapsedConverter.cs

- `ZeroToCollapsedConverter`
  - value == 0 → `Visible`, else `Collapsed`

### BoolToColorConverter.cs

- `BoolToColorConverter`
  - `true` → green (`#10B981`)
  - `false` → gray (`#64748B`)

## Registration

Converters are declared as application resources in `App.xaml`.
