using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using OMP.Ui.Models;

namespace OMP.Ui;

public partial class OptionsWindow : Window
{
    public ObservableCollection<AudioRoute> Routes { get; } = [];

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

        RoutesList.ItemsSource = Routes;

        TrackSelector.ItemsSource = _tracks;
        OutputSelector.ItemsSource = _outputs;

        AddRouteButton.Click += AddRouteButton_Click;
        SaveButton.Click += SaveButton_Click;

        RoutesList.AddHandler(Button.ClickEvent, DeleteRouteHandler, handledEventsToo: true);

        Routes.Add(new AudioRoute(_tracks[0], _outputs[0]));

        UpdateDeleteButtons();
    }

    private void AddRouteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (TrackSelector.SelectedItem is not string track ||
            OutputSelector.SelectedItem is not string output)
            return;

        Routes.Add(new AudioRoute(track, output));

        UpdateDeleteButtons();
    }

    private void DeleteRouteHandler(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button button &&
            button is { Name: "DeleteRouteButton", DataContext: AudioRoute route } &&
            Routes.Count > 1)
        {
            Routes.Remove(route);
            UpdateDeleteButtons();
        }
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void UpdateDeleteButtons()
    {
        var canDelete = Routes.Count > 1;

        foreach (var control in RoutesList.GetLogicalDescendants())
        {
            if (control is Button b && b.Name == "DeleteRouteButton")
                b.IsEnabled = canDelete;
        }
    }
}