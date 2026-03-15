using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OMP.Ui.Models;

namespace OMP.Ui;

public partial class OptionsWindow : Window
{
    private readonly ObservableCollection<AudioRoute> _routes = [];

    private readonly string[] _tracks =
    [
        "Track 1",
        "Track 2",
        "Track 3"
    ];

    private readonly string[] _outputs =
    [
        "Speakers",
        "Headphones",
        "HDMI"
    ];

    public OptionsWindow()
    {
        InitializeComponent();

        RoutesList.ItemsSource = _routes;

        TrackSelector.ItemsSource = _tracks;

        AddRouteButton.Click += OnAddRouteButton;
        SaveButton.Click += OnSaveButton;

        _routes.Add(new AudioRoute(_tracks[0], _outputs[0]));

        UpdateOutputSelector();
        Dispatcher.UIThread.Post(UpdateDeleteButtons);
    }

    private void OnAddRouteButton(object? sender, RoutedEventArgs e)
    {
        if (TrackSelector.SelectedItem is not string track ||
            OutputSelector.SelectedItem is not string output)
        {
            return;
        }

        _routes.Add(new AudioRoute(track, output));

        UpdateOutputSelector();
        UpdateDeleteButtons();
    }

    private void OnDeleteRoute(object? sender, RoutedEventArgs e)
    {
        var route = (AudioRoute)((Button)sender!).Parent!.DataContext!;
        if (_routes.Count > 1)
        {
            _routes.Remove(route);

            UpdateOutputSelector();
            UpdateDeleteButtons();
        }
    }

    private void OnSaveButton(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void UpdateOutputSelector()
    {
        var usedOutputs = _routes.Select(r => r.Output).ToHashSet();

        var availableOutputs = _outputs
            .Where(o => !usedOutputs.Contains(o))
            .ToList();

        OutputSelector.ItemsSource = availableOutputs;

        if (!availableOutputs.Contains(OutputSelector.SelectedItem))
        {
            OutputSelector.SelectedItem = null;
        }
    }

    private void UpdateDeleteButtons()
    {
        var canDelete = _routes.Count > 1;

        foreach (var control in RoutesList.GetVisualDescendants())
        {
            if (control is Button { Name: "DeleteRouteButton" } button)
            {
                button.IsEnabled = canDelete;
            }
        }
    }
}