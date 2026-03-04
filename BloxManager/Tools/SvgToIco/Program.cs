using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections.Generic;
using System.IO;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: SvgToIco <input.svg> <output.ico>");
            return 1;
        }

        var inputSvg = args[0];
        var outputIco = args[1];
        if (!File.Exists(inputSvg))
        {
            Console.Error.WriteLine($"Input SVG not found: {inputSvg}");
            return 2;
        }

        var sizes = new[] { 16, 32, 48, 64, 128, 256 };
        var pngBytes = new List<(int size, byte[] data)>();

        try
        {
            var svg = new SKSvg();
            using var svgStream = File.OpenRead(inputSvg);
            svg.Load(svgStream);
            var picture = svg.Picture;
            if (picture == null)
            {
                Console.Error.WriteLine("Failed to load SVG picture.");
                return 3;
            }

            float vbW = picture.CullRect.Width;
            float vbH = picture.CullRect.Height;

            foreach (var s in sizes)
            {
                using var surface = SKSurface.Create(new SKImageInfo(s, s, SKColorType.Bgra8888, SKAlphaType.Premul));
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);

                float scale = Math.Min(s / vbW, s / vbH);
                float dx = (s - vbW * scale) / 2f;
                float dy = (s - vbH * scale) / 2f;

                canvas.Translate(dx, dy);
                canvas.Scale(scale);
                canvas.DrawPicture(picture);
                canvas.Flush();

                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                pngBytes.Add((s, data.ToArray()));
            }

            using var fs = File.Create(outputIco);
            WriteIco(fs, pngBytes);
            Console.WriteLine($"ICO written: {outputIco}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            return 10;
        }
    }

    static void WriteIco(Stream output, List<(int size, byte[] data)> images)
    {
        // ICO header
        WriteUInt16(output, 0); // reserved
        WriteUInt16(output, 1); // type = icon
        WriteUInt16(output, (ushort)images.Count); // count

        // Reserve space for directory entries
        long dirStart = output.Position;
        int entrySize = 16;
        output.Position = dirStart + images.Count * entrySize;

        // Write image data and record offsets
        var offsets = new List<(int size, int length, int offset)>();
        foreach (var (size, data) in images)
        {
            int offset = (int)output.Position;
            output.Write(data, 0, data.Length);
            offsets.Add((size, data.Length, offset));
        }

        // Write directory entries
        output.Position = dirStart;
        foreach (var (size, length, offset) in offsets)
        {
            output.WriteByte((byte)(size == 256 ? 0 : size)); // width
            output.WriteByte((byte)(size == 256 ? 0 : size)); // height
            output.WriteByte(0); // color count
            output.WriteByte(0); // reserved
            WriteUInt16(output, 1); // planes
            WriteUInt16(output, 32); // bitcount
            WriteUInt32(output, (uint)length); // bytes in resource
            WriteUInt32(output, (uint)offset); // offset
        }
    }

    static void WriteUInt16(Stream s, ushort v)
    {
        s.WriteByte((byte)(v & 0xFF));
        s.WriteByte((byte)((v >> 8) & 0xFF));
    }
    static void WriteUInt32(Stream s, uint v)
    {
        s.WriteByte((byte)(v & 0xFF));
        s.WriteByte((byte)((v >> 8) & 0xFF));
        s.WriteByte((byte)((v >> 16) & 0xFF));
        s.WriteByte((byte)((v >> 24) & 0xFF));
    }
}
