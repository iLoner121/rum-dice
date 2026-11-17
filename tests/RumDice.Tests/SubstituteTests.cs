using RumDice.Framework;
using Xunit;
namespace RumDice.Tests;
public sealed class SubstituteTests
{
    [Fact] public void ClockAdvancesOnlyWhenRequested()
    {
        var clock = new ManualClock(); var before = clock.UtcNow;
        Assert.Equal(DateTimeOffset.UnixEpoch, before);
        clock.Advance(TimeSpan.FromSeconds(30)); Assert.Equal(before.AddSeconds(30), clock.UtcNow);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromTicks(-1)));
    }
    [Fact] public async Task SenderCanReturnFailureWithoutRecordingDelivery()
    {
        var failure = new IOException("injected send failure"); var sender = new RecordingSender { Failure = failure };
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => sender.Client.SendMsgAsync("reply", new GroupMsg()).AsTask()));
        Assert.Empty(sender.Sent);
    }
    [Fact] public void StorageFailureIsExplicitAndRepeatable()
    {
        var storage = new FailingStorage();
        Assert.Same(storage.Failure, Assert.Throws<IOException>(() => storage.Data.ReadTxtFile("unused")));
        Assert.Same(storage.Failure, Assert.Throws<IOException>(() => storage.Data.SaveTxtFile("text", name: "unused")));
    }
}
