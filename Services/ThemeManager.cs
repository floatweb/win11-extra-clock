using Microsoft.Win32;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace Win11_Extra_Clock.Services
{
    public static class ThemeManager
    {
        private static bool _isInitialized = false;
        private static Task? _initializationTask = null;
        
        // update check
        public static void StartAutoUpdate(System.Windows.Application app)
        {
            
            ApplyFallbackTheme(app.Resources);
            
            _initializationTask = Task.Run(() => {
                try {
                    var themeData = ReadSystemTheme();
                    app.Dispatcher.Invoke(() => {
                        ApplyTheme(app.Resources, themeData);
                        _isInitialized = true;
                    });
                }
                catch (Exception) {
                    // 
                }
            });
            
            SystemEvents.UserPreferenceChanged += (_, __) => Task.Run(() => {
                try {
                    var themeData = ReadSystemTheme();
                    app.Dispatcher.Invoke(() => ApplyTheme(app.Resources, themeData));
                }
                catch (Exception) {
                    // 
                }
            });
        }

        public static void ApplyToWindow(Window w)
        {
            if (_isInitialized)
            {
                try
                {
                    var themeData = ReadSystemTheme();
                    ApplyTheme(w.Resources, themeData);
                    return;
                }
                catch
                {
                    // 
                }
            }
            
            ApplyFallbackTheme(w.Resources);
            
            if (_initializationTask != null)
            {
                _initializationTask.ContinueWith(_ => {
                    try
                    {
                        var themeData = ReadSystemTheme();
                        w.Dispatcher.Invoke(() => ApplyTheme(w.Resources, themeData));
                    }
                    catch
                    {
                        
                    }
                });
            }
        }

        private static void ApplyToResources(ResourceDictionary dict)
        {
            try
            {
                var themeData = ReadSystemTheme();
                ApplyTheme(dict, themeData);
            }
            catch (Exception)
            {
                ApplyFallbackTheme(dict);
            }
        }
        
        private static void ApplyTheme(ResourceDictionary dict, (bool isLight, bool useAccent, Color accent) themeData)
        {
            var (isLight, useAccent, accent) = themeData;

            Color bg = isLight ? Colors.White : Color.FromRgb(0x1E, 0x1E, 0x1E);
            double bgOpacity = 0.92;

            Color fg = isLight ? Color.FromRgb(0x11, 0x11, 0x11)
                               : Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF);

            Color border = isLight ? Color.FromArgb(0x26, 0x00, 0x00, 0x00) 
                                   : Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF); 

            if (useAccent)
            {
                border = Mix(accent, isLight ? Colors.Black : Colors.White, isLight ? 0.18 : 0.25);
                border.A = isLight ? (byte)0x26 : (byte)0x2A;
            }

            PutBrush(dict, "FlyoutBackgroundBrush", bg, bgOpacity);
            PutBrush(dict, "FlyoutForegroundBrush", fg, 1.0);
            PutBrush(dict, "FlyoutBorderBrush", border, 1.0);

            dict["FlyoutShadowOpacity"] = isLight ? 0.18 : 0.32;
            dict["SystemAccentColor"] = accent;
    }
    
    private static void ApplyFallbackTheme(ResourceDictionary dict)
    {
        // fallback dark theme
        Color bg = Color.FromRgb(0x1E, 0x1E, 0x1E);
        Color fg = Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF);
        Color border = Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF);
        Color accent = Color.FromRgb(0x00, 0x78, 0xD7);
        
        PutBrush(dict, "FlyoutBackgroundBrush", bg, 0.92);
        PutBrush(dict, "FlyoutForegroundBrush", fg, 1.0);
        PutBrush(dict, "FlyoutBorderBrush", border, 1.0);
        dict["FlyoutShadowOpacity"] = 0.32;
        dict["SystemAccentColor"] = accent;
    }

        private static void PutBrush(ResourceDictionary d, string key, Color color, double opacity)
        {
            if (d[key] is SolidColorBrush b && !b.IsFrozen)
            {
                b.Color = color;
                b.Opacity = opacity;
            }
            else
            {
                d[key] = new SolidColorBrush(color) { Opacity = opacity };
            }
        }

        private static (bool isLight, bool useAccent, Color accent) ReadSystemTheme()
        {
            const string personalize = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            const string dwm = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM";

            bool isLight = ((int?)Registry.GetValue(personalize, "SystemUsesLightTheme", 1)) == 1;
            bool useAccent = ((int?)Registry.GetValue(dwm, "ColorPrevalence", 0)) == 1;

            uint argb = (uint)(int?)Registry.GetValue(dwm, "ColorizationColor", unchecked((int)0xFF0078D7))!;
            byte A = (byte)((argb >> 24) & 0xFF);
            byte R = (byte)((argb >> 16) & 0xFF);
            byte G = (byte)((argb >> 8) & 0xFF);
            byte B = (byte)(argb & 0xFF);
            var accent = Color.FromArgb(A, R, G, B);
            return (isLight, useAccent, accent);
        }

        private static Color Mix(Color a, Color b, double t)
        {
            byte Lerp(byte x, byte y) => (byte)(x + (y - x) * t);
            return Color.FromArgb(0xFF, Lerp(a.R, b.R), Lerp(a.G, b.G), Lerp(a.B, b.B));
        }
    }
}