using ECommons.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace SomethingNeedDoing.NativeMacro.Commands;
/// <summary>
/// Executes game actions and waits for their completion.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ActionCommand"/> class.
/// </remarks>
[GenericDoc(
    "Execute a game action",
    ["actionName"],
    ["/action \"Basic Synthesis\"", "/action \"Basic Touch\" <condition.good>", "/action \"Basic Synthesis\" <errorif.actiontimeout>"]
)]
public class ActionCommand(string text, string actionName) : MacroCommandBase(text)
{
    /// <inheritdoc/>
    public override bool RequiresFrameworkThread => true;
    private bool awaitCraftAction;

    /// <inheritdoc/>
    public override async Task Execute(MacroContext context, CancellationToken token)
    {
        awaitCraftAction = false;
        FrameworkLogger.Debug($"ActionCommand.Execute: Starting {actionName}, UnsafeModifier: {UnsafeModifier != null}, WaitDuration: {WaitDuration}");

        var craftingComplete = new TaskCompletionSource<bool>();
        var actionExecuted = false;
        void OnConditionChange(ConditionFlag flag, bool value)
        {
            if (flag == ConditionFlag.ExecutingCraftingAction && !value)
                craftingComplete.TrySetResult(true);
        }

        var craftAction = Game.Crafting.FindCraftActionByNameAndJob(actionName, Player.Job);
        await context.RunOnFramework(() =>
        {
            if (ConditionModifier is { } cnd)
            {
                if (!cnd.IsMet(Game.Crafting.GetCondition().ToString()))
                {
                    FrameworkLogger.Debug($"Condition skip: condition is {Game.Crafting.GetCondition()}, required condition is {cnd.ModifierText}");
                    return;
                }
            }

            if (craftAction is { })
            {
                if (C.CraftSkip)
                {
                    if (!Game.Crafting.IsCrafting())
                    {
                        FrameworkLogger.Debug($"Not crafting skip: {CommandText}");
                        return;
                    }

                    if (Game.Crafting.IsMaxProgress())
                    {
                        FrameworkLogger.Debug($"Max progress skip: {CommandText}");
                        return;
                    }
                }

                if (C.QualitySkip && Game.Crafting.IsMaxQuality() && craftAction.IncreasesQuality && !craftAction.IncreasesProgress) // allow actions that do both
                {
                    FrameworkLogger.Debug($"Max quality skip: {CommandText}");
                    return;
                }

                if (C.SmartWait && UnsafeModifier is null && WaitDuration <= 0)
                {
                    awaitCraftAction = true;
                    Svc.Condition.ConditionChange += OnConditionChange;
                }
            }

            Chat.SendMessage(CommandText);
            actionExecuted = true;
        });

        if (awaitCraftAction)
        {
            try
            {
                using var cts = new CancellationTokenSource(5000); // 5 second timeout
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, token);
                await craftingComplete.Task.WaitAsync(linkedCts.Token);
            }
            catch (TimeoutException)
            {
                if (C.StopOnError || ErrorIfModifier?.Condition == Modifiers.ErrorCondition.ActionTimeout)
                    throw new MacroTimeoutException("Did not receive a timely response");
            }
            finally
            {
                await context.RunOnFramework(() => Svc.Condition.ConditionChange -= OnConditionChange);
            }
        }

        if (WaitDuration > 0 && actionExecuted)
            await PerformWait(token);
    }
}
