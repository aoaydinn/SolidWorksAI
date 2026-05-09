using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SolidWorksAI.Core;
using SolidWorksAI.Services;
using SolidWorksAI.ViewModels;
using SolidWorksAI.Views;

namespace SolidWorksAI;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sc = new ServiceCollection();

        // HTTP — Ollama 0.23+ Origin header kontrolü yapar; localhost origin ekliyoruz
        sc.AddHttpClient<OllamaClient>(client =>
        {
            client.BaseAddress = new Uri("http://localhost:11434");
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.Add("Origin", "http://localhost");
        });

        // Core
        sc.AddSingleton<SolidWorksConnector>();
        sc.AddSingleton<ActionRegistry>();
        sc.AddSingleton<ActionExecutor>();
        sc.AddSingleton<PromptBuilder>();

        // ViewModel & Window
        sc.AddSingleton<MainViewModel>();
        sc.AddSingleton<MainWindow>();

        _services = sc.BuildServiceProvider();

        var win = _services.GetRequiredService<MainWindow>();
        win.DataContext = _services.GetRequiredService<MainViewModel>();
        MainWindow = win;
        win.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.GetRequiredService<SolidWorksConnector>().Dispose();
        _services?.Dispose();
        base.OnExit(e);
    }
}
