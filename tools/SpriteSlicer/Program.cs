using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

const int Grid = 8;
const int Cell = 256;
const int Size = 2048;

if (args.Length >= 2 && args[0] == "measure")
{
    Measure(args[1]);
    return 0;
}

if (args.Length >= 2 && args[0] == "cleanup")
{
    Cleanup(args[1]);
    return 0;
}

if (args.Length >= 3 && args[0] == "scale")
{
    ScaleAtlas(args[1], double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture));
    return 0;
}

if (args.Length >= 3 && args[0] == "makeico")
{
    MakeIco(args[1], args[2]);
    return 0;
}

if (args.Length < 3)
{
    Console.WriteLine("usage: SpriteSlicer <input.png> <atlasOut.png> <framesDir>");
    return 1;
}

string input = Path.GetFullPath(args[0]);
string atlasOut = Path.GetFullPath(args[1]);
string framesDir = Path.GetFullPath(args[2]);

using var source = new Bitmap(input);
Console.WriteLine($"source: {source.Width} x {source.Height}");

using var atlas = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
using (var g = Graphics.FromImage(atlas))
{
    g.CompositingMode = CompositingMode.SourceCopy;
    g.CompositingQuality = CompositingQuality.HighQuality;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.SmoothingMode = SmoothingMode.HighQuality;
    g.DrawImage(source, new Rectangle(0, 0, Size, Size));
}

CleanAlpha(atlas, 40);
CleanBoundaries(atlas, 8);
RemoveTopStray(atlas, 16);
NormalizeAnchors(atlas);

Directory.CreateDirectory(Path.GetDirectoryName(atlasOut)!);
atlas.Save(atlasOut, ImageFormat.Png);
Console.WriteLine($"atlas: {atlasOut}");

Directory.CreateDirectory(framesDir);
for (int r = 0; r < Grid; r++)
{
    for (int c = 0; c < Grid; c++)
    {
        using var cell = new Bitmap(Cell, Cell, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(cell))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(atlas,
                new Rectangle(0, 0, Cell, Cell),
                new Rectangle(c * Cell, r * Cell, Cell, Cell),
                GraphicsUnit.Pixel);
        }

        int index = r * Grid + c + 1;
        cell.Save(Path.Combine(framesDir, index.ToString("D2") + ".png"), ImageFormat.Png);
    }
}
Console.WriteLine($"frames: {framesDir} (64)");

SavePreview(atlas, Path.Combine(Path.GetDirectoryName(framesDir)!, "_preview_Idle.png"));
Console.WriteLine("done");
return 0;

static void Measure(string path)
{
    using var bmp = new Bitmap(path);
    int cw = bmp.Width / Grid;
    int ch = bmp.Height / Grid;

    var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
        ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    int stride = data.Stride;
    byte[] buf = new byte[stride * bmp.Height];
    Marshal.Copy(data.Scan0, buf, 0, buf.Length);
    bmp.UnlockBits(data);

    Console.WriteLine("idx  cx  feetY   w   h");

    for (int r = 0; r < Grid; r++)
    {
        for (int c = 0; c < Grid; c++)
        {
            int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;

            for (int y = 0; y < ch; y++)
            {
                int gy = r * ch + y;
                int rowOff = gy * stride;
                for (int x = 0; x < cw; x++)
                {
                    int gx = c * cw + x;
                    if (buf[rowOff + gx * 4 + 3] <= 16) continue;
                    if (gx < minX) minX = gx;
                    if (gx > maxX) maxX = gx;
                    if (gy < minY) minY = gy;
                    if (gy > maxY) maxY = gy;
                }
            }

            int idx = r * Grid + c + 1;
            if (maxX < 0)
            {
                Console.WriteLine($"{idx:D2}: (empty)");
                continue;
            }

            int cx = (minX + maxX) / 2 - c * cw;
            int feetY = maxY - r * ch;
            Console.WriteLine($"{idx:D2}: cx={cx,3} feetY={feetY,3} w={maxX - minX,3} h={maxY - minY,3}");
        }
    }
}

static void NormalizeAnchors(Bitmap bmp)
{
    var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
        ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
    int stride = data.Stride;
    byte[] src = new byte[stride * bmp.Height];
    Marshal.Copy(data.Scan0, src, 0, src.Length);
    byte[] dst = (byte[])src.Clone();

    for (int r = 0; r < Grid; r++)
    {
        for (int c = 0; c < Grid; c++)
        {
            int ox = c * Cell, oy = r * Cell;
            int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;

            for (int y = 0; y < Cell; y++)
            {
                int rowOff = (oy + y) * stride;
                for (int x = 0; x < Cell; x++)
                {
                    if (src[rowOff + (ox + x) * 4 + 3] <= 16) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0) continue;

            int height = maxY - minY + 1;
            int bandStart = maxY - (int)(height * 0.15);
            long sumX = 0;
            int count = 0;

            for (int y = bandStart; y <= maxY; y++)
            {
                int rowOff = (oy + y) * stride;
                for (int x = 0; x < Cell; x++)
                {
                    if (src[rowOff + (ox + x) * 4 + 3] <= 16) continue;
                    sumX += x;
                    count++;
                }
            }

            double anchorX = count > 0 ? (double)sumX / count : (minX + maxX) / 2.0;
            double anchorY = maxY;

            int dx = (int)Math.Round(128 - anchorX);
            int dy = (int)Math.Round(236 - anchorY);

            for (int y = 0; y < Cell; y++)
            {
                int rowOff = (oy + y) * stride;
                for (int x = 0; x < Cell; x++)
                {
                    int i = rowOff + (ox + x) * 4;
                    dst[i] = 0;
                    dst[i + 1] = 0;
                    dst[i + 2] = 0;
                    dst[i + 3] = 0;
                }
            }

            for (int y = 0; y < Cell; y++)
            {
                int sy = y - dy;
                if (sy < 0 || sy >= Cell) continue;
                int srcRow = (oy + sy) * stride;
                int dstRow = (oy + y) * stride;
                for (int x = 0; x < Cell; x++)
                {
                    int sx = x - dx;
                    if (sx < 0 || sx >= Cell) continue;
                    int si = srcRow + (ox + sx) * 4;
                    int di = dstRow + (ox + x) * 4;
                    dst[di] = src[si];
                    dst[di + 1] = src[si + 1];
                    dst[di + 2] = src[si + 2];
                    dst[di + 3] = src[si + 3];
                }
            }
        }
    }

    Marshal.Copy(dst, 0, data.Scan0, dst.Length);
    bmp.UnlockBits(data);
}

static void MakeIco(string input, string output)
{
    int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
    using var src = new Bitmap(input);

    var entries = new List<(int Size, byte[] Data)>();
    foreach (int s in sizes)
    {
        using var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.DrawImage(src, new Rectangle(0, 0, s, s));
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        entries.Add((s, ms.ToArray()));
    }

    using var fs = new FileStream(output, FileMode.Create, FileAccess.Write);
    using var w = new BinaryWriter(fs);

    w.Write((ushort)0);
    w.Write((ushort)1);
    w.Write((ushort)entries.Count);

    int offset = 6 + entries.Count * 16;
    foreach (var (size, data) in entries)
    {
        w.Write((byte)(size >= 256 ? 0 : size));
        w.Write((byte)(size >= 256 ? 0 : size));
        w.Write((byte)0);
        w.Write((byte)0);
        w.Write((ushort)1);
        w.Write((ushort)32);
        w.Write(data.Length);
        w.Write(offset);
        offset += data.Length;
    }

    foreach (var (_, data) in entries)
        w.Write(data);

    Console.WriteLine($"ico: {output} ({entries.Count} sizes)");
}

static void CleanAlpha(Bitmap bmp, byte threshold)
{
    Rectangle rect = new(0, 0, bmp.Width, bmp.Height);
    BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
    int bytes = Math.Abs(data.Stride) * bmp.Height;
    byte[] buffer = new byte[bytes];
    Marshal.Copy(data.Scan0, buffer, 0, bytes);

    for (int i = 0; i + 3 < bytes; i += 4)
    {
        if (buffer[i + 3] < threshold)
        {
            buffer[i] = 0;
            buffer[i + 1] = 0;
            buffer[i + 2] = 0;
            buffer[i + 3] = 0;
        }
    }

    Marshal.Copy(buffer, 0, data.Scan0, bytes);
    bmp.UnlockBits(data);
}

static void CleanBoundaries(Bitmap bmp, int margin)
{
    Rectangle rect = new(0, 0, bmp.Width, bmp.Height);
    BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
    int stride = data.Stride;
    int bytes = stride * bmp.Height;
    byte[] buffer = new byte[bytes];
    Marshal.Copy(data.Scan0, buffer, 0, bytes);

    for (int y = 0; y < bmp.Height; y++)
    {
        int ry = y % Cell;
        bool clearRow = ry < margin || ry > Cell - 1 - margin;
        int rowOffset = y * stride;

        for (int x = 0; x < bmp.Width; x++)
        {
            int rx = x % Cell;
            if (!clearRow && rx >= margin && rx <= Cell - 1 - margin)
                continue;

            int i = rowOffset + x * 4;
            buffer[i] = 0;
            buffer[i + 1] = 0;
            buffer[i + 2] = 0;
            buffer[i + 3] = 0;
        }
    }

    Marshal.Copy(buffer, 0, data.Scan0, bytes);
    bmp.UnlockBits(data);
}

/// <summary>
/// 按图集统一缩放（以脚部锚点 128,236 为中心），用于对齐不同图集之间的角色大小。
/// AI 生成的不同图集常整体大小不一，切换图集时会“放大/缩小”。
/// </summary>
static void ScaleAtlas(string path, double factor)
{
    string tmp = path + ".tmp.png";
    using (var src = new Bitmap(path))
    using (var outBmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb))
    {
        for (int r = 0; r < Grid; r++)
        {
            for (int c = 0; c < Grid; c++)
            {
                int ox = c * Cell, oy = r * Cell;

                using var cell = new Bitmap(Cell, Cell, PixelFormat.Format32bppArgb);
                using (var cg = Graphics.FromImage(cell))
                {
                    cg.CompositingMode = CompositingMode.SourceCopy;
                    cg.DrawImage(src,
                        new Rectangle(0, 0, Cell, Cell),
                        new Rectangle(ox, oy, Cell, Cell),
                        GraphicsUnit.Pixel);
                }

                using var g = Graphics.FromImage(outBmp);
                g.CompositingMode = CompositingMode.SourceOver;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;

                // 以锚点为中心缩放该格
                g.TranslateTransform(ox + 128, oy + 236);
                g.ScaleTransform((float)factor, (float)factor);
                g.TranslateTransform(-(ox + 128), -(oy + 236));
                g.DrawImage(cell, ox, oy);
            }
        }

        outBmp.Save(tmp, ImageFormat.Png);
    }

    File.Delete(path);
    File.Move(tmp, path);
    Console.WriteLine($"scaled x{factor:F4}: {path}");
}

static void Cleanup(string path)
{
    // 就地在已加载的位图上处理，保存到临时文件后替换，避免 GDI+ 复制导致的像素改动。
    string tmp = path + ".tmp.png";
    using (var bmp = new Bitmap(path))
    {
        RemoveTopStray(bmp, 16);
        bmp.Save(tmp, ImageFormat.Png);
    }

    File.Delete(path);
    File.Move(tmp, path);
    Console.WriteLine($"cleaned: {path}");
}

/// <summary>
/// 去掉每个格子顶部与主体分离的杂块（AI 原图里相邻行溢出的脚 / 边角）。
/// 只删「不是最大连通块、且触及顶部 band」的组件，保留爱心 / 问号 / Zzz 等悬浮元素。
/// </summary>
static void RemoveTopStray(Bitmap bmp, int band)
{
    Rectangle rect = new(0, 0, bmp.Width, bmp.Height);
    BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
    int stride = data.Stride;
    int bytes = stride * bmp.Height;
    byte[] buffer = new byte[bytes];
    Marshal.Copy(data.Scan0, buffer, 0, bytes);

    bool[] visited = new bool[Cell * Cell];
    int[] stack = new int[Cell * Cell];
    var components = new List<List<int>>();

    for (int r = 0; r < Grid; r++)
    {
        for (int c = 0; c < Grid; c++)
        {
            int ox = c * Cell, oy = r * Cell;
            Array.Clear(visited, 0, visited.Length);
            components.Clear();

            for (int y = 0; y < Cell; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    int idx = y * Cell + x;
                    if (visited[idx]) continue;
                    visited[idx] = true;
                    if (buffer[(oy + y) * stride + (ox + x) * 4 + 3] <= 16) continue;

                    var members = new List<int>();
                    int sp = 0;
                    stack[sp++] = idx;
                    while (sp > 0)
                    {
                        int cur = stack[--sp];
                        members.Add(cur);
                        int cy = cur / Cell, cx = cur % Cell;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int ny = cy + dy, nx = cx + dx;
                                if (ny < 0 || ny >= Cell || nx < 0 || nx >= Cell) continue;
                                int ni = ny * Cell + nx;
                                if (visited[ni]) continue;
                                visited[ni] = true;
                                if (buffer[(oy + ny) * stride + (ox + nx) * 4 + 3] <= 16) continue;
                                stack[sp++] = ni;
                            }
                        }
                    }
                    components.Add(members);
                }
            }

            if (components.Count < 2) continue;

            int biggest = 0;
            for (int i = 1; i < components.Count; i++)
                if (components[i].Count > components[biggest].Count) biggest = i;

            for (int i = 0; i < components.Count; i++)
            {
                if (i == biggest) continue;

                int minY = Cell;
                foreach (int m in components[i])
                {
                    int my = m / Cell;
                    if (my < minY) minY = my;
                }
                if (minY >= band) continue;

                foreach (int m in components[i])
                {
                    int p = (oy + m / Cell) * stride + (ox + m % Cell) * 4;
                    buffer[p] = 0; buffer[p + 1] = 0; buffer[p + 2] = 0; buffer[p + 3] = 0;
                }
            }
        }
    }

    Marshal.Copy(buffer, 0, data.Scan0, bytes);
    bmp.UnlockBits(data);
}

static void SavePreview(Bitmap atlas, string path)
{
    using var preview = new Bitmap(atlas);
    using var g = Graphics.FromImage(preview);
    using var line = new Pen(Color.FromArgb(120, 60, 120, 255), 2);
    using var font = new Font("Segoe UI", 20, FontStyle.Bold);
    using var brush = new SolidBrush(Color.FromArgb(200, 200, 40, 40));

    for (int i = 0; i <= Grid; i++)
    {
        int p = i * Cell;
        g.DrawLine(line, p, 0, p, Size);
        g.DrawLine(line, 0, p, Size, p);
    }

    for (int r = 0; r < Grid; r++)
    {
        for (int c = 0; c < Grid; c++)
        {
            int index = r * Grid + c + 1;
            g.DrawString(index.ToString("D2"), font, brush, c * Cell + 8, r * Cell + 6);
        }
    }

    preview.Save(path, ImageFormat.Png);
}
