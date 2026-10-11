namespace NetSet;

public class TrayApp : ApplicationContext
{
    readonly NotifyIcon _tray;
    readonly ContextMenuStrip _menu = new();
    MainForm? _window;
    bool _busy;

    public Settings Settings { get; } = SettingsStore.Load();

    public event Action? PresetsChanged;

    public TrayApp(bool showWindow = false)
    {
        _tray = new NotifyIcon
        {
            Icon = IconFactory.CreateIcon(SystemInformation.SmallIconSize.Width),
            Visible = true,
            ContextMenuStrip = _menu,
        };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowWindow(); };
        Theme.StyleMenu(_menu);
        _menu.Opening +=(_, _) => RebuildMenu();

        if (Settings.Adapter == null || !NetworkConfig.GetAdapters().Any(a => a.Name == Settings.Adapter))
            Settings.Adapter = NetworkConfig.GetAdapters().FirstOrDefault()?.Name;

        UpdateTooltip();
        if (showWindow) ShowWindow();
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

        var header = new ToolStripMenuItem($"Adapter: {Settings.Adapter ?? "(none)"}") { Enabled = false };
        _menu.Items.Add(header);

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
            var item = new ToolStripMenuItem(preset.Name) { Enabled = Settings.Adapter != null && !_busy };
            item.ToolTipText = preset.Dhcp ? "DHCP" : preset.Ip;
            item.Click += async (_, _) => await ApplyAsync(preset, interactive: false);
            _menu.Items.Add(item);
        }

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Open NetSet", null, (_, _) => ShowWindow());

        var startup = new ToolStripMenuItem("Run at startup") { Checked = StartupTask.IsEnabled };
        startup.Click += (_, _) => ToggleStartup(!startup.Checked);
        _menu.Items.Add(startup);

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
            _tray.ShowBalloonTip(2000, "NetSet", enable ? "Will start with Windows" : "Removed from startup", ToolTipIcon.Info);
        else
            _tray.ShowBalloonTip(5000, "NetSet - startup change failed", Truncate(output, 200), ToolTipIcon.Error);
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
                _tray.ShowBalloonTip(2500, "NetSet", $"{adapter}: {preset.Name} applied", ToolTipIcon.Info);
            }
            else if (interactive && _window is { IsDisposed: false, Visible: true })
            {
                ThemedBox.Show(_window, output, "Could not apply preset");
            }
            else
            {
                _tray.ShowBalloonTip(5000, "NetSet - failed", Truncate(output, 200), ToolTipIcon.Error);
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
        _tray.Visible = false;
        _tray.Dispose();
        _window?.Dispose();
        base.ExitThreadCore();
    }
}
