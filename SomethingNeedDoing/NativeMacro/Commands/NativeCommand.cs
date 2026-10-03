using ECommons.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace SomethingNeedDoing.NativeMacro.Commands;

public class NativeCommand(string text) : MacroCommandBase(text)
{
    public override bool RequiresFrameworkThread => true;

    public override async Task Execute(MacroContext context, CancellationToken token)
    {
        Chat.SendMessage(CommandText.StartsWith('/') ? CommandText : $"/e {CommandText}");
        await PerformWait(token);
    }
}
