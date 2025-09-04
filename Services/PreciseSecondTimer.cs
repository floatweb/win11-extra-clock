using System;
using System.Threading;
using System.Windows.Threading;
using Timer = System.Threading.Timer;

namespace Win11_Extra_Clock.Services
{
    // clock timer
    public sealed class PreciseSecondTimer : IDisposable
    {
        private readonly Dispatcher _ui;
        private readonly Action _onSecond;
        private readonly Timer _tp;
        private volatile bool _disposed;

        public PreciseSecondTimer(Dispatcher uiDispatcher, Action onSecond)
        {
            _ui = uiDispatcher;
            _onSecond = onSecond;
            _tp = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
            ScheduleNext();
        }

        public void Dispose()
        {
            _disposed = true;
            _tp.Dispose();
        }

        private void OnTick(object? _)
        {
            if (_disposed) return;
            _ui.BeginInvoke(DispatcherPriority.Background, _onSecond);
            ScheduleNext();
        }

        private void ScheduleNext()
        {
            var now = DateTime.UtcNow;
            int ms = 1000 - (int)(now.Ticks / TimeSpan.TicksPerMillisecond % 1000);
            if (ms < 5) ms += 1000;
            _tp.Change(ms, Timeout.Infinite);
        }
    }
}