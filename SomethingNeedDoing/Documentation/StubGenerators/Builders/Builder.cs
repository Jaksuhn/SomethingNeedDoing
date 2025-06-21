using System.Text;

namespace SomethingNeedDoing.Documentation.StubGenerators.Builders;

public class Builder
{
    protected string LineTemplate = "--- @[type] [arg1] [arg2]";

    protected StringBuilder sb = new();

    public Builder AddLine(string line)
    {
        sb.AppendLine(line.Trim());
        return this;
    }

    public Builder AddLine(string type, string arg1) => AddLine(type, arg1, string.Empty);

    public Builder AddLine(string type, string arg1, string arg2)
    {
        var line = LineTemplate
            .Replace("[type]", type)
            .Replace("[arg1]", arg1)
            .Replace("[arg2]", arg2);

        sb.AppendLine(line.Trim());

        return this;
    }

    public override string ToString() => sb.ToString();
}
