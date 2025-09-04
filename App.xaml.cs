using System;
using System.IO;
using System.Threading;
using System.Windows;
using Win11_Extra_Clock.Services;
using Win11_Extra_Clock.Tray;
using Win11_Extra_Clock.UI;

namespace Win11_Extra_Clock
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _mutex;
        private const string AppMutexName = "Win11ExtraClockSingleInstanceMutex";

        private TrayIcon? _tray;
        private FlyoutWindow? _flyout;
        private CalendarDetector? _detector;
        private UpdateService? _updates;
        private PreciseSecondTimer? _clockTimer;

        protected override void OnStartup(StartupEventArgs e)
        {
            // single instance
            _mutex = new Mutex(true, AppMutexName, out bool createdNew);
            if (!createdNew)
            {
                _mutex?.Dispose();
                Shutdown();
                return;
            }

            base.OnStartup(e);

            ThemeManager.StartAutoUpdate(this);
            var langDir = Path.Combine(AppContext.BaseDirectory, "languages");
            LocalizationManager.Instance.Initialize(langDir, AppConfig.Language == "auto" ? null : AppConfig.Language);
            LocalizationManager.Instance.SetLanguage(AppConfig.Language == "auto" ? null : AppConfig.Language);

            // 1) UI
            _flyout = new FlyoutWindow
            {
                Topmost = true,
                ShowActivated = false,
                ShowInTaskbar = false
            };
            _flyout.UpdateNow();

            _tray = new TrayIcon(_flyout);

            // 2) Detector
            _detector = new CalendarDetector();
            _detector.CalendarVisibleChanged += OnCalendarVisibleChanged;

            // 3) Precise clock tick
            _clockTimer = new PreciseSecondTimer(Dispatcher, () =>
            {
                _flyout?.UpdateNow();
            });

            // 4) Update
            _updates = new UpdateService(
                "https://raw.githubusercontent.com/floatweb/win11-extra-clock/main/version.txt",
                interval: TimeSpan.FromHours(2),
                startImmediately: true
            );

            _updates.UpdateAvailableChanged += (_, available) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _tray?.SetUpdateAvailable(available);
                    AppState.Instance.UpdateAvailable = available;
                }));
            };

            // sync
            _tray.SetUpdateAvailable(_updates.UpdateAvailable);
            AppState.Instance.UpdateAvailable = _updates.UpdateAvailable;
        }

        private void OnCalendarVisibleChanged(object? sender, bool visible)
        {
            if (_flyout == null) return;

            Dispatcher.Invoke(() =>
            {
                if (visible)
                {
                    _flyout.UpdateNow();
                    _flyout.ApplyPosition();
                    _flyout.ShowFlyout();
                }
                else
                {
                    _flyout.HideFlyout();
                }
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _clockTimer?.Dispose();
            _updates?.Dispose();
            _detector?.Dispose();

            _tray?.Dispose();
            _flyout?.Close();

            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
                _mutex = null;
            }

            base.OnExit(e);
        }
    }
}