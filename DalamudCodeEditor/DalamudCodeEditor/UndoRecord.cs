using DalamudCodeEditor.TextEditor;

namespace DalamudCodeEditor;

public class UndoRecord
{
    internal State Before;

    internal State After;

    internal string BeforeText;

    internal string AfterText;

    private UndoRecord()
    {
    }

    public void Undo(Editor editor)
    {
        editor.Buffer.SetText(BeforeText);
        editor.State = Before;
    }

    public void Redo(Editor editor)
    {
        editor.Buffer.SetText(AfterText);
        editor.State = After;
    }

    public static UndoRecord Create(Editor editor, Action change)
    {
        var record = new UndoRecord
        {
            Before = editor.State.Clone(),
            BeforeText = editor.Buffer.GetText()
        };

        change();

        record.After = editor.State.Clone();
        record.AfterText = editor.Buffer.GetText();

        return record;
    }
}
