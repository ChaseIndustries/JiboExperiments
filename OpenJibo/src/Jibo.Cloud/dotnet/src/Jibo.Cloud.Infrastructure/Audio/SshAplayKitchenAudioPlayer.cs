using Jibo.Cloud.Application.Services;
using Microsoft.Extensions.Logging;

namespace Jibo.Cloud.Infrastructure.Audio;

public sealed class SshAplayKitchenAudioPlayer(
    TtsOptions options,
    IExternalProcessRunner processRunner,
    ILogger<SshAplayKitchenAudioPlayer> logger) : IRobotKitchenAudioPlayer
{
    public bool IsEnabled =>
        options.EnableKitchenPlay && !string.IsNullOrWhiteSpace(options.RobotIp);

    public async Task PlayAsync(byte[] audio, string contentType, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return;

        var python = ResolvePython();
        var script = ResolveScript();
        var extension = contentType.Contains("wav", StringComparison.OrdinalIgnoreCase) ? ".wav" : ".mp3";
        var localPath = Path.Combine(Path.GetTempPath(), $"openjibo-kitchen-{Guid.NewGuid():N}{extension}");
        await File.WriteAllBytesAsync(localPath, audio, cancellationToken);
        try
        {
            logger.LogInformation(
                "Kitchen clone play start robotIp={RobotIp} bytes={Bytes}",
                options.RobotIp,
                audio.Length);
            await processRunner.RunAsync(
                python,
                [
                    script,
                    "--jibo-ip",
                    options.RobotIp!,
                    "--ssh-user",
                    string.IsNullOrWhiteSpace(options.RobotSshUser) ? "root" : options.RobotSshUser,
                    localPath
                ],
                cancellationToken);
        }
        finally
        {
            if (File.Exists(localPath))
                File.Delete(localPath);
        }
    }

    private static string ResolvePython()
    {
        var venv = Path.Combine(Directory.GetCurrentDirectory(), "tts", ".venv", "bin", "python");
        return File.Exists(venv) ? venv : "python3";
    }

    private static string ResolveScript()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "tts", "play-on-jibo.py"),
            Path.Combine(AppContext.BaseDirectory, "tts", "play-on-jibo.py")
        };
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var nested = Path.Combine(directory.FullName, "tts", "play-on-jibo.py");
            if (File.Exists(nested))
                return nested;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find tts/play-on-jibo.py.");
    }
}
