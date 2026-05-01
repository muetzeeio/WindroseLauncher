using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WindroseLauncher;

public partial class SettingsWindow : Window
{
    private const string CurrentVersion = "1.0.0";
    private const string RepoApiUrl = "https://api.github.com/repos/muetzeeio/WindroseLauncher/releases/latest";

    private string? latestVersion;
    private string? downloadUrl;

    public SettingsWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Aktuelle Version: v{CurrentVersion}";
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private async void CheckButton_Click(object sender, RoutedEventArgs e)
    {
        await CheckForUpdate();
    }

    private async Task CheckForUpdate()
    {
        try
        {
            SetCheckingState();

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WindroseLauncher");

            var json = await client.GetStringAsync(RepoApiUrl);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            latestVersion = root.GetProperty("tag_name").GetString() ?? "unknown";
            latestVersion = latestVersion.Replace("v", "");

            downloadUrl = FindExeDownloadUrl(root);

            if (latestVersion != CurrentVersion)
            {
                StatusText.Text = $"Update verfügbar: v{latestVersion}";
                StatusText.Foreground = Brushes.Orange;
                VersionText.Text = $"Installiert: v{CurrentVersion}  |  Neu: v{latestVersion}";
                CheckButton.Background = new SolidColorBrush(Color.FromRgb(220, 95, 8));
                DownloadButton.Visibility = Visibility.Visible;
            }
            else
            {
                StatusText.Text = "Launcher ist aktuell";
                StatusText.Foreground = Brushes.LimeGreen;
                VersionText.Text = $"Neueste Version: v{latestVersion}";
                CheckButton.Background = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                DownloadButton.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Update-Check fehlgeschlagen";
            StatusText.Foreground = Brushes.Red;
            VersionText.Text = ex.Message;
            DownloadButton.Visibility = Visibility.Collapsed;
        }
    }

    private static string? FindExeDownloadUrl(JsonElement releaseRoot)
    {
        if (!releaseRoot.TryGetProperty("assets", out var assets))
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            string name = asset.GetProperty("name").GetString() ?? "";
            string url = asset.GetProperty("browser_download_url").GetString() ?? "";

            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return url;
        }

        return null;
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            MessageBox.Show("Keine Update-EXE im GitHub Release gefunden.", "Update Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            StatusText.Text = "Update wird heruntergeladen...";
            StatusText.Foreground = Brushes.Orange;
            DownloadButton.IsEnabled = false;
            CheckButton.IsEnabled = false;

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WindroseLauncher");

            byte[] data = await client.GetByteArrayAsync(downloadUrl);

            string updatePath = Path.Combine(AppContext.BaseDirectory, "WindroseLauncher_Update.exe");
            await File.WriteAllBytesAsync(updatePath, data);

            StatusText.Text = "Update geladen";
            StatusText.Foreground = Brushes.LimeGreen;
            VersionText.Text = "Starte neue Version...";

            Process.Start(new ProcessStartInfo
            {
                FileName = updatePath,
                UseShellExecute = true
            });

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Update-Download fehlgeschlagen";
            StatusText.Foreground = Brushes.Red;
            VersionText.Text = ex.Message;
            DownloadButton.IsEnabled = true;
            CheckButton.IsEnabled = true;
        }
    }

    private void SetCheckingState()
    {
        StatusText.Text = "Prüfe GitHub...";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(97, 166, 255));
        VersionText.Text = $"Aktuelle Version: v{CurrentVersion}";
        DownloadButton.Visibility = Visibility.Collapsed;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
