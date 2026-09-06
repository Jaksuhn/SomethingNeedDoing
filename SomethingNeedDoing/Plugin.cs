using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using ECommons;
using ECommons.Configuration;
using Microsoft.Extensions.Hosting;

namespace SomethingNeedDoing;

[AutoConstruct]
public partial class Plugin : IAsyncDalamudPlugin
{
    public string Name => Svc.PluginInterface.InternalName;
    internal string Prefix => "SND";

    internal static Plugin P { get; private set; } = null!;
    internal static Config C { get; private set; } = null!;
    internal string Version => Svc.PluginInterface.Manifest.AssemblyVersion.ToString(2);

    private readonly IDalamudPluginInterface _pluginInterface;
    private IHost _host = null!;

    public Task LoadAsync(CancellationToken cancellationToken)
    {
        P = this;
        ECommonsMain.Init(_pluginInterface, this, Module.ObjectFunctions, Module.DalamudReflector);

        EzConfig.DefaultSerializationFactory = new ConfigFactory();
        C = EzConfig.Init<Config>();
        Config.Migrate(C);

        _host = new HostBuilder()
            .UseContentRoot(_pluginInterface.AssemblyLocation.Directory!.FullName)
            .ConfigureServices(services =>
            {
                services.AddSingleton(C);
                services.AddHostedService(sp => sp.GetRequiredService<Config>());
                services.AddSomethingNeedDoing();
            })
            .Build();

        return _host.StartAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _host.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            _host.Dispose();
            ECommonsMain.Dispose();
        }
    }
}
