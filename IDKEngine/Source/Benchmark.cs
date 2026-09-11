using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using BBLogger;

namespace IDKEngine;

/// <summary>
/// Fixed-length timed run, for comparing one GL implementation against another.
///
/// Turn it on with --benchmark N. The window still opens and draws, but after
/// N frames the timings are printed and the app closes on its own, so a run is
/// repeatable and does not need a person at the keyboard.
///
///   --benchmark N          measure N frames, then quit
///   --benchmark-warmup N   frames to throw away first (default 60). Shader
///                          compilation and the first touch of every buffer
///                          land here and would otherwise dominate.
///   --benchmark-out PATH   also write the raw per-frame times as CSV
///   --benchmark-replay P   replay a recorded camera path so every run draws
///                          the same thing
///   --width N --height N   fixed framebuffer size
/// </summary>
class Benchmark
{
    public readonly bool Active;
    public readonly int MeasureFrames;
    public readonly int WarmupFrames;
    public readonly string OutputPath;
    public readonly string ReplayPath;
    public readonly int Width = 1600;
    public readonly int Height = 800;

    private readonly double[] frameTimesMs;
    private readonly Stopwatch stopwatch = new Stopwatch();
    private int framesSeen;
    private bool finished;

    public Benchmark(string[] args)
    {
        MeasureFrames = IntArg(args, "--benchmark", 0);
        Active = MeasureFrames > 0;
        WarmupFrames = IntArg(args, "--benchmark-warmup", 60);
        OutputPath = StringArg(args, "--benchmark-out", null);
        ReplayPath = StringArg(args, "--benchmark-replay", null);
        Width = IntArg(args, "--width", 1600);
        Height = IntArg(args, "--height", 800);

        frameTimesMs = new double[Math.Max(MeasureFrames, 0)];
    }

    /// <summary>Call once per frame, as late in the frame as possible.</summary>
    /// <returns>true when the run is over and the app should close.</returns>
    public bool OnFrame()
    {
        if (!Active || finished)
        {
            return finished;
        }

        if (framesSeen == WarmupFrames)
        {
            Logger.Log(Logger.LogLevel.Info, $"Benchmark: warmup done, measuring {MeasureFrames} frames");
            stopwatch.Restart();
        }
        else if (framesSeen > WarmupFrames)
        {
            int i = framesSeen - WarmupFrames - 1;
            if (i < frameTimesMs.Length)
            {
                frameTimesMs[i] = stopwatch.Elapsed.TotalMilliseconds;
                stopwatch.Restart();
            }

            if (i + 1 >= frameTimesMs.Length)
            {
                Report();
                finished = true;
                return true;
            }
        }

        framesSeen++;
        return false;
    }

    private void Report()
    {
        double[] sorted = (double[])frameTimesMs.Clone();
        Array.Sort(sorted);

        double total = 0.0;
        for (int i = 0; i < sorted.Length; i++)
        {
            total += sorted[i];
        }

        double mean = total / sorted.Length;

        Logger.Log(Logger.LogLevel.Info, "Benchmark results");
        Logger.Log(Logger.LogLevel.Info, $"  device    {BBOpenGL.BBG.GetDeviceInfo().Name}");
        Logger.Log(Logger.LogLevel.Info, $"  api       {BBOpenGL.BBG.GetContextInfo().APIName}");
        Logger.Log(Logger.LogLevel.Info, $"  frames    {sorted.Length} measured, {WarmupFrames} warmup");
        Logger.Log(Logger.LogLevel.Info, $"  mean      {mean:F3} ms  ({1000.0 / mean:F1} fps)");
        Logger.Log(Logger.LogLevel.Info, $"  min       {sorted[0]:F3} ms");
        Logger.Log(Logger.LogLevel.Info, $"  median    {Percentile(sorted, 0.50):F3} ms");
        Logger.Log(Logger.LogLevel.Info, $"  p95       {Percentile(sorted, 0.95):F3} ms");
        Logger.Log(Logger.LogLevel.Info, $"  p99       {Percentile(sorted, 0.99):F3} ms");
        Logger.Log(Logger.LogLevel.Info, $"  max       {sorted[sorted.Length - 1]:F3} ms");

        if (OutputPath != null)
        {
            StringBuilder csv = new StringBuilder();
            csv.AppendLine("frame,ms");
            for (int i = 0; i < frameTimesMs.Length; i++)
            {
                csv.Append(i.ToString(CultureInfo.InvariantCulture));
                csv.Append(',');
                csv.AppendLine(frameTimesMs[i].ToString("F4", CultureInfo.InvariantCulture));
            }

            try
            {
                File.WriteAllText(OutputPath, csv.ToString());
                Logger.Log(Logger.LogLevel.Info, $"  raw times written to {OutputPath}");
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.LogLevel.Error, $"Benchmark: could not write {OutputPath}: {ex.Message}");
            }
        }
    }

    private static double Percentile(double[] sorted, double p)
    {
        // sorted ascending; nearest-rank, which is what you want for a small sample
        int index = (int)Math.Ceiling(p * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    private static int IntArg(string[] args, string name, int fallback)
    {
        string value = StringArg(args, name, null);
        return value != null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            ? result
            : fallback;
    }

    private static string StringArg(string[] args, string name, string fallback)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return fallback;
    }
}
