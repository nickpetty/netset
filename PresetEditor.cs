using System.Net;
using System.Net.Sockets;

namespace NetSet;

public class PresetEditor : Form
{
    readonly ModernTextBox _name = new() { Dock = DockStyle.Fill };
    readonly Choice _dhcp = new() { Text = "Obtain an IP address automatically (DHCP)", IsRadio = true, Dock = DockStyle.Fill };
    readonly Choice _static = new() { Text = "Use the following IP address", IsRadio = true, Dock = DockStyle.Fill };
    readonly ModernTextBox _ip = new() { Dock = DockStyle.Fill };
    readonly ModernTextBox _mask = new() { Dock = DockStyle.Fill };
    readonly ModernTextBox _gw = new() { Dock = DockStyle.Fill };
    readonly ModernTextBox _dns1 = new() { Dock = DockStyle.Fill };
    readonly ModernTextBox _dns2 = new() { Dock = DockStyle.Fill };
    readonly Choice _fav = new() { Text = "Show in tray menu (favorite)", Dock = DockStyle.Fill };
    readonly TableLayoutPanel _fields = new() { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Margin = Padding.Empty };

    public Preset Result { get; private set; } = new();

    public PresetEditor(Preset? preset, string? adapter = null)
    {
        var p = preset ?? new Preset { Dhcp = false };
        Theme.Init(this);
        Text = preset == null ? "New preset" : "Edit preset";
        Icon = IconFactory.CreateIcon(32);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(this.S(460), this.S(490));

        int gap = this.S(6);
        var nameLabel = Theme.Label("PRESET NAME", dim: true, small: true);
        nameLabel.Margin = new Padding(0, 0, 0, gap);

        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, this.S(130)));
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddField("IP address", _ip);
        AddField("Subnet mask", _mask);
        AddField("Default gateway", _gw);
        AddField("Preferred DNS", _dns1);
        AddField("Alternate DNS", _dns2);
        _fields.Margin = new Padding(this.S(28), gap, 0, gap);

        var save = new ModernButton { Text = "Save", Style = ModernButton.Kind.Primary, Margin = new Padding(8, 0, 0, 0) };
        var cancel = new ModernButton { Text = "Cancel", Margin = Padding.Empty };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Margin = new Padding(0, this.S(16), 0, 0) };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(this.S(24), this.S(20), this.S(24), this.S(20)) };
        _name.Margin = new Padding(0, 0, 0, this.S(12));
        _fav.Margin = new Padding(0, this.S(8), 0, 0);

        foreach (var c in new Control[] { nameLabel, _name, _dhcp, _static, _fields, _fav, buttons }) root.Controls.Add(c);
        Controls.Add(root);

        _name.Text = p.Name;
        _dhcp.Checked = p.Dhcp;
        _static.Checked = !p.Dhcp;
        _ip.Text = p.Ip; _mask.Text = p.Mask; _gw.Text = p.Gateway;
        _dns1.Text = p.Dns1; _dns2.Text = p.Dns2;
        _fav.Checked = p.Favorite;
        if (preset == null && adapter != null) SuggestFromAdapter(adapter);
        _dhcp.CheckedChanged += (_, _) => SyncEnabled();
        SyncEnabled();

        save.Click += (_, _) => TryAccept();
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        Shown += (_, _) => _name.Focus();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter) { TryAccept(); return true; }
        if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>
    /// For a new preset, offers the selected adapter's current settings as gray suggestions
    /// in the empty fields. Tab accepts one; typing replaces it.
    /// </summary>
    void SuggestFromAdapter(string adapter)
    {
        var cur = NetworkConfig.ReadCurrent(adapter);
        if (cur == null) return;

        _name.Placeholder = $"{adapter} (current)";
        _ip.Placeholder = cur.Ip;
        _mask.Placeholder = cur.Mask;
        _gw.Placeholder = cur.Gateway;
        _dns1.Placeholder = cur.Dns1;
        _dns2.Placeholder = cur.Dns2;
        _mask.Text = ""; // the default mask would otherwise hide its suggestion
    }

    void AddField(string label, ModernTextBox box)
    {
        int row = _fields.RowCount++;
        var l = Theme.Label(label, dim: true);
        l.Anchor = AnchorStyles.Left;
        box.Margin = new Padding(0, 4, 0, 4);
        _fields.Controls.Add(l, 0, row);
        _fields.Controls.Add(box, 1, row);
    }

    void SyncEnabled()
    {
        foreach (var box in new[] { _ip, _mask, _gw, _dns1, _dns2 }) box.Editable = !_dhcp.Checked;
    }

    static bool ValidIp(string s) =>
        IPAddress.TryParse(s, out var a) && a.AddressFamily == AddressFamily.InterNetwork && s.Count(c => c == '.') == 3;

    void TryAccept()
    {
        string? error = null;
        if (string.IsNullOrWhiteSpace(_name.Text)) error = "Enter a name for this preset.";
        else if (!_dhcp.Checked)
        {
            if (!ValidIp(_ip.Text.Trim())) error = "IP address is not a valid IPv4 address.";
            else if (!ValidIp(_mask.Text.Trim())) error = "Subnet mask is not valid.";
            else if (_gw.Text.Trim() != "" && !ValidIp(_gw.Text.Trim())) error = "Default gateway is not valid.";
            else if (_dns1.Text.Trim() != "" && !ValidIp(_dns1.Text.Trim())) error = "Preferred DNS is not valid.";
            else if (_dns2.Text.Trim() != "" && !ValidIp(_dns2.Text.Trim())) error = "Alternate DNS is not valid.";
            else if (_dns1.Text.Trim() == "" && _dns2.Text.Trim() != "") error = "Enter a preferred DNS before the alternate.";
        }
        if (error != null)
        {
            ThemedBox.Show(this, error, "NetSet");
            return;
        }

        Result = new Preset
        {
            Name = _name.Text.Trim(),
            Dhcp = _dhcp.Checked,
            Ip = _ip.Text.Trim(), Mask = _mask.Text.Trim(), Gateway = _gw.Text.Trim(),
            Dns1 = _dns1.Text.Trim(), Dns2 = _dns2.Text.Trim(),
            Favorite = _fav.Checked,
        };
        DialogResult = DialogResult.OK;
    }
}
