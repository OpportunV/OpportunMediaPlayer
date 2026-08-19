using Avalonia;
using Avalonia.Headless;
using OMP.Ui.Settings;
using OMP.Ui.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace OMP.Ui.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .AfterSetup(builder => ((App)builder.Instance!).Services = new TestServiceProvider());

    private sealed class TestServiceProvider : IServiceProvider
    {
        private readonly IUserSettingsService _settings = new TestUserSettingsService();

        public object? GetService(Type serviceType) => serviceType == typeof(IUserSettingsService) ? _settings : null;
    }

    private sealed class TestUserSettingsService : IUserSettingsService
    {
        public UserSettings Current { get; } = new();

        public void Save()
        {
        }
    }
}
