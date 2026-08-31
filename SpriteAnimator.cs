using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MonkeyPet;

sealed class SpriteAnimator
{
    sealed class Manifest
    {
        public int CellWidth { get; set; }
        public int CellHeight { get; set; }
        public Dictionary<string, Clip> Clips { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    sealed class Clip
    {
        public int Row { get; set; }
        public int Columns { get; set; } = 1;
        public int Fps { get; set; } = 8;
        public bool Loop { get; set; } = true;
    }

    readonly BitmapSource _sheet;
    readonly Manifest _manifest;
    readonly Dictionary<string, BitmapSource[]> _cache = new(StringComparer.OrdinalIgnoreCase);
    BitmapSource[] _frames = [];
    string _name = "idle";
    double _time;
    int _index;

    public bool IsLoaded { get; }
    public BitmapSource Current => _frames.Length == 0 ? _sheet : _frames[_index];
    public string CurrentClip => _name;

    private SpriteAnimator(BitmapSource sheet, Manifest manifest)
    {
        _sheet = sheet;
        _manifest = manifest;
        IsLoaded = manifest.CellWidth > 0 && manifest.CellHeight > 0 && manifest.Clips.Count > 0;
        SetClip("idle");
    }

    public static SpriteAnimator? TryLoad()
    {
        try
        {
            var manifestUri = new Uri("pack://application:,,,/Assets/monkey-actions.json");
            using var stream = System.Windows.Application.GetResourceStream(manifestUri)?.Stream;
            if (stream is null) return null;
            var manifest = JsonSerializer.Deserialize<Manifest>(stream) ?? new Manifest();
            var sheet = new BitmapImage(new Uri("pack://application:,,,/Assets/monkey-atlas.png"));
            return new SpriteAnimator(sheet, manifest);
        }
        catch
        {
            return null;
        }
    }

    public bool SetClip(string name)
    {
        if (!_manifest.Clips.TryGetValue(name, out var clip))
            return false;
        if (!_cache.TryGetValue(name, out var cached))
        {
            var count = Math.Max(1, clip.Columns);
            var frames = new BitmapSource[count];
            for (var column = 0; column < count; column++)
            {
                frames[column] = new CroppedBitmap(_sheet,
                    new Int32Rect(column * _manifest.CellWidth, clip.Row * _manifest.CellHeight,
                        _manifest.CellWidth, _manifest.CellHeight));
                frames[column].Freeze();
            }
            _frames = frames;
            _cache[name] = frames;
        }
        else
        {
            _frames = cached;
        }
        _name = name;
        _time = 0;
        _index = 0;
        return true;
    }

    public BitmapSource Tick(double seconds)
    {
        if (_frames.Length <= 1) return Current;
        var clip = _manifest.Clips[_name];
        _time += Math.Max(0, seconds);
        var frameDuration = 1.0 / Math.Max(1, clip.Fps);
        while (_time >= frameDuration)
        {
            _time -= frameDuration;
            if (_index + 1 < _frames.Length) _index++;
            else if (clip.Loop) _index = 0;
        }
        return Current;
    }
}
