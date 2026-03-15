using System.Collections.ObjectModel;
using Avalonia.Controls;
using OMP.Ui.Models;

namespace OMP.Ui;

public partial class OptionsWindow : Window
{
    public ObservableCollection<AudioRoute> Routes { get; } = new();
    
    public ObservableCollection<string> AvailableTracks { get; } = new() { "Track 1", "Track 2", "Track 3" };

    public ObservableCollection<string> AvailableOutputs { get; } = new() { "Speakers", "Headphones", "HDMI" };
    
    public OptionsWindow()
    {
        InitializeComponent();

        RoutesList.ItemsSource = Routes;

        Routes.Add(new AudioRoute("1", "2"));

        AddRouteButton.Click += OnAddButton;
        SaveButton.Click += OnSaveButton;
    }

    private void OnAddButton(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Routes.Add(new AudioRoute("11", "22"));
    }

    private void OnSaveButton(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnRemoveRouteButton(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button {DataContext: AudioRoute route} && Routes.Count > 1)
        {
            Routes.Remove(route);
        }
    }
}