namespace SomethingNeedDoing.Documentation.StubGenerators.Builders;

public static class FunctionBuilder
{
    public static Builder AddParam(this Builder builder, string name, string type)
    {
        return builder.AddLine($"--- @param {name} {type}");
    }

    public static Builder AddReturn(this Builder builder, string type)
    {
        return builder.AddLine($"--- @return {type}");
    }

    public static Builder AddFunction(this Builder builder, string name, (string name, string type)[] parameters, string returnType)
    {
        foreach (var (paramName, paramType) in parameters)
            builder.AddParam(paramName, paramType);

        builder.AddReturn(returnType);
        var paramList = string.Join(", ", parameters.Select(p => p.name));
        builder.AddLine($"function {name}({paramList}) end");

        return builder;
    }
}
