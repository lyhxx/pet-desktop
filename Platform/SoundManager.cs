using System.IO;
using System.Media;
using System.Text;

namespace DesktopPet.Platform;

/// <summary>
/// 音效。第一阶段没有音频资源，改为运行时合成短促音调（不引入第三方、不依赖素材）。
/// 后续接入真实音效时只需替换这里的合成逻辑。
/// </summary>
public sealed class SoundManager
{
    private const int SampleRate = 22050;

    public bool Enabled { get; set; } = true;
    public double Volume { get; set; } = 0.6;

    public void PlayFeed() => Play((720, 0.09), (940, 0.12));

    public void PlayPet() => Play((620, 0.10), (820, 0.10), (1020, 0.14));

    private void Play(params (double Freq, double Seconds)[] tones)
    {
        if (!Enabled || Volume <= 0) return;

        try
        {
            using Stream wav = BuildWav(tones, Math.Clamp(Volume, 0, 1));
            var player = new SoundPlayer(wav);
            player.Load();
            player.Play();
        }
        catch
        {
            // 音频不可用时静默忽略。
        }
    }

    private static Stream BuildWav((double Freq, double Seconds)[] tones, double volume)
    {
        var samples = new List<short>();

        foreach (var (freq, seconds) in tones)
        {
            int count = Math.Max(1, (int)(SampleRate * seconds));
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / SampleRate;
                double envelope = Math.Sin(Math.PI * i / count);
                double value = Math.Sin(2 * Math.PI * freq * t) * envelope * volume;
                samples.Add((short)(value * short.MaxValue));
            }
        }

        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            int dataLength = samples.Count * 2;

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);

            foreach (short sample in samples)
                writer.Write(sample);
        }

        stream.Position = 0;
        return stream;
    }
}
