using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using Win11_Extra_Clock.Services;
using L10n = Win11_Extra_Clock.Services.LocalizationManager;

namespace Win11_Extra_Clock.UI
{
    public partial class AboutWindow : Window
    {
        // Win32 drag
        private static class NativeDrag
        {
            public const int WM_NCLBUTTONDOWN = 0xA1;
            public const int HTCAPTION = 0x2;

            [DllImport("user32.dll")] public static extern bool ReleaseCapture();
            [DllImport("user32.dll")]
            public static extern IntPtr SendMessage(
                IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        }

        private void StartSystemDrag()
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            NativeDrag.ReleaseCapture();
            NativeDrag.SendMessage(hwnd, NativeDrag.WM_NCLBUTTONDOWN, (IntPtr)NativeDrag.HTCAPTION, IntPtr.Zero);
        }

        private static AboutWindow? _instance;

        private const string LogoFile = "win11_extra_clock_logo_02.png";
        private const string GitHubFile = "github_light.png";
        private const string CoffeeFile = "bmc.png";

        private const string GitHubUrl = "https://github.com/floatweb/win11-extra-clock";
        private const string DonateUrl = "https://www.buymeacoffee.com/floatweb";

        public static void ShowSingleton()
        {
            if (_instance == null || !_instance.IsLoaded)
            {
                _instance = new AboutWindow();
                _instance.Closed += (_, __) => _instance = null;
                _instance.Show();
                _instance.Activate();
            }
            else
            {
                _instance.Activate();
                _instance.Topmost = true;
                _instance.Topmost = false;
            }
        }

        public AboutWindow()
        {
            InitializeComponent();
            ThemeManager.ApplyToWindow(this);

            ApplyLocalization();
            LoadImages();

            CloseBtn.Click += (_, __) => Close();

            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };

            MouseLeftButtonDown += AboutWindow_MouseLeftButtonDown;
            Chrome.MouseLeftButtonDown += AboutWindow_MouseLeftButtonDown;

            GitHubImage.MouseLeftButtonDown += (s, e) => e.Handled = true;
            GitHubImage.MouseLeftButtonUp += (s, e) => { e.Handled = true; OpenLink(GitHubUrl); };
            GitHubImage.ToolTip = L10n.Instance.T("Tray.Tooltip.Github", "Open GitHub");

            CoffeeImage.MouseLeftButtonDown += (s, e) => e.Handled = true;
            CoffeeImage.MouseLeftButtonUp += (s, e) => { e.Handled = true; OpenLink(DonateUrl); };
            CoffeeImage.ToolTip = L10n.Instance.T("Tray.Tooltip.Coffee", "Buy me a coffee");

            AddHandler(Hyperlink.RequestNavigateEvent,
                       new RequestNavigateEventHandler(Hyperlink_RequestNavigate));

            Loaded += (_, __) => PlayEntrance();

            L10n.Instance.LanguageChanged += (_, __) => ApplyLocalization();
        }

        private void AboutWindow_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed)
                return;

            if (IsFromInteractive(e.OriginalSource))
            {
                e.Handled = true;
                return;
            }

            StartSystemDrag();
            e.Handled = true;
        }

        private static void OpenLink(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch {  }
        }

        private static bool IsFromInteractive(object src)
        {
            DependencyObject? d = src as DependencyObject;

            while (d != null)
            {
                if (d is System.Windows.Controls.Primitives.ButtonBase) return true;
                if (d is System.Windows.Controls.Image) return true;
                if (d is Hyperlink) return true;

                if (d is Inline inline)
                {
                    TextElement? p = inline.Parent as TextElement;
                    while (p != null)
                    {
                        if (p is Hyperlink) return true;
                        p = p.Parent as TextElement;
                    }
                }

                d = GetLogicalOrVisualParent(d);
            }

            return false;
        }

        private static DependencyObject? GetLogicalOrVisualParent(DependencyObject obj)
        {
            if (obj is Visual || obj is Visual3D)
                return VisualTreeHelper.GetParent(obj);

            if (obj is FrameworkContentElement fce)
                return fce.Parent;

            if (obj is ContentElement ce)
                return ContentOperations.GetParent(ce);

            return LogicalTreeHelper.GetParent(obj);
        }

        private void ApplyLocalization()
        {
            var T = L10n.Instance.T;

            Title = T("About.Title", "About");
            HeaderTitle.Text = "Win11 Extra Clock";

            // versiune
            var asm = Assembly.GetExecutingAssembly();
            var ver = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? asm.GetName().Version?.ToString()
                      ?? "1.0.0";
            VersionText.Text = string.Format(T("About.VersionPrefix", "Version {0}"), ver);

            DescriptionText.Text = T("About.Description",
                "A small utility that shows a seconds clock and long date when you open the Windows 11 calendar.");

            LicenseTitle.Text = T("About.LicenseHeader", "License");
            LicenseBefore.Text = T("About.LicenseBefore", "Released under the ");
            LicenseAfter.Text = T("About.LicenseAfter", ". You may use, study, modify and redistribute this application under the same license.");

            SourceHeader.Text = T("About.SourceHeader", "Source");
            SourceBefore.Text = T("About.SourceBefore", "Source available on ");
            SourceAfter.Text = T("About.SourceAfter", ".");
            SupportHeader.Text = T("About.SupportHeader", "Support");
            SupportText.Text = T("About.SupportText", "Do you like this? Support the developer.");

            AuthorText.Text = T("About.Author", "Open-source by Float Web.");

            CloseBtn.Content = T("About.Close", "Close");
        }

        private void LoadImages()
        {
            var assets = Path.Combine(AppContext.BaseDirectory, "assets");
            TrySetImage(LogoImage, Path.Combine(assets, LogoFile));
            TrySetImage(GitHubImage, Path.Combine(assets, GitHubFile));
            TrySetImage(CoffeeImage, Path.Combine(assets, CoffeeFile));
        }

        private static void TrySetImage(System.Windows.Controls.Image img, string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.EndInit();
                    img.Source = bmp;
                }
            }
            catch {  }
        }

        public void Hyperlink_RequestNavigate(object? sender, RequestNavigateEventArgs e)
        {
            OpenLink(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void PlayEntrance()
        {
            try
            {
                var fade = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150)) { From = 0.0 };
                BeginAnimation(OpacityProperty, fade);

                var da = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180)) { From = 0.99 };
                ScaleRt.BeginAnimation(ScaleTransform.ScaleXProperty, da);
                ScaleRt.BeginAnimation(ScaleTransform.ScaleYProperty, da);
            }
            catch { }
        }
    }
}