using Jibo.Cloud.Application.Services;
using Jibo.Cloud.Infrastructure.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Jibo.Cloud.Tests.Infrastructure;

public sealed class SshAplayKitchenAudioPlayerTests
{
    [Fact]
    public void IsEnabled_IsFalseWhenKitchenPlayIsOff()
    {
        var player = new SshAplayKitchenAudioPlayer(
            new TtsOptions { EnableKitchenPlay = false, RobotIp = "192.168.4.24" },
            Mock.Of<IExternalProcessRunner>(),
            NullLogger<SshAplayKitchenAudioPlayer>.Instance);

        Assert.False(player.IsEnabled);
    }

    [Fact]
    public async Task PlayAsync_InvokesPlayScriptWithRobotIp()
    {
        string? fileName = null;
        IReadOnlyList<string>? arguments = null;
        var runner = new Mock<IExternalProcessRunner>();
        runner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalProcessResult(0, "", ""))
            .Callback<string, IReadOnlyList<string>, CancellationToken>((name, args, _) =>
            {
                fileName = name;
                arguments = args;
            });

        var player = new SshAplayKitchenAudioPlayer(
            new TtsOptions
            {
                EnableKitchenPlay = true,
                RobotIp = "192.168.4.24",
                RobotSshUser = "root"
            },
            runner.Object,
            NullLogger<SshAplayKitchenAudioPlayer>.Instance);

        await player.PlayAsync([1, 2, 3, 4], "audio/wav");

        Assert.False(string.IsNullOrWhiteSpace(fileName));
        Assert.NotNull(arguments);
        Assert.Contains("192.168.4.24", arguments!);
        Assert.Contains("--jibo-ip", arguments!);
        Assert.EndsWith("play-on-jibo.py", arguments![0], StringComparison.Ordinal);
        runner.Verify(
            r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
