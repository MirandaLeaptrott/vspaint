using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace VSpaint
{
    // Canvas is always 64x64 stored 4bpp (2 pixels per byte = 2048 bytes raw).
    // Index 0 is the background/blank colour (white).
    public static class PaintingUtil
    {
        public const int Width  = 64;
        public const int Height = 64;
        public const int PixelCount = Width * Height;
        public const int EncodedSize = PixelCount / 2;

        // 16-colour ARGB palette, A=255 always. Index 0 is the blank/background slot.
        public static readonly int[] Palette = new int[]
        {
            unchecked((int)0xFFFFFFFF), // 0  White (blank / background)
            unchecked((int)0xFF000000), // 1  Black
            unchecked((int)0xFF404040), // 2  Dark Grey
            unchecked((int)0xFFA0A0A0), // 3  Light Grey
            unchecked((int)0xFFCC0000), // 4  Red
            unchecked((int)0xFF800000), // 5  Dark Red
            unchecked((int)0xFFFF8800), // 6  Orange
            unchecked((int)0xFFFFEE00), // 7  Yellow
            unchecked((int)0xFF00BB00), // 8  Green
            unchecked((int)0xFF006400), // 9  Dark Green
            unchecked((int)0xFF00CCCC), // 10 Cyan
            unchecked((int)0xFF0055FF), // 11 Blue
            unchecked((int)0xFF00008B), // 12 Dark Blue
            unchecked((int)0xFFAA00AA), // 13 Purple
            unchecked((int)0xFF8B4513), // 14 Brown
            unchecked((int)0xFFFFB6C1), // 15 Pink
        };

        public static byte[] EncodePixels(int[] indices)
        {
            if (indices == null) return null;
            byte[] data = new byte[EncodedSize];
            for (int i = 0; i < PixelCount; i++)
            {
                int idx = indices[i] & 0xF;
                if (i % 2 == 0)
                    data[i / 2] = (byte)(idx << 4);
                else
                    data[i / 2] |= (byte)(idx & 0xF);
            }
            return data;
        }

        public static int[] DecodePixels(byte[] data)
        {
            int[] indices = new int[PixelCount];
            if (data == null || data.Length < EncodedSize) return indices;
            for (int i = 0; i < PixelCount; i++)
            {
                indices[i] = (i % 2 == 0)
                    ? (data[i / 2] >> 4) & 0xF
                    : data[i / 2] & 0xF;
            }
            return indices;
        }

        // Returns null if encoded is null or too short. PNG is hand-rolled so we
        // don't drag in an image library just for 64x64 RGB writes.
        public static byte[] PixelsToPng(byte[] encoded)
        {
            if (encoded == null || encoded.Length < EncodedSize) return null;

            // Scanline layout: filter-byte (0 = None) + RGB per pixel per row.
            int rowStride = 1 + Width * 3;
            byte[] scanlines = new byte[Height * rowStride];

            for (int y = 0; y < Height; y++)
            {
                scanlines[y * rowStride] = 0;
                for (int x = 0; x < Width; x++)
                {
                    int pixIdx  = y * Width + x;
                    int colIdx  = (pixIdx % 2 == 0)
                        ? (encoded[pixIdx / 2] >> 4) & 0xF
                        : encoded[pixIdx / 2] & 0xF;

                    int argb = Palette[colIdx];
                    int off  = y * rowStride + 1 + x * 3;
                    scanlines[off]     = (byte)((argb >> 16) & 0xFF);
                    scanlines[off + 1] = (byte)((argb >>  8) & 0xFF);
                    scanlines[off + 2] = (byte)( argb        & 0xFF);
                }
            }

            byte[] idatData;
            using (var ms = new MemoryStream())
            {
                using (var zlib = new ZLibStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
                    zlib.Write(scanlines, 0, scanlines.Length);
                idatData = ms.ToArray();
            }

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                // PNG signature
                bw.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

                // IHDR: w, h, 8-bit depth, color type 2 (RGB), deflate, adaptive filter, no interlace.
                WriteChunk(bw, "IHDR", new byte[]
                {
                    0, 0, 0, (byte)Width,
                    0, 0, 0, (byte)Height,
                    8, 2, 0, 0, 0
                });

                WriteChunk(bw, "IDAT", idatData);
                WriteChunk(bw, "IEND", Array.Empty<byte>());

                return ms.ToArray();
            }
        }

        private static void WriteChunk(BinaryWriter bw, string typeStr, byte[] data)
        {
            byte[] type = Encoding.ASCII.GetBytes(typeStr);

            // PNG chunks are big-endian; BinaryWriter would write little-endian.
            int len = data.Length;
            bw.Write((byte)(len >> 24));
            bw.Write((byte)(len >> 16));
            bw.Write((byte)(len >>  8));
            bw.Write((byte) len);

            bw.Write(type);
            bw.Write(data);

            // CRC-32 covers type + data per PNG spec.
            uint crc = Crc32Init;
            foreach (byte b in type)  crc = Crc32Update(crc, b);
            foreach (byte b in data)  crc = Crc32Update(crc, b);
            crc ^= 0xFFFFFFFF;

            bw.Write((byte)(crc >> 24));
            bw.Write((byte)(crc >> 16));
            bw.Write((byte)(crc >>  8));
            bw.Write((byte) crc);
        }

        // ISO-HDLC / PNG variant. Table-less; fine for 64x64.
        private const uint Crc32Init = 0xFFFFFFFF;
        private static uint Crc32Update(uint crc, byte b)
        {
            crc ^= b;
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            return crc;
        }

        public static string PaletteName(int index)
        {
            switch (index)
            {
                case  0: return "White";
                case  1: return "Black";
                case  2: return "Dark Grey";
                case  3: return "Light Grey";
                case  4: return "Red";
                case  5: return "Dark Red";
                case  6: return "Orange";
                case  7: return "Yellow";
                case  8: return "Green";
                case  9: return "Dark Green";
                case 10: return "Cyan";
                case 11: return "Blue";
                case 12: return "Dark Blue";
                case 13: return "Purple";
                case 14: return "Brown";
                case 15: return "Pink";
                default: return "?";
            }
        }
    }
}
