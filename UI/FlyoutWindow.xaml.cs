using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Win11_Extra_Clock.Models;
using Win11_Extra_Clock.Services;

namespace Win11_Extra_Clock.UI
{
    public partial class FlyoutWindow : Window
    {
        private DispatcherTimer? _timer;
        private bool _isTimerInitialized = false;

        public bool IsPinnedByTest { get; set; }

        public FlyoutWindow()
        {
            InitializeComponent();
            ThemeManager.ApplyToWindow(this);

            Loaded += (s, e) => InitializeTimer();
        }
        
        private void InitializeTimer()
        {
            if (_isTimerInitialized) return;
            
            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (s, e) => UpdateNow();
            _timer.Start();
            
            _isTimerInitialized = true;
        }

        public void ShowFlyout()
        {
            
            InitializeTimer();
            
            ThemeManager.ApplyToWindow(this);
            UpdateNow();

            if (!IsVisible)
            {
                Opacity = 0;
                Show();
                UpdateLayout();
            }

            ApplyPosition();

            var da = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, da);
            
            if (_timer != null && !_timer.IsEnabled)
            {
                _timer.Start();
            }
        }

        public void HideFlyout()
        {
            if (IsPinnedByTest) return;

            var da = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            da.Completed += (_, __) =>
            {
                try { Hide(); } catch { }
                Opacity = 1.0;
                BeginAnimation(OpacityProperty, null);
                
                if (_timer != null && _timer.IsEnabled)
                {
                    _timer.Stop();
                }
            };
            BeginAnimation(OpacityProperty, da);
        }

        public void UpdateNow()
        {
            var now = DateTime.Now;
            TimeText.Text = now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);

            var raw = now.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture);
            DateText.Text = CapitalizeFirst(raw, CultureInfo.CurrentCulture);
        }

        public void Reposition() => ApplyPosition();

        private static string CapitalizeFirst(string text, CultureInfo culture)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var ti = culture.TextInfo;
            var first = ti.ToUpper(text[0].ToString());
            return first + text[1..];
        }

        private (double w, double h) GetSizeForLayout()
        {
            double w = ActualWidth, h = ActualHeight;

            if (w < 1 || h < 1)
            {
                if (!double.IsNaN(Width) && Width > 1) w = Width;
                if (!double.IsNaN(Height) && Height > 1) h = Height;
            }
            if (w < 1 || h < 1)
            {
                if (Content is FrameworkElement fe)
                {
                    fe.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                    var ds = fe.DesiredSize;
                    if (ds.Width > 1) w = ds.Width;
                    if (ds.Height > 1) h = ds.Height;
                }
            }
            if (w < 1) w = 360;
            if (h < 1) h = 160;
            return (w, h);
        }

        public void ApplyPosition()
        {
            var wa = SystemParameters.WorkArea;

            var (w, h) = GetSizeForLayout();

            var (left, top) = PositionHelper.GetTopLeftFor(
                AppConfig.Position,
                wa,
                w,
                h,
                margin: 12,
                customTopLeft: new System.Windows.Point(AppConfig.CustomX, AppConfig.CustomY));

            left = Math.Max(wa.Left, Math.Min(left, wa.Right - w));
            top = Math.Max(wa.Top, Math.Min(top, wa.Bottom - h));

            Left = left;
            Top = top;
        }

        // Custom move
        private System.Windows.Media.Brush? _savedBorderBrush;
        private Thickness _savedBorderThickness;
        private System.Windows.Media.Effects.DropShadowEffect? _savedShadow;
        private DispatcherTimer? _armRevertTimer;

        private void ArmCustomMoveUI(int seconds = 10)
        {
            _savedBorderBrush = Chrome.BorderBrush;
            _savedBorderThickness = Chrome.BorderThickness;
            _savedShadow = Chrome.Effect as System.Windows.Media.Effects.DropShadowEffect;

            Chrome.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2C, 0xD1, 0x55));
            Chrome.BorderThickness = new Thickness(2);
            Chrome.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Color.FromRgb(0x2C, 0xD1, 0x55),
                BlurRadius = 16,
                ShadowDepth = 0,
                Opacity = 0.55
            };

            _armRevertTimer?.Stop();
            _armRevertTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
            _armRevertTimer.Tick += (_, __) => { _armRevertTimer!.Stop(); DisarmCustomMoveUI(); };
            _armRevertTimer.Start();

            this.Cursor = System.Windows.Input.Cursors.SizeAll;
        }

        private void DisarmCustomMoveUI()
        {
            _armRevertTimer?.Stop();
            if (_savedBorderBrush != null) Chrome.BorderBrush = _savedBorderBrush;
            Chrome.BorderThickness = _savedBorderThickness;
            Chrome.Effect = _savedShadow;
            this.Cursor = System.Windows.Input.Cursors.Arrow;
        }

        public void BeginCustomMoveOnce(Action<double, double>? onCommitted = null)
        {
            ArmCustomMoveUI();

            MouseButtonEventHandler? handler = null;
            handler = (s, e) =>
            {
                try { DragMove(); } catch { }
                this.MouseLeftButtonDown -= handler;

                var wa = SystemParameters.WorkArea;
                double nx = Math.Max(wa.Left, Math.Min(Left, wa.Right - ActualWidth));
                double ny = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - ActualHeight));
                Left = nx; Top = ny;

                AppConfig.CustomX = nx;
                AppConfig.CustomY = ny;
                onCommitted?.Invoke(nx, ny);

                DisarmCustomMoveUI();
            };

            this.MouseLeftButtonDown += handler;
        }
    }
}