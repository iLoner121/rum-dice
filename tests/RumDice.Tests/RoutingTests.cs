using Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using RumDice.Core;
using RumDice.Framework;
using Xunit;

namespace RumDice.Tests;

public sealed class RoutingTests
{
    public sealed class EchoService { public string Echo(Post _) => "pong"; }
    private readonly Dictionary<List<KeyWordAttribute>, string> matches = new();
    private readonly Dictionary<string, MyMethodInfo> functions = new();
    private readonly Dictionary<AllType, List<MyMethodInfo>> listeners = new();
    private readonly RecordingSender sender = new();
    private readonly EchoService service = new();
    private static readonly MethodInfo Echo = typeof(EchoService).GetMethod(nameof(EchoService.Echo))!;

    private EventManager Create()
    {
        var data = InterfaceStub.Create<ICoreData>((m, _) => m.Name switch
        {
            "get_MinPriority" => 1, "get_MaxPriority" => 5,
            "get_MatchTable" => matches, "get_FuncTable" => functions,
            "get_ListenerTable" => listeners,
            _ => throw new InvalidOperationException(m.Name)
        });
        var services = InterfaceStub.Create<IServiceManager>((m, args) =>
            m.Name == "GetService" && (Type)args![0]! == typeof(EchoService) ? service
                : throw new InvalidOperationException(m.Name));
        var logger = InterfaceStub.Create<IRumLogger>((_, _) => null);
        return new EventManager(data, services, new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .BuildServiceProvider(), logger, sender.Client);
    }
    private void Add(string name, string keyword, int priority = 3, int scope = 2)
    {
        matches.Add(new() { new KeyWordAttribute(keyword, isFullMatch: true) }, name);
        functions.Add(name, new MyMethodInfo(Echo) { Priority = priority, Scope = scope });
    }

    [Fact] public void ExactCommandResolvesRealMethod()
    {
        Add("echo", "echo");
        Assert.True(Create().MatchFunc("echo", 1, out var method));
        Assert.Equal(Echo, method);
    }
    [Theory] [InlineData("unknown")] [InlineData("")] [InlineData("echo extra")]
    public void NonmatchingInputDoesNotResolve(string input)
    {
        Add("echo", "echo");
        Assert.False(Create().MatchFunc(input, 1, out var method));
        Assert.Null(method);
    }
    [Fact] public void GroupOnlyCommandIsExcludedFromPrivateRouting()
    {
        Add("echo", "echo", scope: 1);
        Assert.False(Create().MatchFunc("echo", 1, out _));
        Assert.True(Create().MatchFunc("echo", 3, out _));
    }
    [Fact] public void SmallerPriorityWinsEvenWhenRegisteredLater()
    {
        Add("low", "echo", 5); Add("high", "echo", 1);
        var high = typeof(string).GetMethod(nameof(string.ToString), Type.EmptyTypes)!;
        functions["high"].MethodInfo = high;
        Assert.True(Create().MatchFunc("echo", 1, out var method));
        Assert.Equal(high, method);
    }
    [Fact] public async Task EventInvokesHandlerAndRecordsReplyWithoutNetwork()
    {
        var kind = Enum.GetValues<AllType>()[0];
        listeners.Add(kind, new() { new MyMethodInfo(Echo) });
        var post = new GroupMsg { Msg = "echo" };
        await Create().HandleEvent(kind, post);
        var sent = Assert.Single(sender.Sent);
        Assert.Equal("pong", sent.Text); Assert.Same(post, sent.Context);
    }
    [Fact] public async Task EventWithoutListenersSendsNothing()
    {
        await Create().HandleEvent(Enum.GetValues<AllType>()[0], new GroupMsg());
        Assert.Empty(sender.Sent);
    }
}
