using NLua;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.Documentation;
using SomethingNeedDoing.LuaMacro.Modules;

namespace SomethingNeedDoing.LuaMacro;
/// <summary>
/// Manages Lua modules and their registration.
/// </summary>
[RegisterSingleton, AutoConstruct]
public partial class LuaModuleManager
{
    private readonly List<LuaModuleBase> _modules = [];
    private readonly LuaDocumentation _documentation;

    [AutoPostConstruct]
    private void Initialize()
    {
        RegisterModule(new ActionsModule());
        RegisterModule(new AddonModule());
        RegisterModule(new DalamudModule());
        RegisterModule(new EntityModule());
        RegisterModule(new ExcelModule());
        RegisterModule(new FateModule());
        RegisterModule(new InstancedContentModule());
        RegisterModule(new InstancesModule());
        RegisterModule(new InventoryModule());
        RegisterModule(new IPCModule());
        RegisterModule(new PlayerModule());
        RegisterModule(new QuestsModule());
        RegisterModule(new SystemModule());

        // the Engines module is created per macro, so its documentation is registered statically
        EnginesModule.RegisterDocumentationStatic(_documentation);
    }

    public void RegisterAll(Lua lua) => _modules.ForEach(m => m.Register(lua));
    public T? GetModule<T>() where T : class, ILuaModule => _modules.OfType<T>().FirstOrDefault();

    public void RegisterModule(LuaModuleBase module)
    {
        module.SetModuleManager(this);
        if (module.ParentType != null)
            module.ParentModule = _modules.FirstOrDefault(m => m.GetType() == module.ParentType)
                ?? throw new InvalidOperationException($"Parent module of type {module.ParentType.Name} not found for {module.GetType().Name}");

        _modules.Add(module);
        module.RegisterDocumentation(_documentation);
    }
}
