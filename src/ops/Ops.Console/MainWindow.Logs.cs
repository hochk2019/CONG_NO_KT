using System.Windows;

namespace Ops.Console;

public partial class MainWindow
{
    private async void OnLoadLogs(object? sender, RoutedEventArgs e)
    {
        try
        {
            var path = TxtLogPath.Text.Trim();
            var lineText = TxtLogLines.Text.Trim();
            int? lines = int.TryParse(lineText, out var parsed) ? parsed : null;
            var result = await _client.GetLogTailAsync(path, lines, CancellationToken.None);
            TxtLogs.Text = result?.Content ?? string.Empty;
        }
        catch (Exception ex)
        {
            TxtLogs.Text = ex.Message;
        }
    }
}
