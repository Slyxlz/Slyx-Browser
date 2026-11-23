# SLYX Browser

A small Windows desktop browser shell using Microsoft Edge WebView2 (Chromium). It uses the installed WebView2 Runtime instead of bundling a separate Chromium copy.

## Requirements
- Windows 10 or Windows 11
- .NET 8 SDK
- Microsoft Edge WebView2 Runtime (Evergreen)

## Build
1. Install the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
2. Install the Microsoft Edge WebView2 Runtime: https://developer.microsoft.com/microsoft-edge/webview2/
3. Open PowerShell in this folder.
4. Run:

   ```powershell
   dotnet restore
   dotnet build -c Release
   dotnet run -c Release
   ```

The executable is created under `bin\\Release\\net8.0-windows\\`.

## Tabs
- Press `+` to create additional tabs. Each tab can navigate to YouTube.
- Tabs are separate WebView2 controls. Five tabs are supported, but five active YouTube pages can use substantially more than 200 MB RAM.
- WebView2 shares the installed Edge runtime and process infrastructure. Actual memory usage depends on the page, codecs, extensions/plugins, and system.

## Plugins
Put `.js` and `.css` files in the `Plugins` folder beside the executable. They are injected into pages at document creation. Restart SLYX Browser after changing plugin files.

Example `Plugins/example.js`:

```javascript
// Example plugin: adds a small label to pages.
(() => {
  const add = () => {
    if (!document.body) return requestAnimationFrame(add);
    if (document.getElementById("slyx-plugin-label")) return;
    const label = document.createElement("div");
    label.id = "slyx-plugin-label";
    label.textContent = "SLYX plugin active";
    label.style.cssText = "position:fixed;bottom:8px;right:8px;z-index:2147483647;background:#222;color:#fff;padding:6px 9px;font:12px sans-serif;border-radius:5px";
    document.body.appendChild(label);
  };
  add();
})();
```

This is a lightweight custom JavaScript/CSS plugin system, **not Chrome Web Store extension compatibility**.

## Privacy and blocking
- No SLYX analytics, telemetry, account, or usage reporting is implemented.
- A small built-in host blocklist blocks common advertising and analytics domains.
- This is not a complete ad blocker or tracker blocker. Blocking can miss new domains and may break site features. YouTube ads and first-party tracking cannot be reliably eliminated by this basic host list.
- The app does not send browsing history to SLYX servers. Web requests go directly to the websites you visit and their providers.

## Notes
This is source code, not a signed installer. Review the source before building. Keep WebView2 and Windows updated.
