using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace OMP.Ui.Services;

public interface IFilePickerService
{
    /// <summary>
    /// Shows a single-file open picker and returns the chosen path, or <see langword="null"/> if
    /// the user cancelled or the chosen file has no local path.
    /// </summary>
    public Task<string?> PickFileAsync(Window owner, string title, FilePickerFileType filter);
}
