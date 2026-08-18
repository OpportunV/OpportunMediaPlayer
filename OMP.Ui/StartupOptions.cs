namespace OMP.Ui;

public sealed class StartupOptions(string? filePath)
{
    public string? FilePath { get; set; } = filePath;
}
