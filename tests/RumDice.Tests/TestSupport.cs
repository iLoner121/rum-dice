using System.Reflection;
using RumDice.Core;
using RumDice.Framework;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RumDice.Tests;

// Strict, test-only interface replacement. Unexpected calls fail immediately.
public class InterfaceStub : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Call { get; set; } =
        (method, _) => throw new InvalidOperationException($"Unexpected call: {method.Name}");
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Call(method!, args);
    public static T Create<T>(Func<MethodInfo, object?[]?, object?> call) where T : class
    {
        var value = DispatchProxy.Create<T, InterfaceStub>();
        ((InterfaceStub)(object)value).Call = call;
        return value;
    }
}

public sealed class ManualClock
{
    public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UnixEpoch;
    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        UtcNow += elapsed;
    }
}

public sealed class RecordingSender
{
    public List<(string Text, Post Context)> Sent { get; } = new();
    public Exception? Failure { get; set; }
    public IClientCPanel Client => InterfaceStub.Create<IClientCPanel>((method, args) =>
    {
        if (method.Name != nameof(IClientCPanel.SendMsgAsync) || args![0] is not string text)
            throw new InvalidOperationException($"Unexpected send overload: {method}");
        if (Failure is not null) return new ValueTask(Task.FromException(Failure));
        Sent.Add((text, (Post)args[1]!));
        return ValueTask.CompletedTask;
    });
}

// Reusable IDataCenter fault substitute; no credentials or actual file access.
public sealed class FailingStorage
{
    public IOException Failure { get; } = new("injected file failure");
    public IDataCenter Data => InterfaceStub.Create<IDataCenter>((_, _) => throw Failure);
}

public sealed class TemporaryDirectory : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "rumdice-tests-" + Guid.NewGuid().ToString("N"));
    public TemporaryDirectory() => Directory.CreateDirectory(Root);
    public void Dispose() => Directory.Delete(Root, recursive: true);
}

// Legacy static dependencies are restored even if assertions fail. No CoreData constructor runs.
public sealed class StaticScope : IDisposable
{
    private readonly List<(FieldInfo Field, object? Original)> originals = new();
    public void Set<T>(object? value) => Set(typeof(T), value);
    public void Set(Type type, object? value)
    {
        var field = type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing legacy singleton: {type.Name}");
        originals.Add((field, field.GetValue(null)));
        field.SetValue(null, value);
    }
    public void Capture<T>()
    {
        var type = typeof(T);
        var field = type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)!;
        originals.Add((field, field.GetValue(null)));
    }
    public void Dispose()
    {
        foreach (var (field, original) in originals.AsEnumerable().Reverse()) field.SetValue(null, original);
    }
}
