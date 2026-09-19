namespace Jibo.Cloud.Application.Services;

public sealed record TtsClip(string Id, string ContentType, byte[] Audio);

public interface ITtsClipCache
{
    void Put(string id, string contentType, byte[] audio);
    bool TryGet(string id, out TtsClip clip);
}

public sealed class InMemoryTtsClipCache : ITtsClipCache
{
    private readonly Dictionary<string, TtsClip> _clips = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public void Put(string id, string contentType, byte[] audio)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (_gate)
        {
            _clips[id] = new TtsClip(id, string.IsNullOrWhiteSpace(contentType) ? "audio/mpeg" : contentType, audio);
        }
    }

    public bool TryGet(string id, out TtsClip clip)
    {
        lock (_gate)
        {
            return _clips.TryGetValue(id, out clip!);
        }
    }
}
