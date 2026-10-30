using System.Threading;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Utils;

/// <summary>Cooperative pause waits shared by the Lua and native macro engines.</summary>
internal static class MacroPause
{
    internal static async Task WaitAsync(ManualResetEventSlim pauseEvent, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // Macro startup and command continuations can run on the framework thread.
        // A synchronous event wait here would prevent the game from processing Resume/Stop.
        while (!pauseEvent.IsSet)
            await Task.Delay(25, token);

        token.ThrowIfCancellationRequested();
    }
}
