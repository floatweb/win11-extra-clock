using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Threading;

namespace Win11_Extra_Clock.Services
{
    public sealed class CalendarDetector : IDisposable
    {
        public event EventHandler<bool>? CalendarVisibleChanged;

        private readonly Thread _thread;
        private Dispatcher? _dispatcher;

        // UIA targets & handlers
        private AutomationElement? _taskbarRoot;
        private AutomationElement? _clockBtn;
        private AutomationPropertyChangedEventHandler? _propHandler;
        private StructureChangedEventHandler? _structHandler;
-
        private DispatcherTimer? _rescanTimer;
        private DispatcherTimer? _fallbackTimer;

        private volatile bool _lastVisible;

        private static readonly string[] ClockNames = {
            "Date and time", "Time and date", "Clock"
        };

        public CalendarDetector()
        {
            _thread = new Thread(ThreadMain)
            {
                IsBackground = true,
                Name = "CalendarDetectorThread",
                Priority = ThreadPriority.BelowNormal
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public void Dispose()
        {
            try
            {
                if (_dispatcher != null && !_dispatcher.HasShutdownStarted)
                    _dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
            }
            catch {  }

            if (_thread.IsAlive)
                _thread.Join(TimeSpan.FromSeconds(2));
        }

        // Thread entry (STA)
        private void ThreadMain()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;

            // Timers
            _rescanTimer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, (_, __) => TryAttach(), _dispatcher);
            _fallbackTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(450), DispatcherPriority.Background, (_, __) => FallbackTick(), _dispatcher);
            _fallbackTimer!.Stop(); // start only if needed

            // Initial attach
            TryAttach();

            Dispatcher.Run();
        }

        // Attach to Taskbar + Clock button / UIA
        private void TryAttach()
        {
            _taskbarRoot = GetTaskbarRoot();
            if (_taskbarRoot == null)
            {
                // fallback : no UIA
                EnsureFallback(on: true);
                return;
            }

            _clockBtn = FindClockButton(_taskbarRoot);
            if (_clockBtn != null)
            {
                AttachHandlers();
                
                EnsureFallback(on: false);
                
                bool? viaBtn = ReadClockButtonState(_clockBtn);
                if (viaBtn.HasValue) RaiseIfChanged(viaBtn.Value);
                return;
            }

            EnsureFallback(on: true);
        }

        private AutomationElement? GetTaskbarRoot()
        {
            try
            {
                var h = FindWindow("Shell_TrayWnd", null);
                if (h == IntPtr.Zero) return null;

                var cr = new CacheRequest();
                cr.Add(AutomationElement.NameProperty);
                cr.Add(AutomationElement.ControlTypeProperty);
                cr.Add(AutomationElement.ClassNameProperty);
                cr.TreeScope = TreeScope.Element | TreeScope.Children;

                using (cr.Activate())
                {
                    return AutomationElement.FromHandle(h);
                }
            }
            catch
            {
                return null;
            }
        }

        private AutomationElement? FindClockButton(AutomationElement root)
        {
            try
            {
                foreach (var name in ClockNames)
                {
                    var cond = new AndCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                        new PropertyCondition(AutomationElement.NameProperty, name, PropertyConditionFlags.IgnoreCase)
                    );
                    var el = root.FindFirst(TreeScope.Subtree, cond);
                    if (el != null) return el;
                }

                // Fallback
                var byId = new PropertyCondition(AutomationElement.AutomationIdProperty, "Clock");
                var elById = root.FindFirst(TreeScope.Subtree, byId);
                if (elById != null) return elById;

                return null;
            }
            catch { return null; }
        }

        private void AttachHandlers()
        {
            DetachHandlers();

            if (_taskbarRoot != null)
            {
                _structHandler = (_, __) =>
                {
                    _dispatcher?.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        TryAttach();
                    }));
                };
                Automation.AddStructureChangedEventHandler(_taskbarRoot, TreeScope.Subtree, _structHandler);
            }

            if (_clockBtn != null)
            {
                _propHandler = (_, e) =>
                {
                    if (e.Property == ExpandCollapsePattern.ExpandCollapseStateProperty ||
                        e.Property == TogglePattern.ToggleStateProperty ||
                        e.Property == AutomationElement.IsOffscreenProperty)
                    {
                        bool? viaBtn = ReadClockButtonState(_clockBtn);
                        if (viaBtn.HasValue) RaiseIfChanged(viaBtn.Value);
                    }
                };

                Automation.AddAutomationPropertyChangedEventHandler(
                    _clockBtn, TreeScope.Element, _propHandler,
                    ExpandCollapsePattern.ExpandCollapseStateProperty,
                    TogglePattern.ToggleStateProperty,
                    AutomationElement.IsOffscreenProperty);
            }
        }

        private void DetachHandlers()
        {
            if (_clockBtn != null && _propHandler != null)
                Automation.RemoveAutomationPropertyChangedEventHandler(_clockBtn, _propHandler);
            _propHandler = null;

            if (_taskbarRoot != null && _structHandler != null)
                Automation.RemoveStructureChangedEventHandler(_taskbarRoot, _structHandler);
            _structHandler = null;
        }

        private bool? ReadClockButtonState(AutomationElement btn)
        {
            try
            {
                if (btn.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out object? expObj))
                {
                    var st = ((ExpandCollapsePattern)expObj).Current.ExpandCollapseState;
                    if (st == ExpandCollapseState.Expanded) return true;
                    if (st == ExpandCollapseState.Collapsed) return false;
                }
            }
            catch {  }

            try
            {
                if (btn.TryGetCurrentPattern(TogglePattern.Pattern, out object? togObj))
                    return ((TogglePattern)togObj).Current.ToggleState == ToggleState.On;
            }
            catch {  }

            try { return btn.Current.HasKeyboardFocus; } catch { return null; }
        }

        // Fallback
        private void EnsureFallback(bool on)
        {
            if (_fallbackTimer == null) return;
            if (on && !_fallbackTimer.IsEnabled) _fallbackTimer.Start();
            else if (!on && _fallbackTimer.IsEnabled) _fallbackTimer.Stop();
        }

        private void FallbackTick()
        {
            bool vis = DetectViaTopmostProbe();
            RaiseIfChanged(vis);
        }

        private bool DetectViaTopmostProbe()
        {
            var (tbRect, edge) = GetTaskbar();
            var pts = GetProbePoints(tbRect, edge);
            foreach (var pt in pts)
            {
                var h = WindowFromPoint(pt);
                if (h == IntPtr.Zero) continue;

                var root = GetAncestor(h, GA_ROOT);
                if (root == IntPtr.Zero) root = h;

                string proc = GetProcessName(root);
                if (!proc.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase))
                    continue;

                string cls = GetClass(root);
                if (cls.IndexOf("CoreWindow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cls.IndexOf("Xaml_WindowedPopupClass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cls.IndexOf("DesktopWindowContentBridge", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void RaiseIfChanged(bool visible)
        {
            if (visible == _lastVisible) return;
            _lastVisible = visible;

            CalendarVisibleChanged?.Invoke(this, visible);
        }

        // Taskbar & Win32 helpers
        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }
        private const uint ABM_GETTASKBARPOS = 0x00000005;
        private const uint ABE_LEFT = 0, ABE_TOP = 1, ABE_RIGHT = 2, ABE_BOTTOM = 3;

        [DllImport("shell32.dll")] private static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        private static (RECT rect, uint edge) GetTaskbar()
        {
            var abd = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
            if (SHAppBarMessage(ABM_GETTASKBARPOS, ref abd) != 0)
                return (abd.rc.Normalize(), abd.uEdge);

            // Fallback
            var h = FindWindow("Shell_TrayWnd", null);
            GetWindowRect(h, out var rc);
            return (rc.Normalize(), ABE_BOTTOM);
        }

        private static POINT[] GetProbePoints(RECT tb, uint edge)
        {
            // calendar flyout
            int w = 420, h = 620;
            RECT area;
            if (edge == ABE_BOTTOM)
                area = new RECT { Right = tb.Right - 20, Left = tb.Right - 20 - w, Bottom = tb.Top - 10, Top = tb.Top - 10 - h };
            else if (edge == ABE_TOP)
                area = new RECT { Right = tb.Right - 20, Left = tb.Right - 20 - w, Top = tb.Bottom + 10, Bottom = tb.Bottom + 10 + h };
            else if (edge == ABE_RIGHT)
                area = new RECT { Left = tb.Left - 10 - w, Right = tb.Left - 10, Bottom = tb.Bottom - 20, Top = tb.Bottom - 20 - h };
            else // left
                area = new RECT { Left = tb.Right + 10, Right = tb.Right + 10 + w, Bottom = tb.Bottom - 20, Top = tb.Bottom - 20 - h };
            area = area.Normalize();

            int cx = (area.Left + area.Right) / 2;
            int cy = (area.Top + area.Bottom) / 2;
            return new[]
            {
                new POINT{ X = cx, Y = cy },
                new POINT{ X = area.Left + 30,  Y = area.Top + 30 },
                new POINT{ X = area.Right - 30, Y = area.Top + 30 },
                new POINT{ X = area.Left + 30,  Y = area.Bottom - 30 },
                new POINT{ X = area.Right - 30, Y = area.Bottom - 30 },
            };
        }

        // Win32 P/Invoke
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
            public bool IsEmpty => Width <= 0 || Height <= 0;
            public RECT Normalize()
            {
                int l = Math.Min(Left, Right);
                int r = Math.Max(Left, Right);
                int t = Math.Min(Top, Bottom);
                int b = Math.Max(Top, Bottom);
                return new RECT { Left = l, Top = t, Right = r, Bottom = b };
            }
        }

        private const uint GA_ROOT = 2;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT Point);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private static string GetProcessName(IntPtr hwnd)
        {
            try { GetWindowThreadProcessId(hwnd, out var pid); return Process.GetProcessById((int)pid).ProcessName; }
            catch { return "?"; }
        }

        private static string GetClass(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }
    }
}