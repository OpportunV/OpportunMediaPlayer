using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OMP.Ui.Settings;
using OMP.Ui.Windows;

namespace OMP.Ui.Services;

internal sealed class OptionsSubtitleZonesSection
{
    public event Action? ZonesChanged;

    public ObservableCollection<SubtitleZone> Zones { get; } = [];

    private readonly Window _owner;
    private readonly IWindowFactory _windowFactory;
    private readonly IUserSettingsService _settings;

    public OptionsSubtitleZonesSection(
        Window owner,
        ItemsControl zonesList,
        Button addZoneButton,
        IWindowFactory windowFactory,
        IUserSettingsService settings)
    {
        _owner = owner;
        _windowFactory = windowFactory;
        _settings = settings;

        foreach (var zone in _settings.Current.SubtitleZones)
        {
            Zones.Add(zone.Clone());
        }

        zonesList.ItemsSource = Zones;
        addZoneButton.Click += OnAddZone;
    }

    internal async void OnEditZone(object? sender, RoutedEventArgs e)
    {
        if (((Control)sender!).DataContext is not SubtitleZone zone)
        {
            return;
        }

        var result = await _windowFactory.ShowDialogAsync<SubtitleZoneEditorWindow, SubtitleZone>(
            _owner, w => w.Load(zone.Clone(), isNew: false));
        if (result is null)
        {
            return;
        }

        var index = Zones.IndexOf(zone);
        if (index >= 0)
        {
            Zones[index] = result;
            PersistAndNotify();
        }
    }

    internal void OnResetZone(object? sender, RoutedEventArgs e)
    {
        if (((Control)sender!).DataContext is not SubtitleZone { IsBuiltIn: true } zone)
        {
            return;
        }

        var index = Zones.IndexOf(zone);
        if (index < 0)
        {
            return;
        }

        Zones[index] = SubtitleZone.CreateBuiltIns().First(z => z.Id == zone.Id);
        PersistAndNotify();
    }

    internal void OnDeleteZone(object? sender, RoutedEventArgs e)
    {
        if (((Control)sender!).DataContext is not SubtitleZone zone || zone.IsBuiltIn)
        {
            return;
        }

        Zones.Remove(zone);
        PersistAndNotify();
    }

    private async void OnAddZone(object? sender, RoutedEventArgs e)
    {
        var result = await _windowFactory.ShowDialogAsync<SubtitleZoneEditorWindow, SubtitleZone>(
            _owner, w => w.Load(new SubtitleZone(), isNew: true));
        if (result is null)
        {
            return;
        }

        Zones.Add(result);
        PersistAndNotify();
    }

    private void PersistAndNotify()
    {
        _settings.Current.SubtitleZones = Zones.ToList();
        _settings.Save();
        ZonesChanged?.Invoke();
    }
}
