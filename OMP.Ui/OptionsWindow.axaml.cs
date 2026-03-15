using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Lib.Session;
using OMP.Ui.Models;

namespace OMP.Ui;

public partial class OptionsWindow : Window
{
    private readonly IMediaSessionRegistry _mediaSessionRegistry;
    private readonly ObservableCollection<AudioRoute> _routes = [];

    private readonly List<AudioStream> _streams = [];
    private readonly List<AudioOutput> _outputs = [];

    public OptionsWindow(IMediaSessionRegistry mediaSessionRegistry)
    {
        InitializeComponent();

        _mediaSessionRegistry = mediaSessionRegistry;
        _streams.AddRange(_mediaSessionRegistry.Current?.AudioStreams ?? []);
        _outputs.AddRange(_mediaSessionRegistry.Current?.AudioOutputs ?? []);

        foreach (var audioRoute in _mediaSessionRegistry.Current?.AudioRoutes.Select(pair =>
                     new AudioRoute(pair.audioStream, pair.audioOutput)) ?? [])
        {
            _routes.Add(audioRoute);
        }

        RoutesList.ItemsSource = _routes;
        StreamSelector.ItemsSource = _streams;

        AddRouteButton.Click += OnAddRouteButton;
        SaveButton.Click += OnSaveButton;
        UpdateOutputSelector();
        Dispatcher.UIThread.Post(UpdateDeleteButtons);
    }

    private void OnAddRouteButton(object? sender, RoutedEventArgs e)
    {
        if (StreamSelector.SelectedItem is not AudioStream stream ||
            OutputSelector.SelectedItem is not AudioOutput output)
        {
            return;
        }

        AddRoute(stream, output);
    }

    private void AddRoute(AudioStream audioStream, AudioOutput audioOutput)
    {
        _routes.Add(new AudioRoute(audioStream, audioOutput));
        UpdateOutputSelector();
        UpdateDeleteButtons();
    }

    private void DeleteRoute(AudioRoute route)
    {
        _routes.Remove(route);
        UpdateOutputSelector();
        UpdateDeleteButtons();
    }

    private void OnDeleteRoute(object? sender, RoutedEventArgs e)
    {
        var route = (AudioRoute)((Button)sender!).Parent!.DataContext!;
        if (_routes.Count > 1)
        {
            DeleteRoute(route);
        }
    }

    private void OnSaveButton(object? sender, RoutedEventArgs e)
    {
        _mediaSessionRegistry.Current?.SetAudioRoutes(_routes.Select(route => (route.AudionStream, route.AudioOutput)));
        Close(true);
    }

    private void UpdateOutputSelector()
    {
        var usedOutputs = _routes.Select(r => r.Output).ToHashSet();

        var availableOutputs = _outputs
            .Where(o => !usedOutputs.Contains(o.FriendlyName))
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