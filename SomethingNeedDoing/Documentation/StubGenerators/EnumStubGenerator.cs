using SomethingNeedDoing.Documentation.StubGenerators;
using SomethingNeedDoing.Documentation.StubGenerators.Builders;

internal sealed class EnumStubGenerator : StubGenerator
{
    private readonly Type _enumType;

    public EnumStubGenerator(Type enumType)
    {
        if (!enumType.IsEnum)
        {
            throw new ArgumentException("Type must be an enum.", nameof(enumType));
        }

        _enumType = enumType;
    }

    protected override StubFile GenerateStub()
    {
        var filename = ToSnakeCase(_enumType.Name);
        var file = new StubFile("enums", $"{filename}.lua");

        file.AddBuilder(new Builder().AddEnum(_enumType));

        return file;
    }
}
