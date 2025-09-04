using System.ComponentModel;

namespace Win11_Extra_Clock.Services
{
    public sealed class AppState : INotifyPropertyChanged
    {
        public static AppState Instance { get; } = new AppState();
        private AppState() { }

        private bool _updateAvailable;
        public bool UpdateAvailable
        {
            get => _updateAvailable;
            set { if (_updateAvailable == value) return; _updateAvailable = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateAvailable))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}