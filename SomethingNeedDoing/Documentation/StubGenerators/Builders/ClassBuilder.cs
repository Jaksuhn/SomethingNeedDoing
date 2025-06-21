namespace SomethingNeedDoing.Documentation.StubGenerators.Builders;

public static class ClassBuilder
{
    public static Builder AddClass(this Builder builder, string name)
    {
        return builder.AddLine("class", name);
    }

    public static Builder AddField(this Builder builder, string name, string type)
    {
        return builder.AddLine("field", name, type);
    }
}
