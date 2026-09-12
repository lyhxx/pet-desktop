using System.Windows;
using System.Windows.Media.Imaging;

namespace DesktopPet.Animation;

/// <summary>
/// 一张固定网格 Sprite Sheet。加载时一次性把 64 格切成 CroppedBitmap 并缓存，
/// 全部共享底层像素。帧号对外为 1-based（与美术规范一致）。
/// </summary>
public sealed class SpriteSheet
{
    private readonly CroppedBitmap[] _frames;

    public int Columns { get; }
    public int Rows { get; }
    public int CellWidth { get; }
    public int CellHeight { get; }
    public int FrameCount => _frames.Length;

    public SpriteSheet(string path, int columns, int rows)
    {
        if (columns <= 0 || rows <= 0)
            throw new ArgumentOutOfRangeException(nameof(columns), "网格行列必须为正数。");

        Columns = columns;
        Rows = rows;

        var atlas = new BitmapImage();
        atlas.BeginInit();
        atlas.UriSource = new Uri(path, UriKind.Absolute);
        atlas.CacheOption = BitmapCacheOption.OnLoad;
        atlas.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        atlas.EndInit();
        atlas.Freeze();

        CellWidth = atlas.PixelWidth / columns;
        CellHeight = atlas.PixelHeight / rows;

        _frames = new CroppedBitmap[columns * rows];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                var cropped = new CroppedBitmap(
                    atlas,
                    new Int32Rect(c * CellWidth, r * CellHeight, CellWidth, CellHeight));
                cropped.Freeze();
                _frames[r * columns + c] = cropped;
            }
        }
    }

    public BitmapSource GetFrame(int oneBasedIndex)
    {
        int index = oneBasedIndex - 1;
        if (index < 0 || index >= _frames.Length)
            throw new ArgumentOutOfRangeException(nameof(oneBasedIndex), oneBasedIndex,
                $"帧号超出范围：1~{_frames.Length}。");
        return _frames[index];
    }
}
