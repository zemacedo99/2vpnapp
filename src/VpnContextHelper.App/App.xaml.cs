using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using VpnContextHelper.Core;
using VpnContextHelper.Core.Config;
using VpnContextHelper.Core.Context;
using VpnContextHelper.Core.Diagnostics;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace VpnContextHelper.App;

public partial class App : Application
{
    private WinForms.NotifyIcon _tray = null!;
    private MainWindow _window = null!;
    private DispatcherTimer _timer = null!;
    private AppConfig _config = null!;
    private ContextService _service = null!;
    private Redactor _redactor = null!;
    private bool _busy;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var (cfg, source) = ConfigLocator.Find();
        _config = cfg;
        _service = new ContextService(_config);
        _redactor = new Redactor(
            _config.Redaction,
            _config.Redaction.MaskUsernames ? new[] { Environment.UserName } : null);

        _window = new MainWindow(this) { ConfigSource = source };

        _tray = new WinForms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Shield,
            Visible = true,
            Text = "VPN Context Helper",
        };
        _tray.DoubleClick += (_, _) => ShowWindow();

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Show status", null, (_, _) => ShowWindow());
        menu.Items.Add("Refresh now", null, async (_, _) => await RefreshAsync());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Open FortiClient", null, (_, _) => OpenFortiClient());
        menu.Items.Add("Generate diagnostic report…", null, async (_, _) => await GenerateReportAsync());
        menu.Items.Add("Generate IT request…", null, async (_, _) => await GenerateItRequestAsync());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(10, _config.PollIntervalSeconds)),
        };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();

        ShowWindow();
        _ = RefreshAsync();
    }

    internal async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        _window.SetBusy(true);
        try
        {
            var result = await Task.Run(() => _service.EvaluateAsync(includeRawCaptures: false));
            _window.Update(result.Context);
            _tray.Text = Truncate($"VPN Helper — {Summary(result.Context)}", 63);
        }
        catch (Exception ex)
        {
            _window.ShowError(ex.Message);
        }
        finally
        {
            _window.SetBusy(false);
            _busy = false;
        }
    }

    internal void OpenFortiClient()
    {
        try
        {
            if (File.Exists(_config.FortiClientPath))
            {
                Process.Start(new ProcessStartInfo(_config.FortiClientPath) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show(
                    $"FortiClient was not found at:\n{_config.FortiClientPath}\n\nUpdate 'fortiClientPath' in config.json.",
                    "FortiClient not found", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open FortiClient: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        // NOTE: this only launches the FortiClient UI. It never enters credentials, MFA or SAML.
    }

    internal async Task GenerateReportAsync()
    {
        try
        {
            var result = await Task.Run(() => _service.EvaluateAsync(includeRawCaptures: true));
            var md = DiagnosticReportBuilder.Build(_config, result.Data, result.Context, _redactor);
            SaveAndOpen(md, "vpn-diagnostic-report.md", "Diagnostic report");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to generate report: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    internal async Task GenerateItRequestAsync()
    {
        try
        {
            var result = await Task.Run(() => _service.EvaluateAsync(includeRawCaptures: false));
            var md = ItRequestBuilder.Build(_config, result.Data, result.Context, _redactor);
            SaveAndOpen(md, "it-request.md", "IT request");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to generate IT request: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAndOpen(string content, string defaultName, string what)
    {
        using var dlg = new WinForms.SaveFileDialog
        {
            FileName = defaultName,
            Filter = "Markdown (*.md)|*.md|All files (*.*)|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Title = $"Save {what}",
        };
        if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;

        File.WriteAllText(dlg.FileName, content);
        var open = MessageBox.Show(
            $"{what} saved to:\n{dlg.FileName}\n\nSecrets are redacted. Open it now?",
            "Saved", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (open == MessageBoxResult.Yes)
            Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
    }

    private void ShowWindow()
    {
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void ExitApp()
    {
        _timer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        _window.AllowClose();
        Shutdown();
    }

    private static string Summary(NetworkContext ctx)
    {
        var vpn = ctx.Vpn.Connected ? (ctx.Vpn.IsFullTunnel ? "full VPN" : "split VPN") : "no VPN";
        return $"{vpn}; {ctx.Company.Label}={ctx.Company.Verdict}; {ctx.Client.Label}={ctx.Client.Verdict}";
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
