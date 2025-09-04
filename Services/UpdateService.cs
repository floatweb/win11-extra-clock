using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Win11_Extra_Clock.Services
{
    public sealed class UpdateService : IDisposable
    {
        public event EventHandler<bool>? UpdateAvailableChanged;

        public bool UpdateAvailable
        {
            get => _state.UpdateAvailable;
            private set
            {
                if (_state.UpdateAvailable == value) return;
                _state.UpdateAvailable = value;
                SaveState();
                UpdateAvailableChanged?.Invoke(this, value);
            }
        }

        public TimeSpan Interval { get; private set; }

        private readonly Uri _rawVersionUri;   // https://raw.githubusercontent.com/floatweb/win11-extra-clock/main/version.txt
        private readonly Uri _apiLatestUri;    // https://api.github.com/repos/floatweb/win11-extra-clock/releases/latest
        private readonly Version _current;
        private readonly string _statePath;
        private readonly string _logPath;
        private readonly System.Threading.Timer _timer;
        private State _state = new();

        // HttpClient
        private static readonly HttpClient _http = BuildClient();

        private static HttpClient BuildClient()
        {
            var h = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            };

            var c = new HttpClient(h)
            {
                Timeout = TimeSpan.FromSeconds(12),
                DefaultRequestVersion = HttpVersion.Version11,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Win11ExtraClock/1.0 (+https://github.com/floatweb/win11-extra-clock)");
            c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            return c;
        }

        public UpdateService(string rawVersionUrl, TimeSpan? interval = null, bool startImmediately = true)
        {
            _rawVersionUri = new Uri(rawVersionUrl);

            var parts = _rawVersionUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && _rawVersionUri.Host.Contains("githubusercontent"))
            {
                var owner = parts[0];
                var repo = parts[1];
                _apiLatestUri = new Uri($"https://api.github.com/repos/{owner}/{repo}/releases/latest");
            }
            else
            {
                _apiLatestUri = _rawVersionUri;
            }

            _current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win11ExtraClock");
            Directory.CreateDirectory(dir);
            _statePath = Path.Combine(dir, "update_state.json");
            _logPath = Path.Combine(dir, "update.log");
            LoadState();

            Log($"Init. App={_current} Raw={_rawVersionUri} API={_apiLatestUri}");

            Interval = interval ?? TimeSpan.FromHours(2);

            var firstDue = startImmediately ? TimeSpan.Zero : Interval;
            _timer = new System.Threading.Timer(async _ => await SafeCheckNow().ConfigureAwait(false),
                                                null, firstDue, Interval);
        }

        public void Dispose() => _timer.Dispose();

        public void SetInterval(TimeSpan newInterval, bool triggerNow = false)
        {
            Interval = newInterval;
            _timer.Change(triggerNow ? TimeSpan.Zero : newInterval, newInterval);
            Log($"Interval set to {newInterval}. TriggerNow={triggerNow}");
        }

        public Task CheckNowAsync() => SafeCheckNow();

        // Update check

        private async Task SafeCheckNow()
        {
            try { await CheckCoreAsync().ConfigureAwait(false); }
            catch (Exception ex) { Log("SafeCheck error: " + ex); }
        }

        private async Task CheckCoreAsync()
        {
            Log("Check start");

            Version? cachedRemote = ParseVersion(_state.RemoteVersion);
            Version? remote = null;

            // 1) RAW
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, _rawVersionUri);
                if (!string.IsNullOrEmpty(_state.RawETag))
                    req.Headers.TryAddWithoutValidation("If-None-Match", _state.RawETag);

                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (resp.StatusCode == HttpStatusCode.NotModified)
                {
                    remote = cachedRemote;
                    Log($"RAW 304 NotModified (cached={remote?.ToString() ?? "null"})");
                }
                else
                {
                    resp.EnsureSuccessStatusCode();
                    var body = (await resp.Content.ReadAsStringAsync().ConfigureAwait(false)).Trim();
                    if (body.StartsWith("v", StringComparison.OrdinalIgnoreCase)) body = body[1..];
                    remote = ParseVersion(body);
                    _state.RawETag = resp.Headers.ETag?.Tag?.Trim();
                    if (remote != null) _state.RemoteVersion = remote.ToString();
                    Log($"RAW OK body='{body}', etag={_state.RawETag ?? "-"} parsed={remote}");
                }
            }
            catch (Exception ex)
            {
                Log("RAW fail: " + ex.GetType().Name + " " + ex.Message);
            }

            // 2) Fallback
            if (remote is null && _apiLatestUri.Host.Contains("api.github.com"))
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, _apiLatestUri);
                    if (!string.IsNullOrEmpty(_state.ApiETag))
                        req.Headers.TryAddWithoutValidation("If-None-Match", _state.ApiETag);
                    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

                    using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                    if (resp.StatusCode == HttpStatusCode.NotModified)
                    {
                        remote = cachedRemote;
                        Log($"API 304 NotModified (cached={remote?.ToString() ?? "null"})");
                    }
                    else if (resp.StatusCode == HttpStatusCode.NotFound)
                    {
                        Log("API 404 (no releases) – skipping");
                    }
                    else
                    {
                        resp.EnsureSuccessStatusCode();
                        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("tag_name", out var tag))
                        {
                            var s = tag.GetString() ?? "";
                            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s[1..];
                            remote = ParseVersion(s);
                            if (remote != null) _state.RemoteVersion = remote.ToString();
                            Log($"API OK tag='{tag.GetString()}' parsed={remote}");
                        }
                        _state.ApiETag = resp.Headers.ETag?.Tag?.Trim();
                    }
                }
                catch (Exception ex)
                {
                    Log("API fail: " + ex.GetType().Name + " " + ex.Message);
                }
            }

            _state.LastCheckedUtc = DateTime.UtcNow;
            SaveState();

            if (remote != null)
            {
                UpdateAvailable = remote > _current;
                Log($"Result: remote={remote} current={_current} -> UpdateAvailable={UpdateAvailable}");
            }
            else
            {
                Log("Result: remote=null (network/cache failure), keeping previous flag: " + UpdateAvailable);
            }
        }

        // version reader
        private static Version? ParseVersion(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim().Trim('\uFEFF', '\u200B', '\u200E', '\u200F');
            var m = Regex.Match(s, @"\d+(?:\.\d+){0,3}");
            if (!m.Success) return null;
            return Version.TryParse(m.Value, out var v) ? v : null;
        }

        private void LoadState()
        {
            try
            {
                if (File.Exists(_statePath))
                    _state = JsonSerializer.Deserialize<State>(File.ReadAllText(_statePath)) ?? new State();
            }
            catch { _state = new State(); }
        }

        private void SaveState()
        {
            try { File.WriteAllText(_statePath, JsonSerializer.Serialize(_state)); } catch { }
        }

        private const long MaxLogBytes = 128 * 1024; // 128 KB
        private const int TailKeepBytes = 32 * 1024; // 32 KB

		// save update log in %LOCALAPPDATA%
        private void Log(string msg)
        {
            try
            {
                RotateLogIfNeeded();
                File.AppendAllText(_logPath, $"{DateTime.Now:HH:mm:ss.fff} {msg}{Environment.NewLine}");
            }
            catch
            {
                Debug.WriteLine("[UpdateService] " + msg);
            }
        }

        private void RotateLogIfNeeded()
        {
            try
            {
                var fi = new FileInfo(_logPath);
                if (!fi.Exists || fi.Length <= MaxLogBytes) return;

                using (var fs = fi.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long keep = Math.Min(TailKeepBytes, fs.Length);
                    fs.Seek(-keep, SeekOrigin.End);

                    using var reader = new StreamReader(fs);
                    string tail = reader.ReadToEnd();

                    var header = $"--- log rolled {DateTime.Now:yyyy-MM-dd HH:mm:ss} (kept last {keep} bytes) ---{Environment.NewLine}";
                    File.WriteAllText(_logPath, header + tail);
                }
            }
            catch
            {
                // 
            }
        }

        private sealed class State
        {
            public string? RawETag { get; set; }
            public string? ApiETag { get; set; }
            public string? RemoteVersion { get; set; }
            public DateTime LastCheckedUtc { get; set; }
            public bool UpdateAvailable { get; set; }
        }
    }
}