namespace NetSet;

public class MainForm : Form
{
    readonly TrayApp _app;
    readonly ModernButton _adapterBtn = new() { LeftAlign = true, Chevron = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
    readonly InfoCard _info = new() { Dock = DockStyle.Fill };
    readonly ModernList _list = new() { Dock = DockStyle.Fill };
    readonly ModernButton _apply = new() { Text = "Apply", Style = ModernButton.Kind.Primary };
    readonly ModernButton _new = new() { Text = "New" };
    readonly ModernButton _edit = new() { Text = "Edit" };
    readonly ModernButton _fav = new() { Text = "Favorite" };
    readonly ModernButton _import = new() { Text = "Import" };
    readonly ModernButton _export = new() { Text = "Export" };
    readonly ModernButton _delete =new() { Text = "Delete", Style = ModernButton.Kind.Danger, Margin = Padding.Empty };
    string? _adapter;

    public MainForm(TrayApp app)
    {
        _app = app;
        Theme.Init(this);
        Text = "NetSet";
        Icon = IconFactory.CreateIcon(32);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(this.S(720), this.S(560));
        MinimumSize = new Size(this.S(720), this.S(500));

        _list.Columns.Add("", 48);
        _list.Columns.Add("NAME", 160);
        _list.Columns.Add("MODE", 80);
        _list.Columns.Add("IP ADDRESS", 120);
        _list.Columns.Add("GATEWAY", 120);
        _list.Columns.Add("DNS", 150);

        var listCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(this.S(4)), Margin = Padding.Empty };
        listCard.Controls.Add(_list);

        var presetsHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Margin = new Padding(0, this.S(20), 0, this.S(8)) };
        presetsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        presetsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var presetsLabel = Theme.Label("PRESETS", dim: true, small: true);
        var hint = Theme.Label("Double-click to apply  ·  click ★ to favorite", dim: true, small: true);
        hint.Anchor = AnchorStyles.Right;
        presetsHeader.Controls.Add(presetsLabel, 0, 0);
        presetsHeader.Controls.Add(hint, 1, 0);

        var left = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty, Dock = DockStyle.Fill };
        left.Controls.AddRange(new Control[] { _apply, _new, _edit, _fav, _import, _export });
        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Margin = new Padding(0, this.S(16), 0, 0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.Controls.Add(left, 0, 0);
        buttons.Controls.Add(_delete, 1, 0);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(this.S(24), this.S(20), this.S(24), this.S(24)) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var adapterLabel = Theme.Label("NETWORK ADAPTER", dim: true, small: true);
        adapterLabel.Margin = new Padding(0, 0, 0, this.S(8));
        _info.Margin = new Padding(0, this.S(12), 0, 0);
        root.Controls.Add(adapterLabel, 0, 0);
        root.Controls.Add(_adapterBtn, 0, 1);
        root.Controls.Add(_info, 0, 2);
        root.Controls.Add(presetsHeader, 0, 3);
        root.Controls.Add(listCard, 0, 4);
        root.Controls.Add(buttons, 0, 5);
        Controls.Add(root);

        _adapterBtn.Click += (_, _) => ShowAdapterMenu();
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();
        _list.DoubleClick += async (_, _) => await ApplySelected();
        _list.MouseDown += OnListMouseDown;
        _list.SizeChanged += (_, _) => FitColumns();
        _apply.Click += async (_, _) => await ApplySelected();
        _new.Click += (_, _) => EditPreset(null);
        _edit.Click += (_, _) => EditPreset(Selected);
        _delete.Click += (_, _) => DeleteSelected();
        _fav.Click += (_, _) => ToggleFavorite();
        _import.Click += (_, _) => ImportPresets();
        _export.Click += (_, _) => ExportPresets();

        _app.PresetsChanged += OnExternalChange;
        Shown += (_, _) => { LoadAdapters(); LoadPresets(); FitColumns(); };
        Activated += (_, _) => RefreshCurrent();
        FormClosing += (_, e) =>
        {
            // Closing the window just tucks it back into the tray.
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        };
        FormClosed += (_, _) => _app.PresetsChanged -= OnExternalChange;
    }

    Preset? Selected => _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as Preset : null;

    void FitColumns()
    {
        int w = _list.ClientSize.Width;
        if (w <= 0 || _list.Columns.Count < 6) return;
        int fav = this.S(48), mode = this.S(80), rest = w - fav - mode;
        int name = (int)(rest * 0.30), ip = (int)(rest * 0.22), gw = (int)(rest * 0.22);
        _list.Columns[0].Width = fav;
        _list.Columns[1].Width = name;
        _list.Columns[2].Width = mode;
        _list.Columns[3].Width = ip;
        _list.Columns[4].Width = gw;
        _list.Columns[5].Width = rest - name - ip - gw;
    }

    void OnListMouseDown(object? sender, MouseEventArgs e)
    {
        var hit = _list.GetItemAt(e.X, e.Y);
        if (hit == null || e.Button != MouseButtons.Left || e.X > _list.Columns[0].Width) return;
        hit.Selected = true;
        ToggleFavorite();
    }

    void OnExternalChange()
    {
        if (!IsHandleCreated) return;
        BeginInvoke(() => { LoadAdapters(); LoadPresets(); });
    }

    void LoadAdapters()
    {
        var names = NetworkConfig.GetAdapters().Select(n => n.Name).ToList();
        _adapter = _app.Settings.Adapter != null && names.Contains(_app.Settings.Adapter) ? _app.Settings.Adapter : names.FirstOrDefault();
        if (_adapter != _app.Settings.Adapter) _app.SetAdapter(_adapter);
        _adapterBtn.Text = _adapter ?? "No adapters found";
        RefreshCurrent();
        UpdateButtons();
    }

    void ShowAdapterMenu()
    {
        var menu = new ContextMenuStrip();
        Theme.StyleMenu(menu);
        menu.MinimumSize = new Size(_adapterBtn.Width, 0);
        foreach (var nic in NetworkConfig.GetAdapters())
        {
            string name = nic.Name;
            var item = new ToolStripMenuItem(name) { Checked = name == _adapter, Padding = new Padding(0, this.S(6), 0, this.S(6)) };
            item.Click += (_, _) =>
            {
                _app.SetAdapter(name);
                LoadAdapters();
            };
            menu.Items.Add(item);
        }
        if (menu.Items.Count == 0) menu.Items.Add(new ToolStripMenuItem("No adapters found") { Enabled = false });
        menu.Show(_adapterBtn, new Point(0, _adapterBtn.Height + 4));
    }

    void RefreshCurrent() => _info.SetRows(NetworkConfig.Describe(_adapter));

    void LoadPresets()
    {
        var keep = Selected;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var p in _app.Settings.Presets)
        {
            var item = new ListViewItem(p.Favorite ? "★" : "") { Tag = p };
            item.SubItems.Add(p.Name);
            item.SubItems.Add(p.Dhcp ? "DHCP" : "Static");
            item.SubItems.Add(p.Dhcp ? "-" : p.Ip);
            item.SubItems.Add(p.Dhcp || p.Gateway == "" ? "-" : p.Gateway);
            item.SubItems.Add(p.Dhcp ? "-" : string.Join(", ", new[] { p.Dns1, p.Dns2 }.Where(d => d != "")));
            item.Selected = p == keep;
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        UpdateButtons();
    }

    void UpdateButtons()
    {
        bool has = Selected != null;
        _apply.Enabled = has && _adapter != null;
        _edit.Enabled = _delete.Enabled = _fav.Enabled = has;
        _export.Enabled = _app.Settings.Presets.Count > 0;
    }

    const string FileFilter = "NetSet presets (*.json)|*.json|All files (*.*)|*.*";

    void ExportPresets()
    {
        using var dlg = new SaveFileDialog
        {
            Title = "Export presets",
            Filter = FileFilter,
            FileName = "netset-presets.json",
            DefaultExt = "json",
            OverwritePrompt = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            PresetFile.Export(dlg.FileName, _app.Settings.Presets);
            ThemedBox.Show(this, $"Exported {_app.Settings.Presets.Count} preset(s).", "Export complete");
        }
        catch (Exception ex)
        {
            ThemedBox.Show(this, ex.Message, "Export failed");
        }
    }

    void ImportPresets()
    {
        using var dlg = new OpenFileDialog { Title = "Import presets", Filter = FileFilter, CheckFileExists = true };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        List<Preset> incoming;
        try { incoming = PresetFile.Import(dlg.FileName); }
        catch (Exception ex)
        {
            ThemedBox.Show(this, ex.Message, "Import failed");
            return;
        }

        var presets = _app.Settings.Presets;
        int choice = presets.Count == 0 ? 0 : ThemedBox.Choose(this,
            $"Found {incoming.Count} preset(s) in the file. You have {presets.Count} saved.\n\n" +
            "Merge adds them to your list, replacing any preset with the same name.\n" +
            "Replace deletes your current presets and uses only the imported ones.",
            "Import presets", "Merge", "Replace", "Cancel");

        switch (choice)
        {
            case 0:
                foreach (var p in incoming)
                {
                    int i = presets.FindIndex(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase));
                    if (i >= 0) presets[i] = p; else presets.Add(p);
                }
                break;
            case 1:
                presets.Clear();
                presets.AddRange(incoming);
                break;
            default:
                return;
        }
        _app.Save();
    }

    async Task ApplySelected()
    {
        if (Selected is not { } p || _adapter == null) return;
        _apply.Enabled = false;
        UseWaitCursor = true;
        try { await _app.ApplyAsync(p, interactive: true); }
        finally { UseWaitCursor = false; UpdateButtons(); }
        await Task.Delay(500); // let Windows settle before re-reading
        RefreshCurrent();
    }

    void EditPreset(Preset? existing)
    {
        using var dlg = new PresetEditor(existing?.Clone());
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        if (existing == null) _app.Settings.Presets.Add(dlg.Result);
        else _app.Settings.Presets[_app.Settings.Presets.IndexOf(existing)] = dlg.Result;
        _app.Save();
    }

    void DeleteSelected()
    {
        if (Selected is not { } p) return;
        if (!ThemedBox.Show(this, $"Delete preset \"{p.Name}\"?", "NetSet", confirm: true)) return;
        _app.Settings.Presets.Remove(p);
        _app.Save();
    }

    void ToggleFavorite()
    {
        if (Selected is not { } p) return;
        p.Favorite = !p.Favorite;
        _app.Save();
    }
}
