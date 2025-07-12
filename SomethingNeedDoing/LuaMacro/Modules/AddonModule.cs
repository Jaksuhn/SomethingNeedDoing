using FFXIVClientStructs.FFXIV.Component.GUI;
using NLua;
using SomethingNeedDoing.LuaMacro.Wrappers;

namespace SomethingNeedDoing.LuaMacro.Modules;
public unsafe class AddonModule : LuaModuleBase
{
    public override string ModuleName => "Addons";
    public override void Register(Lua lua)
    {
        lua.RegisterEnum<NodeType>();
        base.Register(lua);
    }

    [LuaFunction] public AddonWrapper GetAddon(string name) => new(name);
}
