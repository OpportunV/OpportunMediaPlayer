using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OMP.Ui.Helpers;
using OMP.Ui.Models;

namespace OMP.Ui.Controls;

/// <summary>
/// A dropdown-looking button whose flyout filters the track list as you type. Built instead of an
/// editable ComboBox/AutoCompleteBox because a web video can offer ~160 caption tracks, and both of
/// those change their selection on every arrow key - each change here opens a network sidecar, so
/// this only commits on a click or Enter.
/// </summary>
internal sealed partial class SubtitleTrackPicker : UserControl
{
    public event EventHandler<RoutedEventArgs> OptionPicked
    {
        add => AddHandler(OptionPickedEvent, value);
        remove => RemoveHandler(OptionPickedEvent, value);
    }

    public IReadOnlyList<SubtitleStreamOption> Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public SubtitleStreamOption? SelectedOption
    {
        get => GetValue(SelectedOptionProperty);
        set => SetValue(SelectedOptionProperty, value);
    }

    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<SubtitleStreamOption>> OptionsProperty =
        AvaloniaProperty.Register<SubtitleTrackPicker, IReadOnlyList<SubtitleStreamOption>>(nameof(Options), []);

    public static readonly StyledProperty<SubtitleStreamOption?> SelectedOptionProperty =
        AvaloniaProperty.Register<SubtitleTrackPicker, SubtitleStreamOption?>(
            nameof(SelectedOption), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<SubtitleTrackPicker, string?>(nameof(PlaceholderText));

    /// <summary>
    /// Raised only when the user commits a different track, never for a programmatic change.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> OptionPickedEvent =
        RoutedEvent.Register<SubtitleTrackPicker, RoutedEventArgs>(nameof(OptionPicked), RoutingStrategies.Bubble);

    private const double MinFlyoutWidth = 320;

    public SubtitleTrackPicker()
    {
        InitializeComponent();

        var flyout = (Flyout)PickerButton.Flyout!;
        flyout.Opened += (_, _) => OnFlyoutOpened();

        SearchBox.TextChanged += (_, _) => RefreshList();
        SearchBox.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
        OptionsList.Tapped += (_, e) =>
        {
            // Tapping the scrollbar raises Tapped too; only a tap on an item is a pick.
            if ((e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { DataContext: SubtitleStreamOption option })
            {
                Commit(option);
            }
        };
        OptionsList.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit(OptionsList.SelectedItem as SubtitleStreamOption);
                e.Handled = true;
            }
        };

        UpdateSelectionText();
    }

    /// <summary>
    /// The options currently shown for <paramref name="query"/>, in their original order.
    /// </summary>
    internal IReadOnlyList<SubtitleStreamOption> Filter(string? query) =>
        Options.Where(option => SearchQuery.Matches(query, option.SearchText)).ToList();

    /// <summary>
    /// Commits <paramref name="option"/> as if the user had picked it: closes the flyout and, when
    /// it differs from the current track, updates <see cref="SelectedOption"/> and raises
    /// <see cref="OptionPicked"/>.
    /// </summary>
    internal void Commit(SubtitleStreamOption? option)
    {
        if (option is not { IsSupported: true })
        {
            return;
        }

        PickerButton.Flyout?.Hide();

        if (option.Stream.Id == SelectedOption?.Stream.Id)
        {
            return;
        }

        SetCurrentValue(SelectedOptionProperty, option);
        RaiseEvent(new RoutedEventArgs(OptionPickedEvent, this));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedOptionProperty || change.Property == PlaceholderTextProperty)
        {
            UpdateSelectionText();
        }
    }

    private void OnFlyoutOpened()
    {
        FlyoutPanel.Width = Math.Max(Bounds.Width, MinFlyoutWidth);
        SearchBox.Text = string.Empty;
        RefreshList();

        if (SelectedOption is { } selected && OptionsList.ItemsSource is IReadOnlyList<SubtitleStreamOption> shown &&
            shown.FirstOrDefault(o => o.Stream.Id == selected.Stream.Id) is { } current)
        {
            OptionsList.SelectedItem = current;
            OptionsList.ScrollIntoView(current);
        }

        Dispatcher.UIThread.Post(() => SearchBox.Focus(), DispatcherPriority.Input);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                MoveHighlight(+1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveHighlight(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                Commit(OptionsList.SelectedItem as SubtitleStreamOption);
                e.Handled = true;
                break;
        }
    }

    private void MoveHighlight(int direction)
    {
        if (OptionsList.ItemsSource is not IReadOnlyList<SubtitleStreamOption> shown || shown.Count == 0)
        {
            return;
        }

        var index = OptionsList.SelectedIndex;
        do
        {
            index += direction;
        }
        while (index >= 0 && index < shown.Count && !shown[index].IsSupported);

        if (index >= 0 && index < shown.Count)
        {
            OptionsList.SelectedIndex = index;
            OptionsList.ScrollIntoView(shown[index]);
        }
    }

    private void RefreshList()
    {
        var shown = Filter(SearchBox.Text);
        OptionsList.ItemsSource = shown;
        OptionsList.SelectedItem = shown.FirstOrDefault(o => o.IsSupported);
        NoMatchesText.IsVisible = shown.Count == 0;
    }

    private void UpdateSelectionText()
    {
        SelectionText.Text = SelectedOption?.Label ?? PlaceholderText;
        SelectionText.Opacity = SelectedOption is null ? 0.6 : 1;
        ToolTip.SetTip(PickerButton, SelectedOption?.Label);
    }
}
