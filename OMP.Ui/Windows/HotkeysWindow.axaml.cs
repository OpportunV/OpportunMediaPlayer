using Avalonia.Controls;
using OMP.Ui.Input;

namespace OMP.Ui.Windows;

public sealed partial class HotkeysWindow : Window
{
    public HotkeysWindow()
    {
        InitializeComponent();
        HotkeysList.ItemsSource = HotkeyReference.Entries;
    }
}
