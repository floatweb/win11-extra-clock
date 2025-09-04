using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Win11_Extra_Clock.Models;
using Win11_Extra_Clock.Services;
using Drawing = System.Drawing;
using L10n = Win11_Extra_Clock.Services.LocalizationManager;
using WinForms = System.Windows.Forms;

namespace Win11_Extra_Clock.Tray
{
    public sealed class TrayIcon : IDisposable
    {
        private readonly WinForms.NotifyIcon _ni;
        private readonly UI.FlyoutWindow _flyout;

        private readonly Icon _iconForLightTray;
        private readonly Icon _iconForDarkTray;


        private WinForms.ToolStripMenuItem _positionMenu = null!;
        private WinForms.ToolStripMenuItem? _posCustomItem;
        private WinForms.ToolStripMenuItem? _showTestItem;
        private WinForms.ToolStripMenuItem? _customMoveItem;

        private bool _hasUpdate;

        private System.Windows.Forms.ToolStripMenuItem? _updateItem;
        private const string ReleasesUrl = "https://github.com/floatweb/win11-extra-clock/releases/latest";

        private System.Drawing.Icon? _iconForDarkTrayBadge;
        private System.Drawing.Icon? _iconForLightTrayBadge;

        public TrayIcon(UI.FlyoutWindow flyout)
        {
            _flyout = flyout;

            _iconForDarkTray = LoadIcoFromResource("pack://application:,,,/assets/extra-clock-dark.ico");
            _iconForLightTray = LoadIcoFromResource("pack://application:,,,/assets/extra-clock-light.ico");

            _ni = new WinForms.NotifyIcon
            {
                Text = "Win11 Extra Clock",
                Icon = GetIconForCurrentTheme()
            };

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            _ni.Visible = true;

            Task.Run(() =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    BuildContextMenu();
                    L10n.Instance.LanguageChanged += (_, __) => BuildContextMenu();
                });
            });

           
        }

        private static Icon LoadIcoFromResource(string packUri)
        {
            var sri = System.Windows.Application.GetResourceStream(new Uri(packUri));
            if (sri == null) throw new FileNotFoundException("ICO not found: " + packUri);
            using var s = sri.Stream;
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            ms.Position = 0;
            return new Icon(ms);
        }

        private static bool IsSystemLightTheme()
        {
            // system theme
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("SystemUsesLightTheme", 1);
            return Convert.ToInt32(val) == 1;
        }

        private Icon GetIconForCurrentTheme()
        {
            return IsSystemLightTheme() ? _iconForLightTray : _iconForDarkTray;
        }

        private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            // live theme switch
            try
            {
                var desired = GetIconForCurrentTheme();
                if (!ReferenceEquals(_ni.Icon, desired))
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        _ni.Icon = desired;
                    });
                }
            }
            catch {  }
        }

        private void BuildContextMenu()
        {
            var T = L10n.Instance.T;
            var cm = new WinForms.ContextMenuStrip();

            _updateItem = new WinForms.ToolStripMenuItem("📦 Update available", /* image */ null, (_, __) => OpenReleases())
            {
                Visible = _hasUpdate,
                Enabled = _hasUpdate
            };

            if (_hasUpdate && cm.Font != null)
                _updateItem.Font = new System.Drawing.Font(cm.Font, System.Drawing.FontStyle.Bold);

            cm.Items.Insert(0, _updateItem);
            cm.Items.Insert(1, new WinForms.ToolStripSeparator());

            // Flyout positions
            _positionMenu = new WinForms.ToolStripMenuItem(T("Tray.Position", "Position"));
            _posCustomItem = null;

            foreach (Position pos in Enum.GetValues(typeof(Position)))
            {
                // default positions
                var key = $"Pos.{pos}";
                var text = (pos == Position.Custom)
                           ? T("Tray.CustomLocation", "Custom location")
                           : T(key, pos.ToString());

                var item = new WinForms.ToolStripMenuItem(text)
                {
                    Tag = pos,
                    Checked = AppConfig.Position == pos
                };

                if (pos == Position.Custom) _posCustomItem = item;

                item.Click += (s, e) =>
                {
                    foreach (WinForms.ToolStripItem it in _positionMenu.DropDownItems)
                        if (it is WinForms.ToolStripMenuItem mi) mi.Checked = false;

                    var p = (Position)((WinForms.ToolStripMenuItem)s!).Tag!;
                    AppConfig.Position = p;
                    ((WinForms.ToolStripMenuItem)s!).Checked = true;

                    _flyout.ApplyPosition();
                    UpdateCustomMoveEnabled();
                };

                _positionMenu.DropDownItems.Add(item);
            }

            cm.Items.Add(_positionMenu);
            cm.Items.Add(new WinForms.ToolStripSeparator());

            // Show test (pin)
            _showTestItem = new WinForms.ToolStripMenuItem(T("Tray.ShowTest", "Show test"))
            { CheckOnClick = true };

            _showTestItem.CheckedChanged += (s, e) =>
            {
                _flyout.IsPinnedByTest = _showTestItem!.Checked;
                if (_showTestItem.Checked) { _flyout.UpdateNow(); _flyout.ShowFlyout(); }
                else { _flyout.IsPinnedByTest = false; _flyout.HideFlyout(); }
                UpdateCustomMoveEnabled();
            };
            cm.Items.Add(_showTestItem);

            // Custom move
            _customMoveItem = new WinForms.ToolStripMenuItem(T("Tray.CustomMove", "Custom move…"))
            {
                ToolTipText = T("Tray.CustomMoveTip",
                    "Drag the clock; position will be saved as 'Custom location'.")
            };
            _customMoveItem.Click += (s, e) =>
            {
                if (!(_showTestItem?.Checked ?? false)) _showTestItem!.Checked = true;
                _flyout.BeginCustomMoveOnce((x, y) =>
                {
                    AppConfig.Position = Position.Custom;
                    _flyout.ApplyPosition();

                    if (_posCustomItem != null)
                    {
                        foreach (WinForms.ToolStripItem it in _positionMenu.DropDownItems)
                            if (it is WinForms.ToolStripMenuItem mi) mi.Checked = false;
                        _posCustomItem.Checked = true;
                    }

                    UpdateCustomMoveEnabled();
                });
            };
            cm.Items.Add(_customMoveItem);

            // Languages
            var langMenu = new WinForms.ToolStripMenuItem(T("Tray.Language", "Language"));

            var autoItem = new WinForms.ToolStripMenuItem(T("Tray.LanguageAuto", "Auto (system)"))
            {
                Checked = string.Equals(AppConfig.Language, "auto", StringComparison.OrdinalIgnoreCase)
            };
            autoItem.Click += (s, e) =>
            {
                AppConfig.Language = "auto";
                L10n.Instance.SetLanguage(null);
            };
            langMenu.DropDownItems.Add(autoItem);

            foreach (var lang in L10n.Instance.Available.OrderBy(l => l.Name))
            {
                var li = new WinForms.ToolStripMenuItem($"{lang.Name} ({lang.Code})")
                {
                    Tag = lang.Code,
                    Checked = AppConfig.Language.Equals(lang.Code, StringComparison.OrdinalIgnoreCase)
                };
                li.Click += (s, e) =>
                {
                    foreach (WinForms.ToolStripItem it in langMenu.DropDownItems)
                        if (it is WinForms.ToolStripMenuItem mi) mi.Checked = false;

                    var code = (string)((WinForms.ToolStripMenuItem)s!).Tag!;
                    AppConfig.Language = code;
                    ((WinForms.ToolStripMenuItem)s!).Checked = true;
                    L10n.Instance.SetLanguage(code);
                };
                langMenu.DropDownItems.Add(li);
            }

            cm.Items.Add(new WinForms.ToolStripSeparator());
            cm.Items.Add(langMenu);

            // About
            var about = new WinForms.ToolStripMenuItem(T("Tray.About", "About…"));
            about.Click += (s, e) => UI.AboutWindow.ShowSingleton();
            cm.Items.Add(new WinForms.ToolStripSeparator());
            cm.Items.Add(about);

            // Exit
            var exit = new WinForms.ToolStripMenuItem(T("Tray.Exit", "Exit"));
            exit.Click += (s, e) => System.Windows.Application.Current.Shutdown();
            cm.Items.Add(exit);

            _ni.ContextMenuStrip = cm;

            if (_posCustomItem != null) _posCustomItem.Checked = (AppConfig.Position == Position.Custom);
            UpdateCustomMoveEnabled();
        }

        private void UpdateCustomMoveEnabled()
        {
            if (_customMoveItem == null || _showTestItem == null) return;
            _customMoveItem.Enabled = _showTestItem.Checked && AppConfig.Position == Position.Custom;
        }

        public void SetUpdateAvailable(bool available)
        {
            _hasUpdate = available;
            ApplyTrayIcon();

            if (_updateItem != null)
            {
                _updateItem.Visible = available;
                _updateItem.Enabled = available;
                _updateItem.Text = available ? "📦 Update available" : "Up to date";

                if (_ni?.ContextMenuStrip?.Font != null)
                    _updateItem.Font = new System.Drawing.Font(_ni.ContextMenuStrip.Font, available ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular);
            }
        }

        private static void OpenReleases()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ReleasesUrl,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch {  }
        }

        private void ApplyTrayIcon()
        {
            var baseIcon = GetIconForCurrentTheme();
            if (_hasUpdate)
            {
                if (baseIcon == _iconForDarkTray && _iconForDarkTrayBadge == null)
                    _iconForDarkTrayBadge = MakeBadgedIcon(_iconForDarkTray);
                if (baseIcon == _iconForLightTray && _iconForLightTrayBadge == null)
                    _iconForLightTrayBadge = MakeBadgedIcon(_iconForLightTray);

                _ni.Icon = baseIcon == _iconForDarkTray ? _iconForDarkTrayBadge! : _iconForLightTrayBadge!;
            }
            else
            {
                _ni.Icon = baseIcon;
            }
        }


        // update badge
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static System.Drawing.Icon MakeBadgedIcon(System.Drawing.Icon baseIcon)
        {
            using var bmp = baseIcon.ToBitmap();
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

            int w = bmp.Width, h = bmp.Height;
            int d = Math.Max(4, (int)Math.Round(Math.Min(w, h) * 0.44));
            int margin = Math.Max(1, d / 6);

            var rect = new System.Drawing.Rectangle(w - d - margin, margin, d, d);

            var shadow = rect; shadow.Offset(0, 1);
            using (var sb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(110, 0, 0, 0)))
                g.FillEllipse(sb, shadow);

            using (var rb = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0xE8, 0x11, 0x23)))
                g.FillEllipse(rb, rect);

            using (var pen = new System.Drawing.Pen(System.Drawing.Color.White, Math.Max(1f, d * 0.16f)))
                g.DrawEllipse(pen, rect);

            var hi = new System.Drawing.Rectangle(rect.X + (int)(d * 0.20), rect.Y + (int)(d * 0.18),
                                                  (int)(d * 0.42), (int)(d * 0.42));
            using (var hb = new System.Drawing.Drawing2D.LinearGradientBrush(
                hi, System.Drawing.Color.FromArgb(160, 255, 255, 255),
                System.Drawing.Color.FromArgb(0, 255, 255, 255),
                45f))
            {
                g.FillEllipse(hb, hi);
            }

            IntPtr hIcon = bmp.GetHicon();
            try { using var tmp = System.Drawing.Icon.FromHandle(hIcon); return (System.Drawing.Icon)tmp.Clone(); }
            finally { DestroyIcon(hIcon); }
        }

        public void Dispose()
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _ni.Visible = false;
            _ni.Dispose();
            _iconForDarkTray?.Dispose();
            _iconForLightTray?.Dispose();
        }
    }
}