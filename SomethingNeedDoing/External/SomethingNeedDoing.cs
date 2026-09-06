using System.Threading;
using System.Threading.Tasks;
using ECommons.EzIpcManager;
using Microsoft.Extensions.Hosting;
using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.External;

[RegisterSingleton<IHostedService>(Duplicate = DuplicateStrategy.Append), AutoConstruct]
public partial class SomethingNeedDoing : IHostedService
{
    private readonly IMacroScheduler _scheduler;

    [AutoPostConstruct]
    private void Initialize() => EzIPC.Init(this);

    [EzIPC]
    public bool IsAnyMacroRunning() => _scheduler.GetMacros().Any(m => m.State is MacroState.Running);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
