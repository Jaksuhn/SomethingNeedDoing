using SomethingNeedDoing.Documentation.StubGenerators.Builders;
using System.IO;
using System.Text;

namespace SomethingNeedDoing.Documentation.StubGenerators;

internal sealed class StubFile
{
    private readonly string[] pathParts;
    private readonly List<Builder> builders = [];

    public StubFile(params string[] pathParts)
    {
        if (pathParts == null || pathParts.Length == 0)
        {
            throw new ArgumentException("Must provide at least one path part", nameof(pathParts));
        }

        this.pathParts = pathParts;
    }

    public void AddBuilder(Builder builder) => builders.Add(builder);

    public void PrependBuilder(Builder builder) => builders.Insert(0, builder);

    public void Write()
    {
        var path = GetStubPath(pathParts);

        var sb = new StringBuilder();
        for (int i = 0; i < builders.Count; i++)
        {
            sb.Append(builders[i].ToString());

            if (i != builders.Count - 1)
            {
                sb.AppendLine();
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString());
    }

    private static string GetStubPath(string[] parts)
    {
        return Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "stubs", Path.Combine(parts));
    }
}
