using System.Xml.Linq;
using Ops.Shared.Config;
using Ops.Shared.Models;

namespace Ops.Agent.Services;

public sealed class FrontendMaintenanceService
{
    private const string OfflineFileName = "app_offline.htm";
    private const string RuleName = "MaintenanceMode";

    public MaintenanceModeDto GetStatus(OpsConfig config)
    {
        var offlinePath = Path.Combine(config.Frontend.AppPath, OfflineFileName);
        if (!File.Exists(offlinePath))
            return new MaintenanceModeDto(false, null);

        var message = ReadMessage(offlinePath);
        return new MaintenanceModeDto(true, message);
    }

    public CommandResult SetMaintenance(OpsConfig config, MaintenanceModeRequest request)
    {
        try
        {
            var offlinePath = Path.Combine(config.Frontend.AppPath, OfflineFileName);
            var webConfigPath = Path.Combine(config.Frontend.AppPath, "web.config");
            EnsureMaintenanceRule(webConfigPath);

            if (request.Enabled)
            {
                WriteMaintenancePage(offlinePath, request.Message);
            }
            else if (File.Exists(offlinePath))
            {
                File.Delete(offlinePath);
            }

            return new CommandResult(0, "Maintenance updated", string.Empty);
        }
        catch (Exception ex)
        {
            return new CommandResult(1, string.Empty, ex.Message);
        }
    }

    private static string? ReadMessage(string offlinePath)
    {
        try
        {
            var lines = File.ReadAllLines(offlinePath);
            var marker = lines.FirstOrDefault(l => l.Contains("data-message=\""));
            if (string.IsNullOrWhiteSpace(marker))
                return null;

            var start = marker.IndexOf("data-message=\"", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return null;

            start += "data-message=\"".Length;
            var end = marker.IndexOf('"', start);
            return end > start ? marker[start..end] : null;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteMaintenancePage(string offlinePath, string? message)
    {
        var safeMessage = string.IsNullOrWhiteSpace(message)
            ? "Hệ thống đang bảo trì. Vui lòng quay lại sau."
            : message.Trim();

        var html = $$"""
        <!DOCTYPE html>
        <html lang="vi">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>Đang bảo trì</title>
          <style>
            body { font-family: 'Segoe UI', sans-serif; background:#f5f7fb; color:#1f2a44; display:flex; align-items:center; justify-content:center; height:100vh; margin:0; }
            .card { background:white; padding:32px 40px; border-radius:16px; box-shadow:0 12px 30px rgba(31,42,68,0.12); max-width:520px; }
            h1 { font-size:24px; margin:0 0 12px; }
            p { margin:0; line-height:1.5; }
          </style>
        </head>
        <body>
          <div class="card" data-message="{{System.Net.WebUtility.HtmlEncode(safeMessage)}}">
            <h1>Hệ thống đang bảo trì</h1>
            <p>{{System.Net.WebUtility.HtmlEncode(safeMessage)}}</p>
          </div>
        </body>
        </html>
        """;

        File.WriteAllText(offlinePath, html);
    }

    private static void EnsureMaintenanceRule(string webConfigPath)
    {
        if (!File.Exists(webConfigPath))
            return;

        var doc = XDocument.Load(webConfigPath);
        var systemWebServer = EnsureElement(doc.Root, "system.webServer");
        var rewrite = EnsureElement(systemWebServer, "rewrite");
        var rules = EnsureElement(rewrite, "rules");

        var existing = rules.Elements("rule").FirstOrDefault(e => (string?)e.Attribute("name") == RuleName);
        if (existing is not null)
            return;

        var rule = new XElement("rule",
            new XAttribute("name", RuleName),
            new XAttribute("stopProcessing", "true"),
            new XElement("match", new XAttribute("url", ".*")),
            new XElement("conditions",
                new XAttribute("logicalGrouping", "MatchAll"),
                new XElement("add", new XAttribute("input", "{APPL_PHYSICAL_PATH}app_offline.htm"), new XAttribute("matchType", "IsFile"))),
            new XElement("action", new XAttribute("type", "Rewrite"), new XAttribute("url", "/app_offline.htm")));

        rules.AddFirst(rule);
        doc.Save(webConfigPath);
    }

    private static XElement EnsureElement(XElement parent, string name)
    {
        var element = parent.Element(name);
        if (element is not null)
            return element;

        element = new XElement(name);
        parent.Add(element);
        return element;
    }
}
