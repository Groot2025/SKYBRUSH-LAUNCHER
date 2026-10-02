using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SkybrushControlPanel());
    }
}

internal sealed class SkybrushControlPanel : Form
{
    private const string ServerFolder = "skybrush-server-2.52.0\\skybrush-server-2.52.0";
    private const string ConfigFile = "etc\\conf\\skybrush725.jsonc";
    private readonly ComboBox adapters = new ComboBox();
    private readonly TextBox address = new TextBox();
    private readonly TextBox mask = new TextBox();
    private readonly TextBox gateway = new TextBox();
    private readonly TextBox dns = new TextBox();
    private readonly TextBox pingAddress = new TextBox();
    private readonly Label networkStatus = new Label();
    private readonly Label pingStatus = new Label();
    private readonly Label launchStatus = new Label();

    public SkybrushControlPanel()
    {
        Text = "Skybrush Control Center";
        ClientSize = new Size(760, 650);
        MinimumSize = new Size(776, 689);
        BackColor = Color.FromArgb(17, 24, 39);
        Font = new Font("Segoe UI", 9F);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BuildInterface();
        LoadAdapters();
    }

    private void BuildInterface()
    {
        Label title = LabelOf("SKYBRUSH  /  CONTROL CENTER", 22F, Color.White, true);
        title.Location = new Point(30, 22); title.AutoSize = true; Controls.Add(title);
        Label subtitle = LabelOf("Network configuration, connectivity tests, and flight-control launch", 9F, Color.FromArgb(148, 163, 184), false);
        subtitle.Location = new Point(32, 60); subtitle.AutoSize = true; Controls.Add(subtitle);

        Panel launch = PanelAt(30, 96, 700, 105); Controls.Add(launch);
        PanelTitle(launch, "FLIGHT CONTROL", "Start the Skybrush server and Skybrush Live");
        Button launchButton = ButtonOf("Launch Server + Live", Color.FromArgb(14, 165, 233));
        launchButton.Location = new Point(475, 31); launchButton.Size = new Size(190, 42);
        launchButton.Click += delegate { LaunchSkybrush(); }; launch.Controls.Add(launchButton);
        launchStatus.Location = new Point(20, 76); launchStatus.AutoSize = true; launchStatus.ForeColor = Color.FromArgb(125, 211, 252); launch.Controls.Add(launchStatus);

        Panel network = PanelAt(30, 218, 700, 275); Controls.Add(network);
        PanelTitle(network, "ETHERNET CONFIGURATION", "Set a static IPv4 address for a network adapter");
        Field(network, "Network adapter", adapters, 20, 66, 420, 30);
        adapters.DropDownStyle = ComboBoxStyle.DropDownList;
        adapters.SelectedIndexChanged += delegate { ShowAdapterDetails(); };
        Button refresh = ButtonOf("Refresh", Color.FromArgb(71, 85, 105));
        refresh.Location = new Point(460, 66); refresh.Size = new Size(100, 30); refresh.Click += delegate { LoadAdapters(); }; network.Controls.Add(refresh);
        Field(network, "IP address", address, 20, 120, 190, 30);
        Field(network, "Subnet mask", mask, 230, 120, 190, 30);
        Field(network, "Gateway (optional)", gateway, 440, 120, 190, 30);
        Field(network, "Preferred DNS (optional)", dns, 20, 178, 250, 30);
        Button apply = ButtonOf("Apply Static IP", Color.FromArgb(22, 163, 74));
        apply.Location = new Point(440, 178); apply.Size = new Size(190, 32); apply.Click += delegate { ApplyStaticIp(); }; network.Controls.Add(apply);
        networkStatus.Location = new Point(20, 228); networkStatus.Size = new Size(650, 30); networkStatus.ForeColor = Color.FromArgb(148, 163, 184); network.Controls.Add(networkStatus);

        Panel ping = PanelAt(30, 510, 700, 110); Controls.Add(ping);
        PanelTitle(ping, "CONNECTIVITY TEST", "Enter an IPv4 address to send a ping");
        pingAddress.Location = new Point(20, 62); pingAddress.Size = new Size(340, 25); pingAddress.Font = new Font("Consolas", 11F); pingAddress.Text = "192.168.1.1";
        pingAddress.KeyDown += PingKeyDown; ping.Controls.Add(pingAddress);
        Button pingButton = ButtonOf("Ping IP", Color.FromArgb(139, 92, 246));
        pingButton.Location = new Point(380, 60); pingButton.Size = new Size(110, 30); pingButton.Click += delegate { PingAddress(); }; ping.Controls.Add(pingButton);
        pingStatus.Location = new Point(505, 66); pingStatus.AutoSize = true; pingStatus.ForeColor = Color.FromArgb(196, 181, 253); ping.Controls.Add(pingStatus);
    }

    private void LoadAdapters()
    {
        string selected = adapters.SelectedItem == null ? null : adapters.SelectedItem.ToString();
        adapters.Items.Clear();
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            if (adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet || adapter.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet) adapters.Items.Add(adapter.Name);
        if (adapters.Items.Count == 0) { networkStatus.Text = "No Ethernet adapter was found."; return; }
        if (selected != null && adapters.Items.Contains(selected)) adapters.SelectedItem = selected; else adapters.SelectedIndex = 0;
        ShowAdapterDetails();
    }

    private void ShowAdapterDetails()
    {
        if (adapters.SelectedItem == null) return;
        NetworkInterface adapter = FindAdapter(adapters.SelectedItem.ToString()); if (adapter == null) return;
        IPInterfaceProperties properties = adapter.GetIPProperties();
        foreach (UnicastIPAddressInformation ip in properties.UnicastAddresses)
            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) { address.Text = ip.Address.ToString(); mask.Text = ip.IPv4Mask == null ? "" : ip.IPv4Mask.ToString(); break; }
        gateway.Text = properties.GatewayAddresses.Count == 0 ? "" : properties.GatewayAddresses[0].Address.ToString();
        dns.Text = properties.DnsAddresses.Count == 0 ? "" : properties.DnsAddresses[0].ToString();
        networkStatus.Text = adapter.OperationalStatus == OperationalStatus.Up ? "Adapter is connected." : "Adapter is not connected.";
    }

    private void ApplyStaticIp()
    {
        if (adapters.SelectedItem == null) { SetNetworkStatus("Select an Ethernet adapter first.", Color.FromArgb(251, 113, 133)); return; }
        if (!ValidIp(address.Text) || !ValidIp(mask.Text) || (!String.IsNullOrWhiteSpace(gateway.Text) && !ValidIp(gateway.Text)) || (!String.IsNullOrWhiteSpace(dns.Text) && !ValidIp(dns.Text)))
        { SetNetworkStatus("Enter valid IPv4 values for IP address, subnet mask, gateway, and DNS.", Color.FromArgb(251, 113, 133)); return; }
        string name = adapters.SelectedItem.ToString();
        string gatewayValue = String.IsNullOrWhiteSpace(gateway.Text) ? "none" : gateway.Text.Trim();
        string command = "netsh interface ipv4 set address name=\"" + name + "\" source=static address=" + address.Text.Trim() + " mask=" + mask.Text.Trim() + " gateway=" + gatewayValue;
        if (!String.IsNullOrWhiteSpace(dns.Text)) command += " & netsh interface ipv4 set dnsservers name=\"" + name + "\" source=static address=" + dns.Text.Trim() + " validate=no";
        try
        {
            Process p = Process.Start(new ProcessStartInfo("cmd.exe", "/c " + command) { Verb = "runas", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
            p.WaitForExit();
            if (p.ExitCode == 0) { SetNetworkStatus("Static IP configuration applied.", Color.FromArgb(134, 239, 172)); LoadAdapters(); }
            else SetNetworkStatus("Windows could not apply the network configuration.", Color.FromArgb(251, 113, 133));
        }
        catch (System.ComponentModel.Win32Exception) { SetNetworkStatus("Administrator permission was not granted; no settings were changed.", Color.FromArgb(251, 113, 133)); }
    }

    private void PingAddress()
    {
        if (!ValidIp(pingAddress.Text)) { pingStatus.ForeColor = Color.FromArgb(251, 113, 133); pingStatus.Text = "Enter a valid IPv4 address."; return; }
        pingStatus.ForeColor = Color.FromArgb(196, 181, 253); pingStatus.Text = "Pinging..."; Application.DoEvents();
        try
        {
            PingReply reply = new Ping().Send(pingAddress.Text.Trim(), 2000);
            if (reply.Status == IPStatus.Success) { pingStatus.ForeColor = Color.FromArgb(134, 239, 172); pingStatus.Text = "Online: " + reply.RoundtripTime + " ms"; }
            else { pingStatus.ForeColor = Color.FromArgb(251, 113, 133); pingStatus.Text = reply.Status.ToString(); }
        }
        catch (PingException) { pingStatus.ForeColor = Color.FromArgb(251, 113, 133); pingStatus.Text = "No reply."; }
    }

    private void PingKeyDown(object sender, KeyEventArgs args) { if (args.KeyCode == Keys.Enter) { PingAddress(); args.SuppressKeyPress = true; } }

    private void LaunchSkybrush()
    {
        try
        {
            string server = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ServerFolder);
            string config = Path.Combine(server, ConfigFile);
            string live = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Skybrush Live", "Skybrush Live.exe");
            EnsureExists(server, "Skybrush Server folder"); EnsureExists(config, "Skybrush configuration"); EnsureExists(live, "Skybrush Live");
            string command = "\"" + FindUv() + "\" run skybrushd -c .\\etc\\conf\\skybrush725.jsonc";
            Process.Start(new ProcessStartInfo("cmd.exe", "/k title Skybrush Server && " + command) { WorkingDirectory = server, UseShellExecute = true });
            launchStatus.Text = "Server started — opening Skybrush Live..."; Application.DoEvents(); Thread.Sleep(2000);
            Process.Start(new ProcessStartInfo(live) { UseShellExecute = true }); launchStatus.Text = "Skybrush Server and Live are running.";
        }
        catch (Exception exception) { launchStatus.ForeColor = Color.FromArgb(251, 113, 133); launchStatus.Text = "Launch failed: " + exception.Message; }
    }

    private static NetworkInterface FindAdapter(string name) { foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces()) if (adapter.Name == name) return adapter; return null; }
    private static bool ValidIp(string value) { IPAddress parsed; return IPAddress.TryParse(value.Trim(), out parsed) && parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork; }
    private static void EnsureExists(string path, string description) { if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException(description + " was not found at: " + path); }
    private static string FindUv()
    {
        string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "uv.exe"); if (File.Exists(local)) return local;
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)) { string candidate = Path.Combine(directory.Trim().Trim('"'), "uv.exe"); if (File.Exists(candidate)) return candidate; }
        throw new FileNotFoundException("uv.exe was not found. Install uv, then try again.");
    }
    private void SetNetworkStatus(string text, Color color) { networkStatus.ForeColor = color; networkStatus.Text = text; }
    private static Panel PanelAt(int x, int y, int width, int height) { Panel panel = new Panel(); panel.Location = new Point(x, y); panel.Size = new Size(width, height); panel.BackColor = Color.FromArgb(30, 41, 59); panel.BorderStyle = BorderStyle.FixedSingle; return panel; }
    private static Label LabelOf(string text, float size, Color color, bool bold) { Label label = new Label(); label.Text = text; label.Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular); label.ForeColor = color; return label; }
    private static void PanelTitle(Panel panel, string title, string description) { Label heading = LabelOf(title, 10F, Color.FromArgb(226, 232, 240), true); heading.Location = new Point(19, 13); heading.AutoSize = true; panel.Controls.Add(heading); Label text = LabelOf(description, 9F, Color.FromArgb(148, 163, 184), false); text.Location = new Point(20, 36); text.AutoSize = true; panel.Controls.Add(text); }
    private static void Field(Panel panel, string caption, Control field, int x, int y, int width, int height) { Label label = LabelOf(caption, 9F, Color.FromArgb(203, 213, 225), false); label.Location = new Point(x, y - 20); label.AutoSize = true; panel.Controls.Add(label); field.Location = new Point(x, y); field.Size = new Size(width, height); field.BackColor = Color.FromArgb(241, 245, 249); panel.Controls.Add(field); }
    private static Button ButtonOf(string text, Color color) { Button button = new Button(); button.Text = text; button.BackColor = color; button.ForeColor = Color.White; button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0; button.Font = new Font("Segoe UI", 9F, FontStyle.Bold); button.Cursor = Cursors.Hand; return button; }
}
