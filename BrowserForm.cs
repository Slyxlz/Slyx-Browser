using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SLYXBrowser;

public sealed class BrowserForm : Form
{
    private readonly Panel toolbar = new() { Dock = DockStyle.Top, Height = 42, BackColor = Color.FromArgb(25, 27, 31) };
    private readonly FlowLayoutPanel tabsBar = new() { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(18, 20, 23), WrapContents = false, AutoScroll = true };
    private readonly Panel browserHost = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 17, 20) };
    private readonly TextBox address = new() { BorderStyle = BorderStyle.FixedSingle, Width = 560, Font = new Font("Segoe UI", 10), BackColor = Color.FromArgb(39, 42, 48), ForeColor = Color.White };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.Silver, BackColor = Color.FromArgb(20, 22, 25), Text = "SLYX Browser • Privacy protection enabled" };
    private readonly List<BrowserTab> tabs = new();
    private readonly HashSet<string> blockedHosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly string pluginFolder = Path.Combine(AppContext.BaseDirectory, "Plugins");
    private CoreWebView2Environment? environment;
    private BrowserTab? activeTab;
    private int tabNumber;

    private static readonly string[] BuiltInBlockedHosts =
    {
        "doubleclick.net", "googlesyndication.com", "googleadservices.com",
        "adservice.google.com", "adnxs.com", " taboola.com", "outbrain.com",
        "scorecardresearch.com", "quantserve.com", "amazon-adsystem.com",
        "facebook.net", "connect.facebook.net", "hotjar.com", "mixpanel.com",
        "segment.io", "segment.com", "amplitude.com", "fullstory.com",
        "clarity.ms", "browser-intake-datadoghq.com"
    };

    public BrowserForm()
    {
        Text = "SLYX Browser";
        Width = 1280;
        Height = 820;
        MinimumSize = new Size(760, 500);
        BackColor = Color.FromArgb(15, 17, 20);
        StartPosition = FormStartPosition.CenterScreen;

        foreach (var host in BuiltInBlockedHosts)
            blockedHosts.Add(host.Trim());

        BuildToolbar();
        Controls.Add(browserHost);
        Controls.Add(status);
        Controls.Add(toolbar);
        Controls.Add(tabsBar);

        Shown += async (_, _) =>
        {
            Directory.CreateDirectory(pluginFolder);
            try
            {
                environment = await CoreWebView2Environment.CreateAsync(null,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SLYXBrowser", "WebView2"));
                AddTab("https://www.youtube.com");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not initialize Microsoft Edge WebView2.\nInstall the Microsoft Edge WebView2 Runtime, then restart SLYX.\n\n" + ex.Message,
                    "SLYX Browser", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        FormClosing += (_, _) =>
        {
            foreach (var tab in tabs) tab.View?.Dispose();
        };
    }

    private void BuildToolbar()
    {
        var back = MakeButton("←", (_, _) => activeTab?.View?.GoBack());
        var forward = MakeButton("→", (_, _) => activeTab?.View?.GoForward());
        var reload = MakeButton("↻", (_, _) => activeTab?.View?.Reload());
        var go = MakeButton("Go", async (_, _) => await NavigateAddress());
        var newTab = MakeButton("+", (_, _) => AddTab("https://www.youtube.com"));
        var plugin = MakeButton("Plugins", (_, _) => OpenPluginsFolder());
        var home = MakeButton("⌂", (_, _) => Navigate("https://www.youtube.com"));

        toolbar.Controls.AddRange(new Control[] { back, forward, reload, home, address, go, newTab, plugin });
        int x = 8;
        foreach (Control c in toolbar.Controls)
        {
            c.Left = x; c.Top = 7; c.Height = 28;
            x += c.Width + 6;
        }
        address.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        toolbar.Resize += (_, _) =>
        {
            address.Width = Math.Max(180, toolbar.Width - 440);
            go.Left = address.Right + 6;
            newTab.Left = go.Right + 6;
            plugin.Left = newTab.Right + 6;
        };
        address.Width = Math.Max(180, toolbar.Width - 440);
        address.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await NavigateAddress(); }
        };
    }

    private Button MakeButton(string text, EventHandler click)
    {
        var b = new Button
        {
            Text = text, Width = text == "Plugins" ? 72 : 34,
            FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
            BackColor = Color.FromArgb(42, 46, 53), Font = new Font("Segoe UI", 9),
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += click;
        return b;
    }

    private void AddTab(string url)
    {
        if (environment is null) return;
        var tab = new BrowserTab(++tabNumber);
        tabs.Add(tab);
        var button = MakeButton($"Tab {tab.Number}  ×", (_, _) => SelectTab(tab));
        button.Width = 112;
        button.Margin = new Padding(4, 4, 2, 2);
        tab.Button = button;
        tabsBar.Controls.Add(button);
        _ = InitializeTab(tab, url);
        SelectTab(tab);
    }

    private async Task InitializeTab(BrowserTab tab, string url)
    {
        try
        {
            var view = new WebView2 { Dock = DockStyle.Fill, Visible = false };
            tab.View = view;
            browserHost.Controls.Add(view);
            await view.EnsureCoreWebView2Async(environment);
            var core = view.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = true;
            core.Settings.AreBrowserAcceleratorKeysEnabled = true;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsSwipeNavigationEnabled = false;
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, e) => FilterRequest(core, e);
            core.NavigationStarting += (_, e) =>
            {
                if (IsBlockedUri(e.Uri)) { e.Cancel = true; status.Text = "Blocked a known advertising/tracking destination"; }
                else status.Text = "Loading • " + e.Uri;
            };
            core.NavigationCompleted += (_, _) =>
            {
                if (tab == activeTab && core.Source != null)
                {
                    address.Text = core.Source;
                    status.Text = "Protected browsing • " + core.DocumentTitle;
                }
            };
            await LoadPlugins(core);
            core.Navigate(NormalizeUrl(url));
        }
        catch (Exception ex)
        {
            status.Text = "Tab initialization failed: " + ex.Message;
        }
    }

    private void FilterRequest(CoreWebView2 core, CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            var uri = new Uri(e.Request.Uri);
            if (IsBlockedHost(uri.Host))
            {
                e.Response = core.Environment.CreateWebResourceResponse(null, 403, "Blocked by SLYX", "Content-Type: text/plain");
            }
        }
        catch { }
    }

    private bool IsBlockedUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        return IsBlockedHost(uri.Host);
    }

    private bool IsBlockedHost(string host)
    {
        host = host.ToLowerInvariant();
        return blockedHosts.Any(domain => host == domain || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));
    }

    private async Task LoadPlugins(CoreWebView2 core)
    {
        foreach (var file in Directory.EnumerateFiles(pluginFolder, "*.js").OrderBy(x => x))
        {
            try
            {
                string script = await File.ReadAllTextAsync(file);
                await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
            }
            catch (Exception ex) { status.Text = "Plugin error (" + Path.GetFileName(file) + "): " + ex.Message; }
        }
        foreach (var file in Directory.EnumerateFiles(pluginFolder, "*.css").OrderBy(x => x))
        {
            string css = await File.ReadAllTextAsync(file);
            string encoded = JsonSerializer.Serialize(css);
            await core.AddScriptToExecuteOnDocumentCreatedAsync(
                $"(()=>{{const css={encoded};const add=()=>{{if(document.head){{let s=document.createElement('style');s.textContent=css;document.head.appendChild(s)}}else requestAnimationFrame(add)}};add()}})();");
        }
    }

    private async Task NavigateAddress()
    {
        if (activeTab is null) return;
        Navigate(address.Text);
        await Task.CompletedTask;
    }

    private void Navigate(string value)
    {
        if (activeTab?.View?.CoreWebView2 is null) return;
        activeTab.View.CoreWebView2.Navigate(NormalizeUrl(value));
    }

    private static string NormalizeUrl(string input)
    {
        input = input.Trim();
        if (string.IsNullOrWhiteSpace(input)) return "https://www.youtube.com";
        if (Regex.IsMatch(input, @"^[a-zA-Z][a-zA-Z0-9+.-]*://")) return input;
        if (input.Contains(' ') || !input.Contains('.'))
            return "https://www.google.com/search?q=" + Uri.EscapeDataString(input);
        return "https://" + input;
    }

    private void SelectTab(BrowserTab tab)
    {
        if (!tabs.Contains(tab)) return;
        activeTab = tab;
        foreach (var item in tabs)
        {
            if (item.View != null) item.View.Visible = item == tab;
            if (item.Button != null)
                item.Button.BackColor = item == tab ? Color.FromArgb(65, 88, 115) : Color.FromArgb(42, 46, 53);
        }
        if (tab.View?.CoreWebView2 != null) address.Text = tab.View.CoreWebView2.Source;
        else address.Text = "Loading tab…";
    }

    private void OpenPluginsFolder()
    {
        Directory.CreateDirectory(pluginFolder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe", Arguments = $"\"{pluginFolder}\"", UseShellExecute = true
        });
    }

    private sealed class BrowserTab(int number)
    {
        public int Number { get; } = number;
        public WebView2? View { get; set; }
        public Button? Button { get; set; }
    }
}
