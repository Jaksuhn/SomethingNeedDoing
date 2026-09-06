using ECommons.EzIpcManager;
using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.External;

public class SomethingNeedDoing(IMacroScheduler scheduler) : IPC
{
    public override string Name => Svc.PluginInterface.Manifest.Name;
    public override string Repo => Repos.Croizat;

    [EzIPC]
    public bool IsAnyMacroRunning() => scheduler.GetMacros().Any(m => m.State is MacroState.Running);
}
