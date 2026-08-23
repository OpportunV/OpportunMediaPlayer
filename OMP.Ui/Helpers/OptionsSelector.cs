using System;
using System.Collections.Generic;
using System.Linq;

namespace OMP.Ui.Helpers;

internal static class OptionsSelector
{
    public static IReadOnlyList<T> AvailableOptions<T, TKey>(
        IEnumerable<T> all, IEnumerable<TKey> usedKeys, Func<T, TKey> keySelector)
    {
        var used = usedKeys.ToHashSet();
        return all.Where(item => !used.Contains(keySelector(item))).ToList();
    }
}
