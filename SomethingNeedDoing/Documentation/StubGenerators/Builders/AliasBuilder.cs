namespace SomethingNeedDoing.Documentation.StubGenerators.Builders;

public static class AliasBuilder
{
    public static Builder AddAlias(this Builder builder, string original, string alias)
    {
        return builder.AddLine("alias", original, alias);
    }
}
