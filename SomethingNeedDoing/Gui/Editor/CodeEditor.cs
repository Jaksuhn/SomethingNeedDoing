using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using DalamudCodeEditor;
using SomethingNeedDoing.Core.Interfaces;
using System.Threading;
using System.Threading.Tasks;
using TextEditor = DalamudCodeEditor.TextEditor.Editor;

namespace SomethingNeedDoing.Gui.Editor;

/// <summary>
/// DalamudCodeEditor TextEditor wrapper.
/// </summary>
public class CodeEditor : IDisposable
{
    private readonly LuaLanguageDefinition _lua;
    private readonly TextEditor _editor = new();
    private readonly MetadataParser _metadataParser;
    private readonly Dictionary<MacroType, LanguageDefinition> _languages;

    private IMacro? macro = null;
    private CancellationTokenSource? _debounceCts;
    private bool _lastIsDirty = false;

    public CodeEditor(LuaLanguageDefinition lua, MetadataParser metadataParser)
    {
        _lua = lua;
        _metadataParser = metadataParser;
        _languages = new() { { MacroType.Lua, _lua }, { MacroType.Native, new NativeMacroLanguageDefinition() } };
        Config.ConfigFileChanged += RefreshContent;
    }

    public int Lines => _editor.Buffer.LineCount;
    public bool ReadOnly
    {
        get => _editor.IsReadOnly;
        set => _editor.SetReadOnly(value);
    }

    public bool IsHighlightingSyntax
    {
        get => _editor.Colorizer.Enabled;
        set => _editor.Colorizer.SetEnabled(value);
    }

    public bool IsShowingWhitespace
    {
        get => _editor.Style.ShowWhitespace;
        set => _editor.Style.SetShowWhitespace(value);
    }

    public bool IsShowingLineNumbers
    {
        get => _editor.Style.ShowLineNumbers;
        set => _editor.Style.SetShowLineNumbers(value);
    }

    public int Column => _editor.Cursor.GetPosition().Column;

    public void SetMacro(IMacro macro)
    {
        if (this.macro?.Id == macro.Id)
            return;

        this.macro = macro;
        _editor.Buffer.SetText(macro.Content);
        _editor.UndoManager.Clear();

        if (_languages.TryGetValue(macro.Type, out var language))
            _editor.Language = language;
    }

    public void RefreshContent()
    {
        if (macro != null)
            _editor.Buffer.SetText(macro.Content);
    }

    public string GetContent() => _editor.Buffer.GetText();

    public bool Draw()
    {
        if (macro == null)
            return false;

        using var font = ImRaii.PushFont(UiBuilder.MonoFont);
        _editor.Draw(macro.Name);

        var isDirty = _editor.Buffer.IsDirty;
        if (isDirty && !_lastIsDirty && macro is ConfigMacro configMacro)
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();

            _ = Task.Delay(500, _debounceCts.Token).ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully && !_debounceCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (macro?.Id == configMacro.Id)
                        {
                            var content = GetContent();
                            if (MetadataParser.MetadataBlockRegex.IsMatch(content))
                            {
                                var newMetadata = _metadataParser.ParseMetadata(content);

                                if (configMacro.Metadata.TriggerEvents is { Count: > 0 } existingEvents)
                                    newMetadata.TriggerEvents = existingEvents;

                                if (!Equals(configMacro.Metadata, newMetadata))
                                {
                                    configMacro.Metadata = newMetadata;
                                    C.Save();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        FrameworkLogger.Error(ex, "Failed to auto-parse metadata");
                    }
                }

                return Task.CompletedTask;
            }, _debounceCts.Token);
        }

        _lastIsDirty = isDirty;
        return isDirty;
    }

    public void Dispose()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        Config.ConfigFileChanged -= RefreshContent;
    }

}
