# WPF Converters Documentation

## Overview

This document describes the value converters used in the CAT Adaptive Study System's WPF application. Converters are essential components that transform data values for proper display in the UI, enabling clean separation between business logic and presentation.

## Converter Classes

### BoolConverters.cs

Located at `src/CatAdaptive.App/Converters/BoolConverters.cs`, this file contains multiple converter implementations for boolean-based transformations.

#### 1. BoolToVisibilityConverter

```csharp
public class BoolToVisibilityConverter : IValueConverter
```
- **Purpose**: Converts boolean values to Visibility enum
- **True →**: `Visibility.Visible`
- **False →**: `Visibility.Collapsed`
- **Common Usage**: Showing/hiding UI elements based on boolean flags

**Example Usage:**
```xml
<Border Visibility="{Binding IsLoading, Converter={StaticResource BoolToVisibilityConverter}}">
    <!-- Loading content -->
</Border>
```

#### 2. InverseBoolToVisibilityConverter

```csharp
public class InverseBoolToVisibilityConverter : IValueConverter
```
- **Purpose**: Inverts boolean before converting to Visibility
- **True →**: `Visibility.Collapsed`
- **False →**: `Visibility.Visible`
- **Common Usage**: Showing content when a condition is NOT met

**Example Usage:**
```xml
<Grid Visibility="{Binding IsLoading, Converter={StaticResource InverseBoolToVisibilityConverter}}">
    <!-- Content shown when not loading -->
</Grid>
```

#### 3. InverseBoolConverter

```csharp
public class InverseBoolConverter : IValueConverter
```
- **Purpose**: Returns the logical negation of a boolean value
- **True →**: `false`
- **False →**: `true`
- **Common Usage**: Enabling/disabling controls inversely to a state

**Example Usage:**
```xml
<Button IsEnabled="{Binding IsSubmitting, Converter={StaticResource InverseBoolConverter}}">
    Submit
</Button>
```

#### 4. NullToVisibilityConverter

```csharp
public class NullToVisibilityConverter : IValueConverter
```
- **Purpose**: Converts null checks to Visibility
- **Not Null →**: `Visibility.Visible`
- **Null →**: `Visibility.Collapsed`
- **Common Usage**: Showing content only when an object exists

**Example Usage:**
```xml
<TextBlock Text="{Binding SelectedLesson.Title}" 
           Visibility="{Binding SelectedLesson, Converter={StaticResource NullToVisibilityConverter}}" />
```

#### 5. NullToCollapsedConverter

```csharp
public class NullToCollapsedConverter : IValueConverter
```
- **Purpose**: Inverse of NullToVisibilityConverter
- **Null →**: `Visibility.Visible`
- **Not Null →**: `Visibility.Collapsed`
- **Common Usage**: Showing placeholders when content is missing

**Example Usage:**
```xml
<TextBlock Text="No item selected" 
           Visibility="{Binding SelectedItem, Converter={StaticResource NullToCollapsedConverter}}" />
```

#### 6. ZeroToVisibilityConverter

```csharp
public class ZeroToVisibilityConverter : IValueConverter
```
- **Purpose**: Shows/hides based on numeric value
- **Value > 0 →**: `Visibility.Visible`
- **Value ≤ 0 →**: `Visibility.Collapsed`
- **Common Usage**: Showing lists only when they have items

**Example Usage:**
```xml
<ItemsControl ItemsSource="{Binding Items}"
             Visibility="{Binding Items.Count, Converter={StaticResource ZeroToVisibilityConverter}}" />
```

### ZeroToCollapsedConverter.cs

Located at `src/CatAdaptive.App/Converters/ZeroToCollapsedConverter.cs`

#### ZeroToCollapsedConverter

```csharp
public class ZeroToCollapsedConverter : IValueConverter
```
- **Purpose**: Inverse of ZeroToVisibilityConverter
- **Value == 0 →**: `Visibility.Visible`
- **Value ≠ 0 →**: `Visibility.Collapsed`
- **Common Usage**: Showing empty state messages

**Example Usage:**
```xml
<StackPanel Visibility="{Binding Lessons.Count, Converter={StaticResource ZeroToCollapsedConverter}}">
    <TextBlock Text="No lessons available" />
    <Button Content="Refresh" />
</StackPanel>
```

## Implementation Details

### IValueConverter Interface

All converters implement the `IValueConverter` interface:

```csharp
public interface IValueConverter
{
    object Convert(object value, Type targetType, object parameter, CultureInfo culture);
    object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture);
}
```

#### Convert Method
- **Purpose**: Transform source value to target value
- **Parameters**:
  - `value`: The original data value
  - `targetType`: The desired output type
  - `parameter`: Optional converter parameter
  - `culture`: Cultural information for formatting

#### ConvertBack Method
- **Purpose**: Reverse transformation (for two-way binding)
- **Implementation**: Most converters throw `NotImplementedException` as they're one-way only

### Error Handling

#### Defensive Programming
```csharp
public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
{
    return value is true ? Visibility.Visible : Visibility.Collapsed;
}
```
- Type checking prevents runtime errors
- Default values for unexpected inputs
- Clear logic flow

#### ConvertBack Exceptions
```csharp
public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
{
    throw new NotImplementedException();
}
```
- Explicitly not implemented for one-way converters
- Clear indication of unsupported operation

## Registration in App.xaml

Converters must be registered as resources to be used in XAML:

```xml
<Application.Resources>
    <converters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter"/>
    <converters:InverseBoolToVisibilityConverter x:Key="InverseBoolToVisibilityConverter"/>
    <converters:InverseBoolConverter x:Key="InverseBoolConverter"/>
    <converters:NullToVisibilityConverter x:Key="NullToVisibilityConverter"/>
    <converters:NullToCollapsedConverter x:Key="NullToCollapsedConverter"/>
    <converters:ZeroToVisibilityConverter x:Key="ZeroToVisibilityConverter"/>
    <converters:ZeroToCollapsedConverter x:Key="ZeroToCollapsedConverter"/>
</Application.Resources>
```

## Best Practices

### Naming Conventions
- Clear, descriptive names indicating purpose
- "To" in name for directionality (e.g., BoolToVisibility)
- "Inverse" prefix for negated logic

### Performance Considerations
- Converters are called frequently during UI updates
- Keep logic simple and fast
- Avoid expensive operations in Convert method

### Reusability
- Generic converters work across multiple scenarios
- Parameter support for customization
- No hard-coded values

### Testing
- Unit test each converter with various inputs
- Test edge cases (null values, wrong types)
- Verify expected outputs

## Advanced Usage

### Parameterized Converters

While not used in this project, converters can accept parameters:

```xml
<TextBlock Text="{Binding Value, 
                  Converter={StaticResource FormatConverter}, 
                  ConverterParameter='C2'}" />
```

### MultiValue Converters

For complex scenarios involving multiple bindings:

```csharp
public class MultiBooleanConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        return values.All(v => v is bool && (bool)v);
    }
    // ...
}
```

## Common Patterns

### Visibility Management
The most common use case in this application:
1. Show/hide based on boolean flags
2. Display content only when data exists
3. Toggle between loading and content states
4. Show empty states when collections are empty

### State Inversion
Frequently needed for:
- Disabling controls during operations
- Showing alternative content
- Managing mutually exclusive UI states

## Troubleshooting

### Common Issues
1. **Converter Not Found**: Ensure registration in App.xaml
2. **Wrong Key**: Check x:Key matches usage
3. **Type Mismatch**: Verify input/output types
4. **Null References**: Add null checks in converter

### Debug Tips
- Use Visual Studio XAML binding diagnostics
- Add breakpoints in Convert methods
- Check Output window for binding errors
- Verify converter registration

## Future Enhancements

### Potential Additions
1. **FormatConverter**: For string formatting
2. **BooleanToBrushConverter**: Dynamic color changes
3. **DateTimeConverter**: Date formatting
4. **EnumToDescriptionConverter**: User-friendly enum display

### Optimization Opportunities
1. **Caching**: Cache converter results for expensive operations
2. **Markup Extensions**: Reduce XAML verbosity
3. **Generic Converters**: Reduce code duplication

## Conclusion

The converter system in CAT Adaptive provides a clean, maintainable way to handle data transformations in the UI. By following established patterns and best practices, these converters enable flexible data binding while keeping the ViewModel focused on business logic rather than presentation concerns.
