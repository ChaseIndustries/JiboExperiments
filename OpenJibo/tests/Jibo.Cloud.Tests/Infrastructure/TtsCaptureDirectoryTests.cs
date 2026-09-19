using Jibo.Cloud.Application.Services;
using Jibo.Cloud.Infrastructure.DependencyInjection;
using Jibo.Cloud.Infrastructure.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jibo.Cloud.Tests.Infrastructure;

public sealed class TtsCaptureDirectoryTests
{
    [Fact]
    public void AddOpenJiboCloud_ResolvesDefaultTtsCaptureDirectoryUnderOpenJiboCaptures()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();
        var services = new ServiceCollection();
        services.AddOpenJiboCloud(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<TtsOptions>();

        var expected = CapturePathResolver.Resolve(
            "captures/tts",
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory);

        Assert.Equal(expected, options.CaptureDirectory);
        Assert.True(Path.IsPathRooted(options.CaptureDirectory));
        Assert.Equal("tts", Path.GetFileName(options.CaptureDirectory));
        Assert.Equal("captures", Directory.GetParent(options.CaptureDirectory)!.Name);
    }
}
