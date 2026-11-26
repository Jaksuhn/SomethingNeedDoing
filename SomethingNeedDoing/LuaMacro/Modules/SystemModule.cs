namespace SomethingNeedDoing.LuaMacro.Modules;

public unsafe class SystemModule : LuaModuleBase
{
    public override string ModuleName => "System";

    [LuaFunction] public void FlashIcon() => WindowFlash.Flash();
    [LuaFunction][Changelog("12.15")] public string GetClipboardText() => ImGui.GetClipboardText();
    [LuaFunction][Changelog("12.15")] public void SetClipboardText(string text) => ImGui.SetClipboardText(text);
    [LuaFunction][Changelog(ChangelogAttribute.Unreleased)] public int GetProcessId() => Environment.ProcessId;
}
