using Avalonia.Controls;
using OMP.Ui.Input;

namespace OMP.Ui;

public sealed partial class HotkeysWindow : Window
{
    public HotkeysWindow()
    {
        InitializeComponent();
        HotkeysList.ItemsSource = HotkeyReference.Entries;
    }
}
