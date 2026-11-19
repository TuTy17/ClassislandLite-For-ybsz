using System;
using System.Collections.Generic;
using System.IO;

namespace ClassIsland.Helpers;

/// <summary>
/// 生成提醒音效的 WAV 数据（两段短促的和弦提示音），不需要额外音频文件。
/// </summary>
public static class ReminderTone
{
    private const int SampleRate = 44100;

    /// <summary>
    /// 生成提示音 WAV 流：两段 0.18 秒的“叮—咚”，间隔 0.08 秒。
    /// </summary>
    public static Stream CreateStream()
    {
        var samples = new List<short>();
        AppendTone(samples, 880, 0.18);
        AppendSilence(samples, 0.08);
        AppendTone(samples, 1174.66, 0.22);
        AppendSilence(samples, 0.1);

        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true);
        var dataSize = samples.Count * 2;

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);              // PCM 头长度
        writer.Write((short)1);        // PCM
        writer.Write((short)1);        // 单声道
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);  // 每秒字节数
        writer.Write((short)2);        // 块对齐
        writer.Write((short)16);       // 位深
        writer.Write("data"u8.ToArray());
        writer.Write(dataSize);
        foreach (var sample in samples)
        {
            writer.Write(sample);
        }

        writer.Flush();
        memory.Position = 0;
        return new MemoryStream(memory.ToArray());
    }

    private static void AppendTone(List<short> samples, double frequency, double seconds)
    {
        var count = (int)(SampleRate * seconds);
        for (var i = 0; i < count; i++)
        {
            var t = (double)i / SampleRate;
            // 包络：快速起音 + 指数衰减，避免爆音
            var envelope = Math.Min(1.0, i / (SampleRate * 0.012)) * Math.Exp(-3.2 * t / Math.Max(0.01, seconds));
            var value = Math.Sin(2 * Math.PI * frequency * t) * 0.72 * envelope;
            samples.Add((short)(value * short.MaxValue));
        }
    }

    private static void AppendSilence(List<short> samples, double seconds)
    {
        var count = (int)(SampleRate * seconds);
        for (var i = 0; i < count; i++)
        {
            samples.Add(0);
        }
    }
}
