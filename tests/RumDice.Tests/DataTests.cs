using RumDice.Framework;
using Xunit;

namespace RumDice.Tests;

public sealed class DataTests
{
    public sealed class Document { public string Text { get; set; } = ""; }
    private static DataCenter Create(string root)
    {
        var services = InterfaceStub.Create<IServiceManager>((m, args) =>
        {
            if (m.Name != "GetStruct" || (string)args![0]! != nameof(Document))
                throw new InvalidOperationException(m.Name);
            args[1] = typeof(Document); return new Document();
        });
        var data = new DataCenter(services, InterfaceStub.Create<IRumLogger>((_, _) => null));
        var settings = new AppSetting(); settings.FileConfig.RepositoryRoot = "";
        data.Initialize(settings, root);
        return data;
    }
    [Fact] public void JsonRoundTripPreservesTextAndMetadata()
    {
        using var temp = new TemporaryDirectory(); using var statics = new StaticScope();
        statics.Capture<DataCenter>();
        var root = Path.Combine(temp.Root, "repository"); Directory.CreateDirectory(root);
        var data = Create(root);
        Assert.True(data.SaveFile(new Document { Text = "朗姆骰 Unicode" }, name: "sample"));
        var loaded = Assert.IsType<Document>(data.GetObj("\\sample.json"));
        Assert.Equal("朗姆骰 Unicode", loaded.Text);
        var json = File.ReadAllText(root + "\\sample.json");
        Assert.Contains("\"fileType\":\"Document\"", json);
        Assert.Contains("\"readType\":1", json);
    }
    [Fact] public void MissingFileReturnsFalseAndNull()
    {
        using var temp = new TemporaryDirectory(); using var statics = new StaticScope();
        statics.Capture<DataCenter>(); var data = Create(Path.Combine(temp.Root, "repository"));
        Assert.False(data.TryGetObj("\\missing.json", out var result)); Assert.Null(result);
    }
    [Fact] public void DirectoryAtOutputPathReturnsWriteFailure()
    {
        using var temp = new TemporaryDirectory(); using var statics = new StaticScope();
        statics.Capture<DataCenter>(); var root = Path.Combine(temp.Root, "repository");
        Directory.CreateDirectory(root); Directory.CreateDirectory(root + "\\blocked.txt");
        Assert.False(Create(root).SaveTxtFile("text", name: "blocked"));
        Assert.True(Directory.Exists(root + "\\blocked.txt"));
    }
    [Fact] public void TypedReadWithoutMetadataUsesProvidedType()
    {
        using var temp = new TemporaryDirectory(); using var statics = new StaticScope();
        statics.Capture<DataCenter>(); var root = Path.Combine(temp.Root, "repository");
        Directory.CreateDirectory(root);
        File.WriteAllText(root + "\\custom.json", "{\"Text\":\"custom\"}");
        var result = Assert.IsType<Document>(Create(root).CustomRead("custom", typeof(Document)));
        Assert.Equal("custom", result.Text);
    }
}
