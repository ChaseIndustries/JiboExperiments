namespace Jibo.Cloud.Application.Services;

public interface IRobotKitchenAudioPlayer
{
    bool IsEnabled { get; }

    Task PlayAsync(byte[] audio, string contentType, CancellationToken cancellationToken = default);
}

public sealed class NullRobotKitchenAudioPlayer : IRobotKitchenAudioPlayer
{
    public static NullRobotKitchenAudioPlayer Instance { get; } = new();

    public bool IsEnabled => false;

    public Task PlayAsync(byte[] audio, string contentType, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
