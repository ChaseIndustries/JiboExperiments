namespace Jibo.Cloud.Application.Audio;

/// <summary>
/// Detects end-of-speech from Opus VBR packet sizes in audio time.
/// Silence/comfort-noise packets collapse to tiny payloads; speech stays dense.
/// Uses audio-clock duration so network jitter cannot fake or hide silence.
/// Requires at least one prior speech packet so all-quiet / garbage buffers do not
/// spuriously finalize mid-utterance.
/// </summary>
public static class OpusSpeechActivityDetector
{
    /// <summary>Opus sample rate used for TOC frame duration calculations.</summary>
    public const int OpusSampleRate = 48_000;

    /// <summary>
    /// Packets at or below this many bytes-per-millisecond of audio are treated as silence.
    /// ~0.8 B/ms ≈ 6.4 kbps — below typical speech Opus bitrates, above empty DTX.
    /// </summary>
    public const double SilenceBytesPerMillisecond = 0.8;

    /// <summary>Absolute packet size below which a packet is always silence/CN.</summary>
    public const int AbsoluteSilencePacketBytes = 12;

    public static bool HasTrailingSilence(
        IReadOnlyList<byte[]> pages,
        TimeSpan requiredSilence,
        double silenceBytesPerMillisecond = SilenceBytesPerMillisecond)
    {
        if (requiredSilence <= TimeSpan.Zero) return false;

        var packets = OggOpusAudioNormalizer.EnumerateAudioPackets(pages).ToArray();
        if (packets.Length == 0) return false;

        var lastSpeechIndex = -1;
        for (var index = 0; index < packets.Length; index += 1)
        {
            if (!IsSilencePacket(packets[index], silenceBytesPerMillisecond))
                lastSpeechIndex = index;
        }

        // Never observed speech — do not treat an all-quiet buffer as end-of-speech.
        if (lastSpeechIndex < 0) return false;

        var requiredSamples = (ulong)Math.Ceiling(requiredSilence.TotalSeconds * OpusSampleRate);
        ulong trailingSilenceSamples = 0;
        for (var index = lastSpeechIndex + 1; index < packets.Length; index += 1)
            trailingSilenceSamples += packets[index].SampleCount;

        return trailingSilenceSamples >= requiredSamples;
    }

    public static TimeSpan MeasureTrailingSilence(
        IReadOnlyList<byte[]> pages,
        double silenceBytesPerMillisecond = SilenceBytesPerMillisecond)
    {
        var packets = OggOpusAudioNormalizer.EnumerateAudioPackets(pages).ToArray();
        var lastSpeechIndex = -1;
        for (var index = 0; index < packets.Length; index += 1)
        {
            if (!IsSilencePacket(packets[index], silenceBytesPerMillisecond))
                lastSpeechIndex = index;
        }

        if (lastSpeechIndex < 0) return TimeSpan.Zero;

        ulong trailingSilenceSamples = 0;
        for (var index = lastSpeechIndex + 1; index < packets.Length; index += 1)
            trailingSilenceSamples += packets[index].SampleCount;

        return TimeSpan.FromSeconds(trailingSilenceSamples / (double)OpusSampleRate);
    }

    public static bool IsSilencePacket(
        OpusAudioPacket packet,
        double silenceBytesPerMillisecond = SilenceBytesPerMillisecond)
    {
        if (packet.SampleCount == 0) return true;
        if (packet.ByteLength <= AbsoluteSilencePacketBytes) return true;

        var durationMs = packet.SampleCount * 1000.0 / OpusSampleRate;
        if (durationMs <= 0) return true;

        return packet.ByteLength / durationMs <= silenceBytesPerMillisecond;
    }
}
