using SomethingNeedDoing.Core.Events;
using SomethingNeedDoing.Core.Interfaces;
using System.Text.RegularExpressions;

namespace SomethingNeedDoing.Utils;

public static class ConfigMacroExtensions
{
    /// <summary>
    /// Removes the metadata block from macro content.
    /// </summary>
    public static string ContentSansMetadata(this IMacro macro)
    {
        var match = MetadataParser.MetadataBlockRegex.Match(macro.Content);
        if (!match.Success)
            return macro.Content;
        return macro.Content[..match.Index] + macro.Content[(match.Index + match.Length)..].TrimStart('\r', '\n');
    }

    public static void Rename(this ConfigMacro macro, string newName)
    {
        if (C.IsValidMacroName(newName, macro.Id))
        {
            macro.Name = newName;
            C.Save();
        }
        else
        {
            macro.Name = C.GetUniqueMacroName(newName, macro.Id);
            C.Save();
        }
    }

    public static void Duplicate(this ConfigMacro macro, string? newName = null)
    {
        var duplicate = new ConfigMacro
        {
            Name = C.GetUniqueMacroName(newName ?? $"{macro.Name} (Copy)"),
            Type = macro.Type,
            Content = macro.Content,
            FolderPath = macro.FolderPath
        };
        C.Macros.Add(duplicate);
    }
    public static void Delete(this ConfigMacro macro) => C.Macros.RemoveAll(m => m.Id == macro.Id);
    public static void Move(this ConfigMacro macro, string folderPath) => macro.FolderPath = folderPath;
    public static void Start(this IMacro macro, IMacroScheduler scheduler, TriggerEventArgs? args = null)
        => _ = scheduler.StartMacro(macro, 0, args);
    public static void Stop(this IMacro macro, IMacroScheduler scheduler)
        => scheduler.StopMacro(macro.Id);
    public static void Pause(this IMacro macro, IMacroScheduler scheduler)
        => scheduler.SetPaused(macro.Id, true);
    public static void Resume(this IMacro macro, IMacroScheduler scheduler)
        => scheduler.SetPaused(macro.Id, false);
    public static void UpdateLastModified(this IMacro macro) => macro.Metadata.LastModified = DateTime.Now;

    /// <summary>
    /// Adds a trigger event to a macro and subscribes to it.
    /// </summary>
    /// <param name="macro">The macro to add the trigger to.</param>
    /// <param name="scheduler">The macro scheduler to use for subscription.</param>
    /// <param name="triggerEvent">The trigger event to add.</param>
    public static void AddTriggerEvent(this IMacro macro, IMacroScheduler scheduler, TriggerEvent triggerEvent)
    {
        Svc.Log.Debug($"Adding trigger event {triggerEvent} to macro {macro.Name}");
        macro.Metadata.TriggerEvents.Add(triggerEvent);
        scheduler.SubscribeToTriggerEvent(macro, triggerEvent);
        C.Save();
    }

    /// <summary>
    /// Removes a trigger event from a macro and unsubscribes from it.
    /// </summary>
    /// <param name="macro">The macro to remove the trigger from.</param>
    /// <param name="scheduler">The macro scheduler to use for unsubscription.</param>
    /// <param name="triggerEvent">The trigger event to remove.</param>
    public static void RemoveTriggerEvent(this IMacro macro, IMacroScheduler scheduler, TriggerEvent triggerEvent)
    {
        Svc.Log.Debug($"Removing trigger event {triggerEvent} from macro {macro.Name}");
        macro.Metadata.TriggerEvents.Remove(triggerEvent);
        scheduler.UnsubscribeFromTriggerEvent(macro, triggerEvent);
        C.Save();
    }

    public static void SetTriggerEvents(this IMacro macro, IMacroScheduler scheduler, IEnumerable<TriggerEvent> triggerEvents)
    {
        // First, remove all existing trigger events that aren't in the new set
        var eventsToRemove = macro.Metadata.TriggerEvents.Where(e => !triggerEvents.Contains(e)).ToList();
        foreach (var triggerEvent in eventsToRemove)
            macro.RemoveTriggerEvent(scheduler, triggerEvent);

        // Then, add all new trigger events that aren't already in the set
        var eventsToAdd = triggerEvents.Where(e => !macro.Metadata.TriggerEvents.Contains(e)).ToList();
        foreach (var triggerEvent in eventsToAdd)
            macro.AddTriggerEvent(scheduler, triggerEvent);
    }

    public static bool HasValidConfigs(this IMacro macro)
    {
        if (macro is ConfigMacro m)
        {
            foreach (var cfg in m.Metadata.Configs)
            {
                if (!string.IsNullOrEmpty(cfg.Value.ValidationPattern))
                {
                    try
                    {
                        if (!Regex.IsMatch(cfg.Value.ToString() ?? string.Empty, cfg.Value.ValidationPattern))
                            return false;
                    }
                    catch
                    {
                        Svc.Log.Error($"Unable to validate config value [{cfg.Value}] against pattern [{cfg.Value.ValidationPattern}]");
                        return false;
                    }
                }
            }
        }
        return true;
    }
}
