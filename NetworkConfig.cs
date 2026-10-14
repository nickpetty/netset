using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace NetSet;

public static class NetworkConfig
{
    public static List<NetworkInterface> GetAdapters() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .OrderBy(n => n.Name)
            .ToList();

    public static List<(string Label, string Value)> Describe(string? adapterName)
    {
        var nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Name == adapterName);
        if (nic == null) return new() { ("Status", "No adapter selected") };

        var props = nic.GetIPProperties();
        var addr = props.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
        var gw = props.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
        var dns = props.DnsAddresses.Where(d => d.AddressFamily == AddressFamily.InterNetwork).ToList();
        bool dhcp = false;
        try { dhcp = props.GetIPv4Properties().IsDhcpEnabled; } catch { }

        return new()
        {
            ("Status", nic.OperationalStatus.ToString()),
            ("Configured", dhcp ? "DHCP" : "Static"),
            ("IP address", addr?.Address.ToString() ?? "-"),
            ("Subnet mask", addr?.IPv4Mask?.ToString() ?? "-"),
            ("Gateway", gw?.Address.ToString() ?? "-"),
            ("DNS", dns.Count > 0 ? string.Join(", ", dns) : "-"),
            ("MAC", nic.GetPhysicalAddress().ToString()),
        };
    }

    /// <summary>Reads an adapter's current IPv4 settings into a preset (Name left blank).</summary>
    public static Preset? ReadCurrent(string? adapterName)
    {
        var nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Name == adapterName);
        if (nic == null) return null;

        var props = nic.GetIPProperties();
        var addr = props.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
        var gw = props.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
        var dns = props.DnsAddresses.Where(d => d.AddressFamily == AddressFamily.InterNetwork).Select(d => d.ToString()).ToList();
        bool dhcp = false;
        try { dhcp = props.GetIPv4Properties().IsDhcpEnabled; } catch { }

        return new Preset
        {
            Dhcp = dhcp,
            Ip = addr?.Address.ToString() ?? "",
            Mask = addr?.IPv4Mask?.ToString() ?? "255.255.255.0",
            Gateway = gw?.Address.ToString() ?? "",
            Dns1 = dns.ElementAtOrDefault(0) ?? "",
            Dns2 = dns.ElementAtOrDefault(1) ?? "",
        };
    }

    /// <summary>One-line description for the tray menu, e.g. "192.168.1.66 (Static)".</summary>
    public static string Summary(string? adapterName)
    {
        var nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Name == adapterName);
        var cur = ReadCurrent(adapterName);
        if (nic == null || cur == null) return "No adapter";
        if (nic.OperationalStatus != OperationalStatus.Up) return "Disconnected";
        if (cur.Ip == "") return "No IP address";
        return $"{cur.Ip} ({(cur.Dhcp ? "DHCP" : "Static")})";
    }

    /// <summary>True when the adapter's current IPv4 configuration is what the preset would set.</summary>
    public static bool Matches(string? adapterName, Preset p)
    {
        var cur = ReadCurrent(adapterName);
        if (cur == null) return false;
        if (p.Dhcp || cur.Dhcp) return p.Dhcp && cur.Dhcp; // DNS source is not exposed, so DHCP matches on mode alone

        static string[] Dns(Preset x) => new[] { x.Dns1, x.Dns2 }.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.Trim()).ToArray();
        return cur.Ip == p.Ip.Trim()
            && cur.Mask == p.Mask.Trim()
            && cur.Gateway == p.Gateway.Trim()
            && Dns(cur).SequenceEqual(Dns(p));
    }

    public static async Task<(bool ok, string output)> ApplyAsync(string adapter, Preset p)
    {
        var steps = new List<string[]>();
        string name = $"name={adapter}";

        if (p.Dhcp)
        {
            steps.Add(new[] { "interface", "ip", "set", "address", name, "source=dhcp" });
            steps.Add(new[] { "interface", "ip", "set", "dnsservers", name, "source=dhcp" });
        }
        else
        {
            var addr = new List<string> { "interface", "ip", "set", "address", name, "static", p.Ip, p.Mask };
            if (!string.IsNullOrWhiteSpace(p.Gateway)) { addr.Add(p.Gateway); addr.Add("1"); }
            steps.Add(addr.ToArray());

            if (string.IsNullOrWhiteSpace(p.Dns1))
            {
                steps.Add(new[] { "interface", "ip", "set", "dnsservers", name, "source=static", "address=none" });
            }
            else
            {
                steps.Add(new[] { "interface", "ip", "set", "dnsservers", name, "static", p.Dns1, "primary", "validate=no" });
                if (!string.IsNullOrWhiteSpace(p.Dns2))
                    steps.Add(new[] { "interface", "ip", "add", "dnsservers", name, p.Dns2, "index=2", "validate=no" });
            }
        }

        var log = new StringBuilder();
        foreach (var step in steps)
        {
            var (code, text) = await Task.Run(() => RunNetsh(step));
            log.AppendLine(text.Trim());
            if (code != 0) return (false, log.ToString().Trim());
        }
        return (true, log.ToString().Trim());
    }

    static (int code, string output) RunNetsh(string[] args)
    {
        var psi = new ProcessStartInfo("netsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)!;
        string text = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        return (proc.ExitCode, text);
    }
}
