namespace NetSet;

public class TrayApp : ApplicationContext
{
    readonly NotifyIcon _tray;
    readonly ContextMenuStrip _menu = new();
    MainForm? _window;
    bool _busy;
    UpdateInfo? _update;
    bool _balloonOpensUpdate;
    System.Windows.Forms.Timer? _updateTimer;

    // Must be evaluated before Settings loads (initializers run in order): no settings file means first run.
    readonly bool _firstRun = !SettingsStore.Exists;

    public Settings Settings { get; } = SettingsStore.Load();

    public event Action? PresetsChanged;

    public TrayApp(bool showWindow = false, bool launchedAtStartup = false)
    {
        _tray = new NotifyIcon
        {
            Icon = IconFactory.CreateIcon(SystemInformation.SmallIconSize.Width),
            Visible = true,
            ContextMenuStrip = _menu,
        };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowWindow(); };
        Theme.StyleMenu(_menu);
        _menu.Opening += (_, _) => RebuildMenu();
        _tray.BalloonTipClicked += (_, _) => { if (_balloonOpensUpdate && _update != null) OpenUpdatePage(); };

        if (Settings.Adapter == null || !NetworkConfig.GetAdapters().Any(a => a.Name == Settings.Adapter))
            Settings.Adapter = NetworkConfig.GetAdapters().FirstOrDefault()?.Name;

        UpdateTooltip();

        // Greet a brand-new install with the window, but stay quiet when Windows launches us at logon.
        if (showWindow || (_firstRun && !launchedAtStartup)) ShowWindow();
        if (_firstRun) SettingsStore.Save(Settings); // so the next launch is no longer "first run"
        StartUpdateTimer();
    }

    void Balloon(int ms, string title, string text, ToolTipIcon icon)
    {
        _balloonOpensUpdate = false;
        _tray.ShowBalloonTip(ms, title, text, icon);
    }

    // First check shortly after launch, then once a day while the app stays running.
    void StartUpdateTimer()
    {
        _updateTimer = new System.Windows.Forms.Timer { Interval = 8000 };
        _updateTimer.Tick += async (_, _) =>
        {
            _updateTimer.Interval = (int)TimeSpan.FromHours(24).TotalMilliseconds;
            if (Settings.AutoCheckUpdates) await CheckForUpdatesAsync(manual: false);
        };
        _updateTimer.Start();
    }

    async Task CheckForUpdatesAsync(bool manual)
    {
        var (update, error) = await UpdateChecker.CheckAsync();
        _update = update;

        if (update != null)
        {
            // Automatic checks announce each version once; a manual check always answers.
            if (manual || Settings.NotifiedUpdate != update.Tag)
            {
                Settings.NotifiedUpdate = update.Tag;
                SettingsStore.Save(Settings);
                Balloon(8000, "NetSet update available", $"{update.Tag} is out (you have v{UpdateChecker.CurrentText}). Click to open the download page.", ToolTipIcon.Info);
                _balloonOpensUpdate = true;
            }
        }
        else if (manual)
        {
            Balloon(4000, "NetSet", error ?? $"You're up to date (v{UpdateChecker.CurrentText}).", error == null ? ToolTipIcon.Info : ToolTipIcon.Warning);
        }
    }

    void OpenUpdatePage()
    {
        if (_update == null) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_update.Url) { UseShellExecute = true }); }
        catch (Exception ex) { Balloon(4000, "NetSet", $"Could not open the browser: {ex.Message}", ToolTipIcon.Warning); }
    }

    public void Save()
    {
        SettingsStore.Save(Settings);
        PresetsChanged?.Invoke();
    }

    public void SetAdapter(string? name)
    {
        Settings.Adapter = name;
        SettingsStore.Save(Settings);
        UpdateTooltip();
    }

    void UpdateTooltip() =>
        _tray.Text = Settings.Adapter == null ? "NetSet" : Truncate($"NetSet - {Settings.Adapter}", 63);

    static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    public void ShowWindow()
    {
        if (_window == null || _window.IsDisposed)
            _window = new MainForm(this);
        _window.Show();
        if (_window.WindowState == FormWindowState.Minimized) _window.WindowState = FormWindowState.Normal;
        _window.Activate();
    }

    void RebuildMenu()
    {
        _menu.Items.Clear();

        if (_update != null)
        {
            var upd = new ToolStripMenuItem($"Update available: {_update.Tag}") { ForeColor = Theme.Accent };
            upd.Click += (_, _) => OpenUpdatePage();
            _menu.Items.Add(upd);
            _menu.Items.Add(new ToolStripSeparator());
        }

        _menu.Items.Add(new ToolStripMenuItem($"Adapter: {Settings.Adapter ?? "(none)"}") { Enabled = false });
        _menu.Items.Add(new ToolStripMenuItem(NetworkConfig.Summary(Settings.Adapter)) { Enabled = false });

        var adapterMenu = new ToolStripMenuItem("Change adapter");
        Theme.StyleMenu((ToolStripDropDownMenu)adapterMenu.DropDown);
        foreach (var nic in NetworkConfig.GetAdapters())
        {
            string name = nic.Name;
            var item = new ToolStripMenuItem(name) { Checked = name == Settings.Adapter };
            item.Click += (_, _) => { SetAdapter(name); PresetsChanged?.Invoke(); };
            adapterMenu.DropDownItems.Add(item);
        }
        _menu.Items.Add(adapterMenu);
        _menu.Items.Add(new ToolStripSeparator());

        var favs = Settings.Presets.Where(p => p.Favorite).ToList();
        if (favs.Count == 0)
            _menu.Items.Add(new ToolStripMenuItem("No favorite presets") { Enabled = false });
        foreach (var p in favs)
        {
            var preset = p;
            // Re-read from the adapter every time the menu opens, so changes made elsewhere uncheck it.
            var item = new ToolStripMenuItem(preset.Name)
            {
                Enabled = Settings.Adapter != null && !_busy,
                Checked = NetworkConfig.Matches(Settings.Adapter, preset),
            };
            item.ToolTipText = preset.Dhcp ? "DHCP" : preset.Ip;
            item.Click += async (_, _) => await ApplyAsync(preset, interactive: false);
            _menu.Items.Add(item);
        }

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Open NetSet", null, (_, _) => ShowWindow());

        var startup = new ToolStripMenuItem("Run at startup") { Checked = StartupTask.IsEnabled };
        startup.Click += (_, _) => ToggleStartup(!startup.Checked);
        _menu.Items.Add(startup);

        var updates = new ToolStripMenuItem("Updates");
        Theme.StyleMenu((ToolStripDropDownMenu)updates.DropDown);
        updates.DropDownItems.Add(new ToolStripMenuItem($"NetSet v{UpdateChecker.CurrentText}") { Enabled = false });
        updates.DropDownItems.Add("Check for updates now", null, async (_, _) => await CheckForUpdatesAsync(manual: true));
        var auto = new ToolStripMenuItem("Check automatically") { Checked = Settings.AutoCheckUpdates };
        auto.Click += (_, _) => { Settings.AutoCheckUpdates = !Settings.AutoCheckUpdates; SettingsStore.Save(Settings); };
        updates.DropDownItems.Add(auto);
        _menu.Items.Add(updates);

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit", null, (_, _) => ExitThread());

        PadItems(_menu.Items);
    }

    void PadItems(ToolStripItemCollection items)
    {
        foreach (ToolStripItem i in items)
        {
            if (i is not ToolStripMenuItem m) continue;
            m.Padding = new Padding(0, 6, 0, 6);
            PadItems(m.DropDownItems);
        }
    }

    void ToggleStartup(bool enable)
    {
        var (ok, output) = StartupTask.Set(enable);
        if (ok)
            Balloon(2000, "NetSet", enable ? "Will start with Windows" : "Removed from startup", ToolTipIcon.Info);
        else
            Balloon(5000, "NetSet - startup change failed", Truncate(output, 200), ToolTipIcon.Error);
    }

    /// <summary>Applies a preset to the selected adapter. Returns true on success.</summary>
    public async Task<bool> ApplyAsync(Preset preset, bool interactive)
    {
        if (Settings.Adapter == null || _busy) return false;
        _busy = true;
        string adapter = Settings.Adapter;
        try
        {
            var (ok, output) = await NetworkConfig.ApplyAsync(adapter, preset);
            if (ok)
            {
                Balloon(2500, "NetSet", $"{adapter}: {preset.Name} applied", ToolTipIcon.Info);
            }
            else if (interactive && _window is { IsDisposed: false, Visible: true })
            {
                ThemedBox.Show(_window, output, "Could not apply preset");
            }
            else
            {
                Balloon(5000, "NetSet - failed", Truncate(output, 200), ToolTipIcon.Error);
            }
            return ok;
        }
        finally
        {
            _busy = false;
        }
    }

    protected override void ExitThreadCore()
    {
        _updateTimer?.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _window?.Dispose();
        base.ExitThreadCore();
    }
}
