using AutoRetainerAPI;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Chat;
using Dalamud.Game.DutyState;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.LuaMacro.Wrappers;
using System.Collections.Concurrent;

namespace SomethingNeedDoing.Managers;

/// <summary>
/// Listens to game and plugin events and raises them as trigger events.
/// </summary>
[RegisterSingleton, AutoConstruct]
public partial class TriggerEventSources : IDisposable
{
    private static readonly TimeSpan PluginPollInterval = TimeSpan.FromSeconds(1);

    private readonly TriggerEventManager _triggerEventManager;

    private readonly ConcurrentDictionary<(AddonEvent EventType, string AddonName), (int RefCount, IAddonLifecycle.AddonEventDelegate Handler)> _addonListenerHandlers = [];
    private readonly ConcurrentDictionary<string, AutoRetainerApi> _arApis = [];
    private HashSet<string> _activePlugins = [];
    private DateTime _nextPluginPoll = DateTime.MinValue;
    private long _combatStart = 0;

    private unsafe delegate long OnEmoteFuncDelegate(IntPtr a1, GameObject* source, ushort emoteId, GameObjectId targetId, long a5);
    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 30 4C 8B 74 24 ?? 48 8B D9", DetourName = nameof(OnEmoteFuncDetour))]
    private readonly Hook<OnEmoteFuncDelegate> OnEmoteFuncHook = null!;

    [AutoPostConstruct]
    private void Initialize()
    {
        Svc.Hook.InitializeFromAttributes(this);
        OnEmoteFuncHook?.Enable();

        Svc.Framework.Update += OnFrameworkUpdate;
        Svc.Condition.ConditionChange += OnConditionChange;
        Svc.ClientState.TerritoryChanged += OnTerritoryChanged;
        Svc.Chat.ChatMessage += OnChatMessage;
        Svc.ClientState.Login += OnLogin;
        Svc.ClientState.Logout += OnLogout;
        Svc.DutyState.DutyStarted += OnDutyStarted;
        Svc.DutyState.DutyWiped += OnDutyWiped;
        Svc.DutyState.DutyCompleted += OnDutyCompleted;
    }

    private void Raise(TriggerEvent triggerEvent, object? data = null) => _triggerEventManager.RaiseTriggerEvent(triggerEvent, data);

    #region Addon listeners
    /// <summary>
    /// Starts listening to an addon event. Listeners are reference counted, so every call must be paired with <see cref="ReleaseAddonListener"/>.
    /// </summary>
    public void AcquireAddonListener(AddonEvent eventType, string addonName)
    {
        var key = (eventType, addonName);
        var eventTypeStr = eventType.ToString();
        var (refCount, handler) = _addonListenerHandlers.AddOrUpdate(key,
            _ => (1, (type, args) => OnAddonEvent(type, args, addonName, eventTypeStr)),
            (_, existing) => (existing.RefCount + 1, existing.Handler));
        if (refCount == 1)
            Svc.AddonLifecycle.RegisterListener(eventType, addonName, handler);
    }

    /// <summary>
    /// Releases a listener acquired with <see cref="AcquireAddonListener"/>.
    /// </summary>
    public void ReleaseAddonListener(AddonEvent eventType, string addonName)
    {
        var key = (eventType, addonName);
        if (!_addonListenerHandlers.TryGetValue(key, out var existing))
            return;

        if (existing.RefCount <= 1)
        {
            Svc.AddonLifecycle.UnregisterListener(eventType, addonName, existing.Handler);
            _addonListenerHandlers.TryRemove(key, out _);
        }
        else
            _addonListenerHandlers[key] = (existing.RefCount - 1, existing.Handler);
    }

    private void OnAddonEvent(AddonEvent type, AddonArgs args, string addonName, string eventTypeStr)
    {
        FrameworkLogger.Verbose($"[{nameof(OnAddonEvent)}] fired [{addonName}, {eventTypeStr}, {type}, {args}]");
        Raise(TriggerEvent.OnAddonEvent, new Dictionary<string, object> { { "type", type }, { "args", args }, { "AddonName", addonName }, { "EventType", eventTypeStr } });
    }
    #endregion

    #region AutoRetainer
    /// <summary>
    /// Registers a macro with AutoRetainer as a character post process task.
    /// </summary>
    public void AttachAutoRetainer(IMacro macro)
    {
        if (_arApis.ContainsKey(macro.Id)) return;
        var api = new AutoRetainerApi();
        api.OnCharacterPostprocessStep += () => CheckCharacterPostProcess(macro, api);
        api.OnCharacterReadyToPostProcess += () => DoCharacterPostProcess(macro);
        _arApis[macro.Id] = api;
    }

    /// <summary>
    /// Unregisters a macro from AutoRetainer.
    /// </summary>
    public void DetachAutoRetainer(string macroId)
    {
        if (_arApis.TryRemove(macroId, out var api))
            api.Dispose();
    }

    /// <summary>
    /// Tells AutoRetainer that a macro's character post process has finished.
    /// </summary>
    public void FinishCharacterPostProcess(IMacro macro)
    {
        if (_arApis.TryGetValue(macro.Id, out var api))
        {
            FrameworkLogger.Info($"{macro.Name} character post process finished, calling FinishCharacterPostProcess()");
            api.FinishCharacterPostProcess();
        }
    }

    private static bool IsExcludedCharacter(IMacro macro)
    {
        if (!C.ARCharacterPostProcessExcludedCharacters.Any(x => x == Svc.PlayerState.ContentId))
            return false;
        FrameworkLogger.Info($"Skipping post process macro {macro.Name} for current character.");
        return true;
    }

    private static void CheckCharacterPostProcess(IMacro macro, AutoRetainerApi api)
    {
        if (!IsExcludedCharacter(macro))
            api.RequestCharacterPostprocess();
    }

    private void DoCharacterPostProcess(IMacro macro)
    {
        if (IsExcludedCharacter(macro)) return;
        FrameworkLogger.Info($"Executing post process macro {macro.Name} for current character.");
        Raise(TriggerEvent.OnAutoRetainerCharacterPostProcess, new Dictionary<string, object> { { "Id", Svc.PlayerState.ContentId }, { "Name", Svc.PlayerState.CharacterName } });
    }
    #endregion

    #region Game events
    private void OnFrameworkUpdate(IFramework framework)
    {
        if (Svc.Condition[ConditionFlag.InCombat])
        {
            if (_combatStart == 0)
            {
                _combatStart = DateTime.Now.Ticks;
                var startTimestamp = _combatStart;
                var opponents = Svc.Objects.Where(o => o.TargetObjectId == Player.Object?.GameObjectId).Select(o => new EntityWrapper(o));
                Raise(TriggerEvent.OnCombatStart, new { startTimestamp, opponents });
                FrameworkLogger.Verbose($"Combat started against {string.Join(", ", opponents.Select(o => o.Name))} at {startTimestamp}");
            }
        }
        else if (_combatStart != 0)
        {
            var endTimestamp = DateTime.Now.Ticks;
            var duration = TimeSpan.FromTicks(endTimestamp - _combatStart).TotalSeconds;
            _combatStart = 0;
            Raise(TriggerEvent.OnCombatEnd, new { endTimestamp, duration });
            FrameworkLogger.Verbose($"Combat ended at {endTimestamp} in {duration:F2} seconds");
        }

        if (DateTime.Now >= _nextPluginPoll)
        {
            _nextPluginPoll = DateTime.Now + PluginPollInterval;
            CheckActivePlugins();
        }

        Raise(TriggerEvent.OnUpdate);
    }

    private void CheckActivePlugins()
    {
        var current = Svc.PluginInterface.InstalledPlugins.Where(p => p.IsLoaded).Select(p => p.InternalName).ToHashSet();
        var changed = _activePlugins;
        changed.SymmetricExceptWith(current);
        _activePlugins = current;
        if (changed.Count == 0) return;

        var diffs = changed.Select(plugin => new PluginWrapper { Name = plugin, IsLoaded = current.Contains(plugin) }).OrderBy(p => !p.IsLoaded).ToList();
        var eventData = new Dictionary<string, object> { { "changedPlugins", diffs } };
        Raise(TriggerEvent.OnActivePluginsChanged, new { eventData });
        FrameworkLogger.Verbose($"[{nameof(TriggerEvent.OnActivePluginsChanged)}] fired [{string.Join(", ", diffs)}]");
    }

    private record class PluginWrapper
    {
        public required string Name;
        public bool IsLoaded;
        public override string ToString() => $"{Name}: {IsLoaded}";
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        FrameworkLogger.Verbose($"[{nameof(OnConditionChange)}] fired [{flag}, {value}]");
        Raise(TriggerEvent.OnConditionChange, new Dictionary<string, object> { { "flag", flag }, { "value", value } });
    }

    private void OnTerritoryChanged(uint territoryType)
    {
        FrameworkLogger.Verbose($"[{nameof(OnTerritoryChanged)}] fired [{territoryType}]");
        Raise(TriggerEvent.OnTerritoryChange, new Dictionary<string, object> { { "territoryType", territoryType } });
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        FrameworkLogger.Verbose($"[{nameof(OnChatMessage)}] fired [{message.LogKind}, {message.Timestamp}, {message.Sender}, {message.Message.TextValue}, {message.IsHandled}]");
        Raise(TriggerEvent.OnChatMessage, new Dictionary<string, object> { { "type", message.LogKind }, { "timestamp", message.Timestamp }, { "sender", message.Sender.TextValue }, { "message", message.Message.TextValue }, { "isHandled", message.IsHandled } });
    }

    private void OnLogin()
    {
        FrameworkLogger.Verbose($"[{nameof(OnLogin)}] fired");
        Raise(TriggerEvent.OnLogin);
    }

    private void OnLogout(int type, int code)
    {
        FrameworkLogger.Verbose($"[{nameof(OnLogout)}] fired [{type}, {code}]");
        Raise(TriggerEvent.OnLogout, new Dictionary<string, object> { { "type", type }, { "code", code } });
    }

    private void OnDutyStarted(IDutyStateEventArgs args)
    {
        FrameworkLogger.Verbose($"[{nameof(OnDutyStarted)}] fired [{args}]");
        Raise(TriggerEvent.OnDutyStarted);
    }

    private void OnDutyWiped(IDutyStateEventArgs args)
    {
        FrameworkLogger.Verbose($"[{nameof(OnDutyWiped)}] fired [{args}]");
        Raise(TriggerEvent.OnDutyWiped);
    }

    private void OnDutyCompleted(IDutyStateEventArgs args)
    {
        FrameworkLogger.Verbose($"[{nameof(OnDutyCompleted)}] fired [{args}]");
        Raise(TriggerEvent.OnDutyCompleted);
    }

    private unsafe long OnEmoteFuncDetour(IntPtr a1, GameObject* source, ushort emoteId, GameObjectId targetId, long a5)
    {
        FrameworkLogger.Verbose($"Emote performed: Source={source->NameString}, EmoteId={emoteId}, TargetId={targetId.Id}, a5={a5}");
        Raise(TriggerEvent.OnEmote, new Dictionary<string, object> { { "SourceId", source->EntityId }, { "SourceName", source->NameString }, { "EmoteId", emoteId }, { "TargetId", targetId } });
        return OnEmoteFuncHook!.Original(a1, source, emoteId, targetId, a5);
    }
    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
        Svc.Condition.ConditionChange -= OnConditionChange;
        Svc.ClientState.TerritoryChanged -= OnTerritoryChanged;
        Svc.Chat.ChatMessage -= OnChatMessage;
        Svc.ClientState.Login -= OnLogin;
        Svc.ClientState.Logout -= OnLogout;
        Svc.DutyState.DutyStarted -= OnDutyStarted;
        Svc.DutyState.DutyWiped -= OnDutyWiped;
        Svc.DutyState.DutyCompleted -= OnDutyCompleted;

        foreach (var (key, (_, handler)) in _addonListenerHandlers.ToList())
            Svc.AddonLifecycle.UnregisterListener(key.EventType, key.AddonName, handler);
        _addonListenerHandlers.Clear();

        _arApis.Values.Each(a => a.Dispose());
        _arApis.Clear();

        OnEmoteFuncHook?.Dispose();
    }
}
