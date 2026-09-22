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
///   --benchmark-shot PATH  save the last measured frame as a .bmp, so a run
///                          says what it drew and not only how fast
///   --benchmark-vsync      keep waiting for the display, which pins every
///                          frame to the refresh rate
///   --width N --height N   fixed framebuffer size
/// </summary>
class Benchmark
{
    public readonly bool Active;
    public readonly int MeasureFrames;
    public readonly int WarmupFrames;
    public readonly string OutputPath;
    public readonly string ReplayPath;
    public readonly string ShotPath;
    public readonly int Width = 1600;
    public readonly int Height = 800;
    public readonly bool VSync;

    private readonly double[] frameTimesMs;
    private readonly Stopwatch stopwatch = new Stopwatch();
    private int framesSeen;
    private bool finished;
    private bool wantsCapture;

    public Benchmark(string[] args)
    {
        MeasureFrames = IntArg(args, "--benchmark", 0);
        Active = MeasureFrames > 0;
        WarmupFrames = IntArg(args, "--benchmark-warmup", 60);
        OutputPath = StringArg(args, "--benchmark-out", null);
        ReplayPath = StringArg(args, "--benchmark-replay", null);
        ShotPath = StringArg(args, "--benchmark-shot", null);

        // Waiting for the display makes every frame the same length and says
        // nothing about the driver, so a run measures without it unless asked
        VSync = Array.IndexOf(args, "--benchmark-vsync") >= 0;
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

            if (i + 2 >= frameTimesMs.Length)
            {
                wantsCapture = ShotPath != null;
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

    /// <summary>Same as the window grab, but for one GL texture. For finding
    /// which pass in a frame went dark.</summary>
    public void CaptureTexture(BBOpenGL.BBG.Texture texture, string suffix)
    {
        if (!wantsCapture || ShotPath == null || texture == null)
        {
            return;
        }

        int w = texture.Width, h = texture.Height;
        byte[] pixels = new byte[w * h * 4];

        // glGetTextureImage on a render target is refused, so read it the way
        // any GL app would: attach it and glReadPixels
        OpenTK.Graphics.OpenGL.GL.Finish();

        int fbo = 0;
        OpenTK.Graphics.OpenGL.GL.CreateFramebuffers(1, ref fbo);
        OpenTK.Graphics.OpenGL.GL.NamedFramebufferTexture(fbo,
            OpenTK.Graphics.OpenGL.FramebufferAttachment.ColorAttachment0, texture.ID, 0);
        OpenTK.Graphics.OpenGL.GL.BindFramebuffer(OpenTK.Graphics.OpenGL.FramebufferTarget.Framebuffer, fbo);
        OpenTK.Graphics.OpenGL.GL.ReadBuffer(OpenTK.Graphics.OpenGL.ReadBufferMode.ColorAttachment0);
        OpenTK.Graphics.OpenGL.GL.PixelStorei(OpenTK.Graphics.OpenGL.PixelStoreParameter.PackAlignment, 1);
        OpenTK.Graphics.OpenGL.GL.ReadPixels(0, 0, w, h,
            OpenTK.Graphics.OpenGL.PixelFormat.Rgba,
            OpenTK.Graphics.OpenGL.PixelType.UnsignedByte, pixels);
        OpenTK.Graphics.OpenGL.GL.BindFramebuffer(OpenTK.Graphics.OpenGL.FramebufferTarget.Framebuffer, 0);
        OpenTK.Graphics.OpenGL.GL.DeleteFramebuffers(1, ref fbo);

        WriteBmp(Path.ChangeExtension(ShotPath, null) + "_" + suffix + ".bmp", pixels, w, h);
    }

    private static void WriteBmp(string path, byte[] rgba, int width, int height)
    {
        int rowBytes = width * 3;
        int padding = (4 - (rowBytes % 4)) % 4;
        int imageBytes = (rowBytes + padding) * height;

        using BinaryWriter w = new BinaryWriter(File.Create(path));
        w.Write((byte)0x42); w.Write((byte)0x4D);
        w.Write(54 + imageBytes);
        w.Write(0);
        w.Write(54);
        w.Write(40);
        w.Write(width);
        w.Write(height);
        w.Write((short)1);
        w.Write((short)24);
        w.Write(0);
        w.Write(imageBytes);
        w.Write(2835); w.Write(2835);
        w.Write(0); w.Write(0);

        byte[] row = new byte[rowBytes + padding];
        for (int y = 0; y < height; y++)
        {
            int src = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                row[x * 3 + 0] = rgba[src + x * 4 + 2];
                row[x * 3 + 1] = rgba[src + x * 4 + 1];
                row[x * 3 + 2] = rgba[src + x * 4 + 0];
            }
            w.Write(row);
        }
    }

    /// <summary>Reads the window back and writes a 24 bit BMP. Called before SwapBuffers.</summary>
    public void CaptureIfRequested(int width, int height)
    {
        if (!wantsCapture || width <= 0 || height <= 0)
        {
            return;
        }

        wantsCapture = false;

        byte[] pixels = new byte[width * height * 4];
        OpenTK.Graphics.OpenGL.GL.Finish();
        OpenTK.Graphics.OpenGL.GL.ReadBuffer(OpenTK.Graphics.OpenGL.ReadBufferMode.Back);
        OpenTK.Graphics.OpenGL.GL.PixelStorei(OpenTK.Graphics.OpenGL.PixelStoreParameter.PackAlignment, 1);
        OpenTK.Graphics.OpenGL.GL.ReadPixels(0, 0, width, height,
            OpenTK.Graphics.OpenGL.PixelFormat.Rgba,
            OpenTK.Graphics.OpenGL.PixelType.UnsignedByte, pixels);

        // BMP rows run bottom to top, which is the order glReadPixels already gives
        int rowBytes = width * 3;
        int padding = (4 - (rowBytes % 4)) % 4;
        int imageBytes = (rowBytes + padding) * height;

        using BinaryWriter w = new BinaryWriter(File.Create(ShotPath));
        w.Write((byte)0x42); w.Write((byte)0x4D);
        w.Write(54 + imageBytes);
        w.Write(0);
        w.Write(54);
        w.Write(40);
        w.Write(width);
        w.Write(height);
        w.Write((short)1);
        w.Write((short)24);
        w.Write(0);
        w.Write(imageBytes);
        w.Write(2835); w.Write(2835);
        w.Write(0); w.Write(0);

        byte[] row = new byte[rowBytes + padding];
        for (int y = 0; y < height; y++)
        {
            int src = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                row[x * 3 + 0] = pixels[src + x * 4 + 2];
                row[x * 3 + 1] = pixels[src + x * 4 + 1];
                row[x * 3 + 2] = pixels[src + x * 4 + 0];
            }
            w.Write(row);
        }

        Logger.Log(Logger.LogLevel.Info, $"Benchmark: wrote {ShotPath} ({width}x{height})");
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
