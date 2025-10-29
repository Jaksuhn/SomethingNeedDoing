using ECommons.EzIpcManager;
using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.External;

public class ARDiscard : IPC
{
    public override string Name => "ARDiscard";
    public override string Repo => Repos.Vera;

    [EzIPC]
    [LuaFunction(description: "Gets a list of item IDs that should be discarded")]
    public readonly Func<IReadOnlySet<uint>> GetItemsToDiscard = null!;
}
