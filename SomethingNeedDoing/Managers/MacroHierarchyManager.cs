using SomethingNeedDoing.Core.Interfaces;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Managers;

/// <summary>
/// Manages the hierarchy and relationships between macros and their temporary children.
/// </summary>
[RegisterSingleton, AutoConstruct]
public partial class MacroHierarchyManager
{
    private readonly ConcurrentDictionary<string, MacroNode> _macroNodes = [];
    private readonly ConcurrentDictionary<string, string> _parentLookup = [];

    /// <summary>
    /// Represents a node in the macro hierarchy.
    /// </summary>
    private class MacroNode(IMacro macro)
    {
        public IMacro Macro { get; } = macro;
        public List<IMacro> Children { get; } = [];
        public TaskCompletionSource<bool>? CompletionSource { get; set; }
    }

    /// <summary>
    /// Registers a temporary macro as a child of a parent macro.
    /// </summary>
    /// <param name="parentMacro">The parent macro that spawned the temporary macro.</param>
    /// <param name="temporaryMacro">The temporary macro to register.</param>
    /// <returns>The registered temporary macro.</returns>
    public IMacro RegisterTemporaryMacro(IMacro parentMacro, IMacro temporaryMacro)
    {
        var parentNode = _macroNodes.GetOrAdd(parentMacro.Id, _ => new MacroNode(parentMacro));
        var childNode = _macroNodes.GetOrAdd(temporaryMacro.Id, _ => new MacroNode(temporaryMacro));

        parentNode.Children.Add(temporaryMacro);
        _parentLookup[temporaryMacro.Id] = parentMacro.Id;

        // Set up error propagation
        temporaryMacro.StateChanged += (_, e) =>
        {
            if (e.NewState == MacroState.Error)
            {
                parentMacro.State = MacroState.Error;
                parentNode.CompletionSource?.TrySetResult(false);
            }
        };

        FrameworkLogger.Verbose($"Registered temporary macro {temporaryMacro.Id} with parent {parentMacro.Id}");
        return temporaryMacro;
    }

    /// <summary>
    /// Gets the parent macro of a temporary macro.
    /// </summary>
    /// <param name="temporaryMacroId">The ID of the temporary macro.</param>
    /// <returns>The parent macro, or null if not found.</returns>
    public IMacro? GetParentMacro(string temporaryMacroId)
        => _parentLookup.TryGetValue(temporaryMacroId, out var parentId) && _macroNodes.TryGetValue(parentId, out var parentNode) ? parentNode.Macro : null;

    /// <summary>
    /// Gets the root parent macro (the original macro that started the chain).
    /// This traverses up the hierarchy until it finds a non-temporary macro.
    /// </summary>
    /// <param name="temporaryMacroId">The ID of the temporary macro.</param>
    /// <returns>The root parent macro, or null if not found.</returns>
    public IMacro? GetRootParentMacro(string temporaryMacroId)
    {
        var currentId = temporaryMacroId;
        while (_parentLookup.TryGetValue(currentId, out var parentId))
        {
            if (_macroNodes.TryGetValue(parentId, out var parentNode))
            {
                if (parentNode.Macro is not TemporaryMacro)
                    return parentNode.Macro;
                currentId = parentId;
            }
            else
                break;
        }
        return null;
    }

    /// <summary>
    /// Gets all child macros of a parent macro.
    /// </summary>
    /// <param name="parentMacroId">The ID of the parent macro.</param>
    /// <returns>A list of child macros.</returns>
    public IReadOnlyList<IMacro> GetChildMacros(string parentMacroId) => _macroNodes.TryGetValue(parentMacroId, out var node) ? node.Children : [];

    /// <summary>
    /// Unregisters a temporary macro and cleans up its relationships.
    /// </summary>
    /// <param name="temporaryMacroId">The ID of the temporary macro to unregister.</param>
    public void UnregisterTemporaryMacro(string temporaryMacroId)
    {
        if (_parentLookup.TryRemove(temporaryMacroId, out var parentId) && _macroNodes.TryGetValue(parentId, out var parentNode))
            parentNode.Children.RemoveAll(m => m.Id == temporaryMacroId);
        _macroNodes.TryRemove(temporaryMacroId, out _);
    }

    /// <summary>
    /// Unregisters a macro and all its children.
    /// </summary>
    /// <param name="macroId">The ID of the macro to unregister.</param>
    public void UnregisterMacro(string macroId)
    {
        if (_macroNodes.TryRemove(macroId, out var node))
            foreach (var child in node.Children)
                UnregisterTemporaryMacro(child.Id);
    }
}
