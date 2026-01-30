using System.Windows.Controls;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using CatAdaptive.App.ViewModels;

namespace CatAdaptive.App.Views;

public partial class LessonsView : UserControl
{
    private readonly LessonsViewModel _viewModel;
    private readonly Dictionary<ScrollViewer, DispatcherTimer> _scrollTimers = new();
    private const int ScrollDelay = 500; // ms delay before updating progress

    public LessonsView()
    {
        InitializeComponent();
        _viewModel = (LessonsViewModel)DataContext;
        Loaded += LessonsView_Loaded;
    }

    private void LessonsView_Loaded(object sender, RoutedEventArgs e)
    {
        // Find all ScrollViewers in the lesson detail sections
        FindScrollViewers(this);
    }

    private void FindScrollViewers(DependencyObject parent)
    {
        if (parent == null) return;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            
            if (child is ScrollViewer scrollViewer)
            {
                // Subscribe to scroll events
                scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            }
            else if (child is ItemsControl itemsControl)
            {
                // Look for ScrollViewers within ItemsControl templates
                if (VisualTreeHelper.GetChildrenCount(itemsControl) > 0)
                {
                    var itemsPresenter = (ItemsPresenter)VisualTreeHelper.GetChild(itemsControl, 0);
                    if (itemsPresenter != null)
                    {
                        FindScrollViewers(itemsPresenter);
                    }
                }
            }
            else
            {
                // Recursively search child elements
                FindScrollViewers(child);
            }
        }
    }

    private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || e.VerticalChange == 0) return;

        // Stop any existing timer for this ScrollViewer
        if (_scrollTimers.TryGetValue(scrollViewer, out var existingTimer))
        {
            existingTimer.Stop();
        }

        // Create a new timer to debounce scroll events
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(ScrollDelay)
        };
        timer.Tick += (s, args) =>
        {
            ScrollTimerCallback(scrollViewer);
            timer.Stop();
        };
        timer.Start();
        _scrollTimers[scrollViewer] = timer;
    }

    private void ScrollTimerCallback(ScrollViewer scrollViewer)
    {
        // Calculate scroll percentage
        var scrollPercent = scrollViewer.VerticalOffset;
        var maxOffset = scrollViewer.ScrollableHeight;
        var percentRead = maxOffset > 0 ? (scrollPercent / maxOffset) * 100 : 0;

        // Find the section index
        var sectionContainer = FindParent<Border>(scrollViewer, "SectionContainerStyle");
        if (sectionContainer != null)
        {
            var sectionIndex = GetSectionIndex(sectionContainer);
            
            // Update progress
            _ = _viewModel.UpdateSectionProgressAsync(sectionIndex, percentRead);
        }

        // Clean up timer
        if (_scrollTimers.TryGetValue(scrollViewer, out var timer))
        {
            timer.Stop();
            _scrollTimers.Remove(scrollViewer);
        }
    }

    private T? FindParent<T>(DependencyObject child, string? styleKey = null) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        
        while (parent != null)
        {
            if (parent is T target)
            {
                if (styleKey == null || (target is FrameworkElement fe && fe.Style?.TargetType?.Name == styleKey))
                {
                    return target;
                }
            }
            parent = VisualTreeHelper.GetParent(parent);
        }
        
        return null;
    }

    private int GetSectionIndex(Border sectionContainer)
    {
        // Find the parent ItemsControl
        var itemsControl = FindParent<ItemsControl>(sectionContainer);
        if (itemsControl == null) return -1;

        // Find the container for this item
        var container = itemsControl.ItemContainerGenerator.ContainerFromItem(sectionContainer.DataContext);
        if (container == null) return -1;

        // Get the index from the ItemsControl
        return itemsControl.ItemContainerGenerator.IndexFromContainer(container);
    }
}
