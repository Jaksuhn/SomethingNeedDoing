using Dalamud.Plugin;
using ECommons;
using ECommons.Configuration;
using ECommons.Singletons;
using Microsoft.Extensions.DependencyInjection;
using SomethingNeedDoing.Services;
using SndIpc = SomethingNeedDoing.External.SomethingNeedDoing;

namespace SomethingNeedDoing;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => Svc.PluginInterface.InternalName;
    internal string Prefix => "SND";

    internal static Plugin P { get; private set; } = null!;
    internal static Config C { get; private set; } = null!;
    internal string Version => Svc.PluginInterface.Manifest.AssemblyVersion.ToString(2);

    private readonly ServiceProvider _serviceProvider;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        P = this;
        ECommonsMain.Init(pluginInterface, this, Module.ObjectFunctions, Module.DalamudReflector);

        EzConfig.DefaultSerializationFactory = new ConfigFactory();
        C = EzConfig.Init<Config>();
        Config.Migrate(C);
        Config.InitializeFileWatcher();

        _serviceProvider = new ServiceCollection().SetupPluginServices().BuildServiceProvider();
        _ = _serviceProvider.GetRequiredService<WindowService>();
        _ = _serviceProvider.GetRequiredService<CommandService>();
        _ = _serviceProvider.GetRequiredService<StubGeneratorService>();
        _ = _serviceProvider.GetRequiredService<SndIpc>();
        SingletonServiceManager.Initialize(typeof(StaticsService)); // rip 100% DI
    }

    public void Dispose()
    {
        Config.DisposeFileWatcher();
        _serviceProvider.Dispose();
        ECommonsMain.Dispose();
    }
}
