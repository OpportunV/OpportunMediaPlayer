using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace OMP.Ui.Services;

internal sealed class FilePickerService : IFilePickerService
{
    /// <inheritdoc/>
    public async Task<string?> PickFileAsync(Window owner, string title, FilePickerFileType filter)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = [filter]
            });

        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }
}