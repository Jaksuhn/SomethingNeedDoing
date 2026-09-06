using Dalamud.Interface.Windowing;

namespace SomethingNeedDoing.Services;

[RegisterSingleton<WindowSystem>]
public class SndWindowSystem() : WindowSystem(P.Name);
