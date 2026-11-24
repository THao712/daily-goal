using System.Media;

namespace DailyGoal;

internal enum FeedbackSoundKind
{
    TaskComplete,
    AllDone,
    FocusComplete
}

/// <summary>
/// 纯内存合成轻柔提示音（极短正弦包络 WAV），零外部音频文件依赖。
/// </summary>
internal static class SoundFeedback
{
    private const int SampleRate = 22050;
    private static readonly Dictionary<FeedbackSoundKind, byte[]> Cache = [];

    public static void Play(FeedbackSoundKind kind, bool enabled)
    {
        if (!enabled)
        {
            return;
        }

        try
        {
            var wav = GetOrCreateWav(kind);
            var stream = new MemoryStream(wav, writable: false);
            var player = new SoundPlayer(stream);
            // 异步播放避免卡住 UI 线程；播放完毕后由 GC 回收小缓冲区。
            player.Play();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.IO.IOException or TimeoutException)
        {
        }
    }

    internal static byte[] GetOrCreateWav(FeedbackSoundKind kind)
    {
        if (Cache.TryGetValue(kind, out var existing))
        {
            return existing;
        }

        var notes = kind switch
        {
            FeedbackSoundKind.AllDone => new (double Freq, double StartSec, double DurationSec, double Gain)[]
            {
                (523.25, 0.00, 0.16, 0.22), // C5
                (659.25, 0.09, 0.18, 0.22), // E5
                (783.99, 0.18, 0.22, 0.24), // G5
                (1046.50, 0.27, 0.34, 0.20) // C6
            },
            FeedbackSoundKind.FocusComplete => new (double Freq, double StartSec, double DurationSec, double Gain)[]
            {
                (659.25, 0.00, 0.26, 0.22), // E5
                (880.00, 0.16, 0.42, 0.22)  // A5
            },
            _ => new (double Freq, double StartSec, double DurationSec, double Gain)[]
            {
                (587.33, 0.00, 0.11, 0.20), // D5
                (880.00, 0.07, 0.20, 0.18)  // A5
            }
        };

        var totalSeconds = notes.Max(note => note.StartSec + note.DurationSec) + 0.03;
        var sampleCount = (int)Math.Ceiling(totalSeconds * SampleRate);
        var buffer = new double[sampleCount];

        foreach (var (freq, startSec, durationSec, gain) in notes)
        {
            var startIndex = (int)(startSec * SampleRate);
            var length = Math.Max(1, (int)(durationSec * SampleRate));
            var attackSamples = Math.Max(1, (int)(0.012 * SampleRate));
            for (var index = 0; index < length && startIndex + index < sampleCount; index += 1)
            {
                var t = (double)index / SampleRate;
                var attack = index < attackSamples ? (double)index / attackSamples : 1.0;
                var decay = Math.Pow(1.0 - (double)index / length, 1.9);
                var sample = (Math.Sin(2.0 * Math.PI * freq * t) * 0.82
                    + Math.Sin(4.0 * Math.PI * freq * t) * 0.18) * gain * attack * decay;
                buffer[startIndex + index] += sample;
            }
        }

        var dataBytes = sampleCount * 2;
        using var output = new MemoryStream(44 + dataBytes);
        using var writer = new BinaryWriter(output);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write((short)1); // Mono
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(dataBytes);
        for (var index = 0; index < sampleCount; index += 1)
        {
            var clamped = Math.Clamp(buffer[index], -0.95, 0.95);
            writer.Write((short)Math.Round(clamped * short.MaxValue));
        }

        var bytes = output.ToArray();
        Cache[kind] = bytes;
        return bytes;
    }
}
