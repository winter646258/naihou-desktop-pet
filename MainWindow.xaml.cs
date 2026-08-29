using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace MonkeyPet;

public partial class MainWindow : Window
{
    const int GwlExStyle = -20, Transparent = 0x20, ToolWindow = 0x80, NoActivate = 0x08000000;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    readonly DispatcherTimer behavior = new() { Interval = TimeSpan.FromSeconds(3) };
    readonly PetSettings settings = PetSettings.Load();
    readonly Random random = new();
    readonly Forms.NotifyIcon tray = new();
    BitmapSource? bitmap;
    readonly Dictionary<string, BitmapSource> frames = new();
    readonly MediaPlayer call = new();
    IntPtr hwnd; bool dragging, interactive, paused, calling;
    string motion = "idle";
    System.Windows.Point dragMouse, dragWindow;

    public MainWindow()
    {
        InitializeComponent(); Loaded += LoadedPet; SourceInitialized += (_, _) => NativeSetup();
        MouseLeftButtonDown += LeftDown; MouseMove += MovePet; MouseLeftButtonUp += (_, _) => EndDrag();
        MouseRightButtonUp += (_, _) => PlayCall(); Closed += (_, _) => { tray.Dispose(); call.Close(); };
    }

    void LoadedPet(object? _, RoutedEventArgs e)
    {
        frames["front"] = LoadFrame("front.png"); frames["side"] = LoadFrame("side.png"); frames["back"] = LoadFrame("back.png"); SetFrame("front"); ApplySize();
        Left = double.IsNaN(settings.Left) ? SystemParameters.WorkArea.Width * .08 : settings.Left;
        Top = double.IsNaN(settings.Top) ? SystemParameters.WorkArea.Height * .55 : settings.Top;
        call.MediaEnded += (_, _) => calling = false; call.MediaFailed += (_, _) => calling = false;
        BuildTray(); timer.Tick += (_, _) => HitTestMouse(); behavior.Tick += (_, _) => Behave(); timer.Start(); behavior.Start(); if (settings.Hidden || settings.StartMinimized) Hide();
    }
    void NativeSetup() { hwnd = new WindowInteropHelper(this).Handle; SetWindowLong(hwnd, GwlExStyle, GetWindowLong(hwnd, GwlExStyle) | ToolWindow | NoActivate | Transparent); }
    void BuildTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示 / 隐藏猴子", null, (_, _) => Toggle());
        menu.Items.Add("暂停动作", null, (_, _) => paused = !paused);
        menu.Items.Add("设置", null, (_, _) => SettingsDialog());
        menu.Items.Add("退出程序", null, (_, _) => { settings.Hidden = false; settings.Save(); tray.Visible = false; System.Windows.Application.Current.Shutdown(); });
        tray.Icon = System.Drawing.SystemIcons.Application; tray.Text = "奶猴桌宠"; tray.ContextMenuStrip = menu; tray.Visible = true; tray.DoubleClick += (_, _) => Toggle();
    }
    void Toggle() { if (IsVisible) { settings.Hidden = true; Hide(); } else { settings.Hidden = false; Show(); Topmost = true; } settings.Save(); }
    BitmapSource LoadFrame(string name) => new BitmapImage(new Uri($"pack://application:,,,/Assets/{name}"));
    void SetFrame(string name) { bitmap = frames[name]; MonkeyImage.Source = bitmap; }
    void HitTestMouse()
    {
        if (hwnd == IntPtr.Zero || dragging || !IsVisible) return; GetCursorPos(out var p);
        var hit = p.X >= Left && p.Y >= Top && p.X < Left + ActualWidth && p.Y < Top + ActualHeight && Opaque((int)(p.X - Left), (int)(p.Y - Top));
        if (hit == interactive) return; interactive = hit; var style = GetWindowLong(hwnd, GwlExStyle); SetWindowLong(hwnd, GwlExStyle, hit ? style & ~Transparent : style | Transparent);
    }
    bool Opaque(int x, int y)
    {
        if (bitmap is null || ActualWidth < 1 || ActualHeight < 1) return false; x = Math.Clamp((int)(x / ActualWidth * bitmap.PixelWidth), 0, bitmap.PixelWidth - 1); y = Math.Clamp((int)(y / ActualHeight * bitmap.PixelHeight), 0, bitmap.PixelHeight - 1);
        var bytes = new byte[4]; bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), bytes, 4, 0); return bytes[3] > 30;
    }
    void LeftDown(object? _, MouseButtonEventArgs e) { dragging = true; dragMouse = e.GetPosition(null); dragWindow = new System.Windows.Point(Left, Top); CaptureMouse(); }
    void MovePet(object? _, System.Windows.Input.MouseEventArgs e) { if (!dragging) return; var p = e.GetPosition(null); Left = dragWindow.X + p.X - dragMouse.X; Top = dragWindow.Y + p.Y - dragMouse.Y; }
    void EndDrag() { if (!dragging) return; dragging = false; ReleaseMouseCapture(); settings.Left = Left; settings.Top = Top; settings.Save(); }
    void Behave()
    {
        if (paused || !IsVisible) return; if (Fullscreen()) { Hide(); settings.Hidden = true; settings.Save(); return; }
        var (p, side) = EdgeTarget(); var seconds = 1.4 + random.NextDouble() * 1.8;
        motion = side is 0 or 1 ? "crawl" : "climb"; SetFrame(motion == "crawl" ? "side" : "front");
        BeginAnimation(LeftProperty, new DoubleAnimation(p.X, TimeSpan.FromSeconds(seconds)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
        BeginAnimation(TopProperty, new DoubleAnimation(p.Y, TimeSpan.FromSeconds(seconds)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
        var transforms = new TransformGroup(); var scale = new ScaleTransform(1, 1); var rotate = new RotateTransform(side == 2 ? -90 : side == 3 ? 90 : 0); transforms.Children.Add(scale); transforms.Children.Add(rotate); MonkeyImage.RenderTransformOrigin = new System.Windows.Point(.5, .5); MonkeyImage.RenderTransform = transforms;
        var b = new DoubleAnimation(1, 1.09, TimeSpan.FromMilliseconds(220)) { AutoReverse = true, RepeatBehavior = new RepeatBehavior(3) }; scale.BeginAnimation(ScaleTransform.ScaleXProperty, b); scale.BeginAnimation(ScaleTransform.ScaleYProperty, b);
    }
    (System.Windows.Point point, int side) EdgeTarget()
    {
        var work = SystemParameters.WorkArea; var windows = Windows().Where(w => w.visible && !w.minimized && w.h != hwnd).ToList(); var fg = GetForegroundWindow(); var chosen = windows.FirstOrDefault(w => w.h == fg); if (chosen == default && windows.Count > 0) chosen = windows.OrderBy(w => Math.Abs(Left - w.r.Left) + Math.Abs(Top - w.r.Top)).First();
        var side = random.Next(4); if (chosen != default) { var r = chosen.r; return side switch { 0 => (new System.Windows.Point(Clamp(r.Left + random.NextDouble() * r.Width, work.Left, work.Right - ActualWidth), Clamp(r.Top - ActualHeight + 12, work.Top, work.Bottom - ActualHeight)), side), 1 => (new System.Windows.Point(Clamp(r.Left + random.NextDouble() * r.Width, work.Left, work.Right - ActualWidth), Clamp(r.Bottom - 12, work.Top, work.Bottom - ActualHeight)), side), 2 => (new System.Windows.Point(Clamp(r.Left - ActualWidth + 12, work.Left, work.Right - ActualWidth), Clamp(r.Top + random.NextDouble() * r.Height, work.Top, work.Bottom - ActualHeight)), side), _ => (new System.Windows.Point(Clamp(r.Right - 12, work.Left, work.Right - ActualWidth), Clamp(r.Top + random.NextDouble() * r.Height, work.Top, work.Bottom - ActualHeight)), side) }; }
        return side switch { 0 => (new System.Windows.Point(work.Left, work.Top + random.NextDouble() * (work.Height - ActualHeight)), side), 1 => (new System.Windows.Point(work.Right - ActualWidth, work.Top + random.NextDouble() * (work.Height - ActualHeight)), side), 2 => (new System.Windows.Point(work.Left + random.NextDouble() * (work.Width - ActualWidth), work.Top), side), _ => (new System.Windows.Point(work.Left + random.NextDouble() * (work.Width - ActualWidth), work.Bottom - ActualHeight), side) };
    }
    static double Clamp(double n, double lo, double hi) => Math.Max(lo, Math.Min(hi, n));
    bool Fullscreen() { var h = GetForegroundWindow(); if (h == IntPtr.Zero) return false; GetWindowRect(h, out var r); var w = SystemParameters.WorkArea; return r.Left <= w.Left && r.Top <= w.Top && r.Right >= w.Right && r.Bottom >= w.Bottom; }
    void ApplySize() { Height = SystemParameters.WorkArea.Height * settings.Size / 100; Width = Height * .83; }
    void PlayCall()
    {
        if (calling || settings.Muted) return; calling = true;
        var seconds = 2.0; var start = random.NextDouble() * Math.Max(1, 168.85 - seconds);
        call.Open(new Uri("pack://application:,,,/Assets/monkey_call.mp3")); call.Volume = settings.Volume; call.Position = TimeSpan.FromSeconds(start); call.Play();
        var stop = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) }; stop.Tick += (_, _) => { stop.Stop(); call.Stop(); calling = false; }; stop.Start();
    }
    void SettingsDialog()
    {
        var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(20), Width = 300 }; stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "奶猴桌宠设置", FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 16) }); stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "猴子大小（屏幕高度百分比）" });
        var size = new System.Windows.Controls.Slider { Minimum = 8, Maximum = 25, Value = settings.Size, TickFrequency = 1, IsSnapToTickEnabled = true }; stack.Children.Add(size); stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "猴叫音量" }); var volume = new System.Windows.Controls.Slider { Minimum = 0, Maximum = 1, Value = settings.Volume, TickFrequency = .1, IsSnapToTickEnabled = true }; stack.Children.Add(volume); var muted = new System.Windows.Controls.CheckBox { Content = "静音", IsChecked = settings.Muted, Margin = new Thickness(0, 4, 0, 8) }; stack.Children.Add(muted); var auto = new System.Windows.Controls.CheckBox { Content = "Windows 开机时自动启动", IsChecked = settings.AutoStart, Margin = new Thickness(0, 8, 0, 4) }; stack.Children.Add(auto); var mini = new System.Windows.Controls.CheckBox { Content = "启动后最小化到系统托盘", IsChecked = settings.StartMinimized, Margin = new Thickness(0, 4, 0, 12) }; stack.Children.Add(mini); var save = new System.Windows.Controls.Button { Content = "保存", Width = 80, HorizontalAlignment = System.Windows.HorizontalAlignment.Right }; stack.Children.Add(save);
        var dialog = new Window { Title = "设置", Content = stack, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, Topmost = true }; save.Click += (_, _) => { settings.Size = size.Value; settings.Volume = volume.Value; settings.Muted = muted.IsChecked == true; settings.AutoStart = auto.IsChecked == true; settings.StartMinimized = mini.IsChecked == true; ApplySize(); settings.Save(); dialog.Close(); }; dialog.ShowDialog();
    }
    static IEnumerable<(IntPtr h, Rect r, bool visible, bool minimized)> Windows() { var list = new List<(IntPtr, Rect, bool, bool)>(); EnumWindows((h, _) => { GetWindowRect(h, out var r); if (IsWindowVisible(h) && r.Right-r.Left > 100 && r.Bottom-r.Top > 100) list.Add((h, new Rect(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top), true, IsIconic(h))); return true; }, IntPtr.Zero); return list; }
    delegate bool EnumProc(IntPtr h, IntPtr l); [StructLayout(LayoutKind.Sequential)] struct P { public int X,Y; } [StructLayout(LayoutKind.Sequential)] struct R { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h,int i); [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h,int i,int v); [DllImport("user32.dll")] static extern bool GetCursorPos(out P p); [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow(); [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out R r); [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h); [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h); [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p,IntPtr l);
}

sealed class PetSettings
{
    public double Size { get; set; } = 15; public double Volume { get; set; } = .6; public bool Muted { get; set; } public double Left { get; set; } = double.NaN; public double Top { get; set; } = double.NaN; public bool AutoStart { get; set; } public bool StartMinimized { get; set; } public bool Hidden { get; set; }
    static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonkeyPet", "settings.json");
    public static PetSettings Load() { try { return JsonSerializer.Deserialize<PetSettings>(File.ReadAllText(FilePath)) ?? new(); } catch { return new(); } }
    public void Save() { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); File.WriteAllText(FilePath, JsonSerializer.Serialize(this)); }
}
