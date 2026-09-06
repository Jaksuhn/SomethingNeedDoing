using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Microsoft.Extensions.Hosting;
using SomethingNeedDoing.Gui;

namespace SomethingNeedDoing.Services;

[RegisterSingleton<IHostedService>(Duplicate = DuplicateStrategy.Append), AutoConstruct]
public partial class WindowService : IHostedService, IDisposable
{
    private readonly WindowSystem _ws;
    private readonly MainWindow _mainWindow;
    private readonly StatusWindow _runningMacrosWindow;
    private readonly ChangelogWindow _changelogWindow;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ws.AddWindow(_mainWindow);
        _ws.AddWindow(_runningMacrosWindow);
        _ws.AddWindow(_changelogWindow);
        Svc.PluginInterface.UiBuilder.Draw += _ws.Draw;
        Svc.PluginInterface.UiBuilder.OpenMainUi += _mainWindow.Toggle;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += _mainWindow.Toggle;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Svc.PluginInterface.UiBuilder.Draw -= _ws.Draw;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= _mainWindow.Toggle;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= _mainWindow.Toggle;
        _ws.RemoveAllWindows();
        return Task.CompletedTask;
    }

    public void Dispose() => StopAsync(CancellationToken.None).GetAwaiter().GetResult();
}
