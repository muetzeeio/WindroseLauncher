using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Net.Http;
using System.Text.Json;
using System.IO.Compression;

namespace WindroseLauncher;

public partial class MainWindow : Window
{
    const string currentVersion = "1.0.0";

    private async Task CheckForUpdate()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WindroseLauncher");

            var json = await client.GetStringAsync("https://api.github.com/repos/muetzeeio/WindroseLauncher/releases/latest");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string latestVersion = root.GetProperty("tag_name").GetString() ?? "unknown";
            latestVersion = latestVersion.Replace("v", "");

            Log($"Neueste Version: v{latestVersion}");

            if (latestVersion != currentVersion)
            {
                Log($"Neue Version verfügbar: v{latestVersion}");

                MessageBox.Show(
                    $"Neue Version verfügbar: v{latestVersion}",
                    "Update verfügbar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            else
            {
                Log("Launcher ist aktuell");
            }
        }
        catch (Exception ex)
        {
            Log("Update Fehler: " + ex.Message);
        }
    }

    private async Task DownloadMods()
    {
        try
        {
            string url = "https://github.com/muetzeeio/WindroseLauncher/releases/latest/download/mods.zip";
            string zipPath = Path.Combine(baseDir, "mods.zip");
            string extractPath = Path.Combine(baseDir, "mods");

            Log("Lade Mods herunter...");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WindroseLauncher");

            var data = await client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(zipPath, data);

            Log("Entpacke Mods...");

            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);

            ZipFile.ExtractToDirectory(zipPath, extractPath);

            File.Delete(zipPath);

            Log("Mods erfolgreich heruntergeladen");
        }
        catch (Exception ex)
        {
            Log("Mod Download Fehler: " + ex.Message);
        }
    }

    private const string AppId = "3041230";
    private readonly string baseDir = AppContext.BaseDirectory;
    private readonly string configFile;
    private readonly string modsDir;
    private readonly DispatcherTimer statusTimer;

    public MainWindow()
    {
        InitializeComponent();
        configFile = Path.Combine(baseDir, "config.txt");
        modsDir = Path.Combine(baseDir, "mods");

        LoadConfig();
        UpdateStatus();
        Log("Launcher gestartet");

        statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        statusTimer.Tick += (_, _) => UpdateGameStatus();
        statusTimer.Start();
        UpdateGameStatus();

        _ =CheckForUpdate();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        Log("Fenstergröße ist fest eingestellt.");
    }

    private string GameDir
    {
        get
        {
            if (File.Exists(configFile))
                return File.ReadAllText(configFile).Trim();
            return "";
        }
    }

    private void LoadConfig()
    {
        var dir = GameDir;
        PathText.Text = dir;

        if (!string.IsNullOrWhiteSpace(dir))
            Log("Installationspfad geladen");
    }

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Windrose Installationsordner auswählen"
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(configFile, dialog.FolderName);
            PathText.Text = dialog.FolderName;
            Log("Installationspfad gespeichert");
            UpdateStatus();
        }
    }

    private bool ValidateSetup()
    {
        string gameDir = GameDir;

        if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
        {
            MessageBox.Show("Bitte zuerst den Windrose-Installationspfad auswählen.", "Pfad fehlt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!Directory.Exists(Path.Combine(gameDir, "R5")))
        {
            MessageBox.Show("Der gewählte Ordner sieht nicht nach Windrose aus. Im Ordner muss ein R5-Unterordner sein.", "Ungültiger Pfad", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!Directory.Exists(Path.Combine(modsDir, "R5")))
        {
            MessageBox.Show("Der mods-Ordner fehlt. Lege ihn neben die EXE: mods\\R5\\...", "Mods fehlen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private async void InstallMods_Click(object sender, RoutedEventArgs e)
    {
        await InstallModsAsync();
    }
    private async Task InstallModsAsync()
    {
        await DownloadMods();
        if (!ValidateSetup()) return;
        InstallMods();
        Log("Mods installiert");
        UpdateStatus();
    }

    private void RemoveMods_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateSetup()) return;
        RemoveMods();
        Log("Mods entfernt");
        UpdateStatus();
    }

    private void PlayVanilla_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateSetup()) return;
        RemoveMods();
        Log("Starte Vanilla");
        UpdateStatus();
        LaunchSteam();
    }

    private void PlayMods_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateSetup()) return;
        InstallMods();
        Log("Starte mit Mods");
        UpdateStatus();
        LaunchSteam();
    }

    private void InstallMods()
    {
        string gameDir = GameDir;

        foreach (string file in Directory.GetFiles(modsDir, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(modsDir, file);
            if (rel.EndsWith("HIER_DEINE_MODS_REIN.txt", StringComparison.OrdinalIgnoreCase))
                continue;

            string target = Path.Combine(gameDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private void RemoveMods()
    {
        string gameDir = GameDir;

        foreach (string file in Directory.GetFiles(modsDir, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(modsDir, file);
            if (rel.EndsWith("HIER_DEINE_MODS_REIN.txt", StringComparison.OrdinalIgnoreCase))
                continue;

            string target = Path.Combine(gameDir, rel);
            if (File.Exists(target))
                File.Delete(target);
        }

        foreach (string folder in Directory.GetDirectories(modsDir, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
        {
            string rel = Path.GetRelativePath(modsDir, folder);
            string target = Path.Combine(gameDir, rel);

            if (Directory.Exists(target) && !Directory.EnumerateFileSystemEntries(target).Any())
            {
                try { Directory.Delete(target); } catch { }
            }
        }
    }

    private void LaunchSteam()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = $"steam://rungameid/{AppId}",
            UseShellExecute = true
        });

        UpdateGameStatus();
    }

    private bool IsModded()
    {
        string gameDir = GameDir;
        if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
            return false;

        // Main known marker from your current pack
        string marker = Path.Combine(gameDir, "R5", "Content", "Paks", "pakchunk99-KillExpMod_HeroLevels_P.pak");
        if (File.Exists(marker))
            return true;

        // Detect any file that exists from our mods folder inside the game dir
        if (!Directory.Exists(modsDir))
            return false;

        foreach (string file in Directory.GetFiles(modsDir, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(modsDir, file);
            if (rel.EndsWith("HIER_DEINE_MODS_REIN.txt", StringComparison.OrdinalIgnoreCase))
                continue;

            if (File.Exists(Path.Combine(gameDir, rel)))
                return true;
        }

        return false;
    }

    private void UpdateStatus()
    {
        string gameDir = GameDir;
        PathText.Text = gameDir;

        if (string.IsNullOrWhiteSpace(gameDir))
        {
            StatusIconOverlay.Text = "🛡";
            StatusTitleOverlay.Text = "KEIN PFAD";
            StatusSubOverlay.Text = "Bitte Installationspfad wählen";
            StatusTitleOverlay.Foreground = new SolidColorBrush(Color.FromRgb(255, 176, 0));
            StatusIconOverlay.Foreground = new SolidColorBrush(Color.FromRgb(95, 176, 68));
            return;
        }

        if (IsModded())
        {
            StatusIconOverlay.Text = "🧩";
            StatusTitleOverlay.Text = "MODS AKTIV";
            StatusSubOverlay.Text = "Mod-Dateien erkannt";
            StatusTitleOverlay.Foreground = new SolidColorBrush(Color.FromRgb(216, 138, 46));
            StatusIconOverlay.Foreground = new SolidColorBrush(Color.FromRgb(216, 138, 46));
            Log("Mod Status: Mods aktiv");
        }
        else
        {
            StatusIconOverlay.Text = "🛡";
            StatusTitleOverlay.Text = "VANILLA";
            StatusSubOverlay.Text = "Keine Mods aktiv";
            StatusTitleOverlay.Foreground = new SolidColorBrush(Color.FromRgb(98, 184, 68));
            StatusIconOverlay.Foreground = new SolidColorBrush(Color.FromRgb(95, 176, 68));
            Log("Mod Status: Vanilla");
        }
    }


    private static bool IsGameRunning()
    {
        try
        {
            return Process.GetProcesses()
                .Any(p =>
                {
                    try
                    {
                        string name = p.ProcessName;
                        return name.Equals("Windrose-Win64-Shipping", StringComparison.OrdinalIgnoreCase)
                            || name.Equals("Windrose", StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                });
        }
        catch
        {
            return false;
        }
    }

    private void UpdateGameStatus()
    {
        if (GameStatusOverlay == null)
            return;

        if (IsGameRunning())
        {
            GameStatusOverlay.Text = "Spiel läuft bereits!";
            GameStatusOverlay.Foreground = new SolidColorBrush(Color.FromRgb(255, 70, 70));
        }
        else
        {
            GameStatusOverlay.Text = "Bereit zum Start";
            GameStatusOverlay.Foreground = new SolidColorBrush(Color.FromRgb(92, 255, 92));
        }
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Windrose Mod Launcher\nVersion 1.0.0\n\nAppID: 3041230\n\ncreated by Muetzee",
            "Über",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Log(string text)
    {
        if (RealLogBox == null)
            return;

        Brush color = Brushes.LightBlue;
        string lower = text.ToLowerInvariant();

        if (lower.Contains("mods aktiv"))
            color = Brushes.Red;
        else if (lower.Contains("vanilla"))
            color = Brushes.LimeGreen;

        var paragraph = new Paragraph
        {
            Margin = new Thickness(0)
        };

        var run = new Run($"[{DateTime.Now:HH:mm:ss}]  {text}")
        {
            Foreground = color
        };

        paragraph.Inlines.Add(run);
        RealLogBox.Document.Blocks.Add(paragraph);
        RealLogBox.ScrollToEnd();
    }
}
