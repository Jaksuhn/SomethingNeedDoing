using SomethingNeedDoing.Documentation.StubGenerators.Builders;
using System.IO;
using System.Text;

namespace SomethingNeedDoing.Documentation.StubGenerators;

internal abstract class StubGenerator
{
    private readonly List<string> documentation = [];

    protected abstract StubFile GenerateStub();

    public StubFile GetStubFile()
    {
        var stub = GenerateStub();

        if (documentation.Count > 0)
        {
            var builder = new Builder();
            foreach (var line in documentation)
            {
                builder.AddLine($"-- {line}");
            }

            stub.PrependBuilder(builder);
        }

        return stub;
    }

    protected string GetStubPath(string filename) => Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "stubs", filename);

    protected string GetStubPath(params string[] pathSegments)
    {
        var baseDir = Svc.PluginInterface.ConfigDirectory.FullName;
        var allSegments = new List<string> { baseDir, "stubs" };
        allSegments.AddRange(pathSegments);
        return Path.Combine([.. allSegments]);
    }

    public StubGenerator WithDocumentationLine(string line)
    {
        documentation.Add(line);
        return this;
    }

    protected string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLower(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
