using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;

namespace OMP.Ui.Helpers;

internal static class OptionsSelector
{
    public static IReadOnlyList<T> AvailableOptions<T, TKey>(
        IEnumerable<T> all, IEnumerable<TKey> usedKeys, Func<T, TKey> keySelector)
    {
        var used = usedKeys.ToHashSet();
        return all.Where(item => !used.Contains(keySelector(item))).ToList();
    }

    /// <summary>
    /// Points <paramref name="selector"/> at the options not already used, dropping its selection
    /// if that option is no longer among them. Options windows offer several of these "pick one
    /// that isn't taken yet" dropdowns, each over an unrelated type.
    /// </summary>
    public static void Rebind<T, TKey>(
        ComboBox selector, IEnumerable<T> all, IEnumerable<TKey> usedKeys, Func<T, TKey> keySelector)
        where T : class
    {
        var available = AvailableOptions(all, usedKeys, keySelector);
        selector.ItemsSource = available;

        if (selector.SelectedItem is T selected && !available.Contains(selected))
        {
            selector.SelectedItem = null;
        }
    }
}
