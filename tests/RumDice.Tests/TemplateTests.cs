using RumDice.Core;
using RumDice.Framework;
using Xunit;

namespace RumDice.Tests;

public sealed class TemplateTests
{
    private static StaticScope Configure()
    {
        var scope = new StaticScope();
        try
        {
            var words = new ReturnWordTable();
            words.table.Add("test.echo", new() { ["default"] = "你好 {0}!", ["named"] = "你好 {name}!" });
            scope.Set<CoreData>(InterfaceStub.Create<ICoreData>((m, _) => m.Name == "get_Setting"
                ? new AppSetting() : throw new InvalidOperationException(m.Name)));
            scope.Set<DataCenter>(InterfaceStub.Create<IDataCenter>((m, _) => m.Name == "GetObj"
                ? words : throw new InvalidOperationException(m.Name)));
            scope.Set<RumLogger>(InterfaceStub.Create<IRumLogger>((_, _) => null));
            return scope;
        }
        catch { scope.Dispose(); throw; }
    }
    [Fact] public void PositionalTemplateReplacesArgument()
    {
        using var scope = Configure();
        Assert.Equal("你好 世界!", new MsgTool().GenerateMsg("test.echo", new List<string> { "世界" }));
    }
    [Fact] public void NamedTemplateSelectsRequestedVariant()
    {
        using var scope = Configure();
        Assert.Equal("你好 朗姆!", new MsgTool().GenerateMsg("test.echo", new Dictionary<string, string> { ["name"] = "朗姆" }, "named"));
    }
    [Fact] public void LiteralArgumentIsPreserved()
    {
        using var scope = Configure();
        Assert.Equal("你好 {unknown}!", new MsgTool().GenerateMsg("test.echo", new List<string> { "{unknown}" }));
    }
    [Fact] public void ReplyKeepsGroupPlatformAndDestination()
    {
        var post = new GroupMsg { MsgType = MsgType.Group, BotType = BotType.QQbot, GroupID = 42 };
        var send = new MsgTool().MakeSend("reply", post);
        Assert.IsType<OneBotSend>(send); Assert.Equal("reply", send.Msg);
        Assert.Equal(42L, send.GroupID); Assert.Equal(BotType.QQbot, send.BotType);
    }
}
