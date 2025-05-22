using Microsoft.Extensions.DependencyInjection;
using SomethingNeedDoing.Gui;
using SomethingNeedDoing.External;
using Dalamud.Interface.Windowing;
using SomethingNeedDoing.Framework.Interfaces;
using SomethingNeedDoing.NativeMacro;
using SomethingNeedDoing.LuaMacro;
using SomethingNeedDoing.Scheduler;
using SomethingNeedDoing.Managers;

namespace SomethingNeedDoing.Utils;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSomethingNeedDoingServices(this IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<MacroParser>();
        services.AddSingleton<NativeMacroEngine>();
        services.AddSingleton<NLuaMacroEngine>();
        services.AddSingleton<LayLuaMacroEngine>();
        services.AddSingleton<TriggerEventManager>();
        services.AddSingleton<IMacroScheduler, MacroScheduler>();
        services.AddSingleton<IMacroEngine, NativeMacroEngine>();
        services.AddSingleton<IMacroEngine, NLuaMacroEngine>();
        services.AddSingleton<IMacroEngine, LayLuaMacroEngine>();
        services.AddSingleton<LuaModuleManager>();
        services.AddSingleton<GitMacroManager>();
        services.AddSingleton<MacroHierarchyManager>();

        // UI Services
        services.AddSingleton<RunningMacrosPanel>();
        services.AddSingleton<WindowSystem>();
        services.AddSingleton<MacroStatusWindow>();
        services.AddSingleton<HelpUI>();
        services.AddSingleton(sp => new MacroEditor(
            sp.GetRequiredService<IMacroScheduler>(),
            sp.GetRequiredService<GitMacroManager>(),
            sp.GetRequiredService<MacroStatusWindow>()
        ));
        services.AddSingleton(sp => new MainWindow(
            sp.GetRequiredService<WindowSystem>(),
            sp.GetRequiredService<IMacroScheduler>(),
            sp.GetRequiredService<GitMacroManager>(),
            sp.GetRequiredService<RunningMacrosPanel>(),
            sp.GetRequiredService<MacroEditor>(),
            sp.GetRequiredService<HelpUI>(),
            sp.GetRequiredService<MacroStatusWindow>()
        ));

        // External Services
        services.AddSingleton<Tippy>();

        // Framework Services
        services.AddTransient<MacroContext>();

        return services;
    }
}
