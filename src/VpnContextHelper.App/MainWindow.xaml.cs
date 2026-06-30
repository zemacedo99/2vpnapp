using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using VpnContextHelper.Core.Context;

namespace VpnContextHelper.App;

public partial class MainWindow : Window
{
    private readonly App _app;
    private bool _allowClose;

    public string ConfigSource { get; set; } = string.Empty;

    public MainWindow(App app)
    {
        _app = app;
        InitializeComponent();
    }

    public void Update(NetworkContext ctx)
    {
        SubtitleText.Text =
            "Read-only helper. It never connects/disconnects VPNs, changes routes, or touches credentials/MFA/SAML.\n" +
            $"Config: {ConfigSource}";

        TrustedText.Text = $"Trusted network: {(ctx.TrustedNetworkDetected ? "yes (on-net)" : "no")}";

        VpnText.Text = ctx.Vpn.Connected
            ? $"VPN tunnel: {(ctx.Vpn.IsFullTunnel ? "FULL" : "SPLIT")} @ {ctx.Vpn.TunnelIp}  ·  routed: {string.Join(", ", ctx.Vpn.TunneledCidrs.Select(c => c.ToString()))}"
            : "VPN tunnel: none active";

        CompanyText.Text = $"{ctx.Company.Label}: {ctx.Company.Verdict}";
        ClientText.Text = $"{ctx.Client.Label}: {ctx.Client.Verdict}";

        MessagesList.Items.Clear();
        foreach (var m in ctx.Messages) MessagesList.Items.Add(m);
    }

    public void SetBusy(bool busy)
    {
        RefreshBtn.IsEnabled = !busy;
        Title = busy ? "VPN Context Helper — refreshing…" : "VPN Context Helper";
    }

    public void ShowError(string message)
    {
        MessagesList.Items.Insert(0, $"ERROR: {message}");
    }

    internal void AllowClose() => _allowClose = true;

    protected override void OnClosing(CancelEventArgs e)
    {
        // Closing the window only hides it; the tray icon keeps the app alive. Use tray > Exit to quit.
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e) => await _app.RefreshAsync();

    private void OpenForti_Click(object sender, RoutedEventArgs e) => _app.OpenFortiClient();

    private async void Report_Click(object sender, RoutedEventArgs e) => await _app.GenerateReportAsync();

    private async void ItRequest_Click(object sender, RoutedEventArgs e) => await _app.GenerateItRequestAsync();
}
