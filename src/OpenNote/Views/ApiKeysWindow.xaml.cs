using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using OpenNote.Core;

namespace OpenNote.Views;

/// <summary>
/// Where users add their own Gemini API keys: how to get one, a tip for more free quota, and a
/// live check of every key before it is saved.
/// </summary>
internal partial class ApiKeysWindow : Window
{
    const string StudioUrl = "https://aistudio.google.com/apikey";

    // One client for every check (created on the first one), so repeated checks don't pile up connections.
    static readonly Lazy<HttpClient> Client = new(() => new HttpClient { Timeout = TimeSpan.FromSeconds(15) });

    readonly Func<IReadOnlyList<string>, string?> _save;
    CancellationTokenSource? _checking;

    /// <param name="currentKeys">Keys already saved, shown so they can be edited.</param>
    /// <param name="save">Saves the keys; returns an error message, or null when saved.</param>
    public ApiKeysWindow(IEnumerable<string> currentKeys, Func<IReadOnlyList<string>, string?> save)
    {
        InitializeComponent();
        _save = save;
        KeysBox.Text = string.Join(Environment.NewLine, currentKeys);
        Loaded += (_, _) => KeysBox.Focus();
        Closed += (_, _) => _checking?.Cancel();
    }

    void OpenStudio_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(StudioUrl) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Clipboard.SetText(StudioUrl);
            ShowSummary("No browser could be opened. The address was copied: paste it into your browser.", warn: true);
        }
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    async void Save_Click(object sender, RoutedEventArgs e)
    {
        var keys = GeminiKeyChecker.ParseKeys(KeysBox.Text);
        Results.ItemsSource = null;
        if (keys.Count == 0)
        {
            // An empty box removes all keys: AI checking is simply turned off.
            var error = _save([]);
            ShowSummary(error ?? "No keys saved. Check with AI stays off until you add one.", warn: error is not null);
            return;
        }

        _checking?.Cancel();
        var cancel = _checking = new CancellationTokenSource();
        SaveButton.IsEnabled = false;
        CheckingText.Text = keys.Count == 1 ? "Checking your key with Google…" : $"Checking {keys.Count} keys with Google…";
        CheckingPanel.Visibility = Visibility.Visible;
        Summary.Visibility = Visibility.Collapsed;

        KeyCheckResult[] results;
        try
        {
            results = await GeminiKeyChecker.CheckAllAsync(keys, Client.Value, cancel.Token);
        }
        catch (OperationCanceledException)
        {
            return; // window closed while checking
        }
        finally
        {
            if (!cancel.IsCancellationRequested)
            {
                CheckingPanel.Visibility = Visibility.Collapsed;
                SaveButton.IsEnabled = true;
            }
        }

        Results.ItemsSource = results.Select(r => new ResultRow(r)).ToList();
        var keep = results.Where(r => r.Keep).Select(r => r.Key).ToList();
        int working = results.Count(r => r.Status == KeyStatus.Works);
        if (keep.Count == 0)
        {
            ShowSummary("None of these keys can be used, so nothing was saved. Check that you copied the whole key.", warn: true);
            return;
        }

        var saveError = _save(keep);
        if (saveError is not null)
        {
            ShowSummary(saveError, warn: true);
            return;
        }
        KeysBox.Text = string.Join(Environment.NewLine, keep);
        ShowSummary(working == keep.Count
            ? $"Saved {keep.Count} working key{(keep.Count == 1 ? "" : "s")}. Check with AI (Ctrl+G) is ready."
            : $"Saved {keep.Count} key{(keep.Count == 1 ? "" : "s")} ({working} working right now). Keys that were rejected were left out.",
            warn: working == 0);
    }

    void ShowSummary(string text, bool warn)
    {
        Summary.Text = text;
        Summary.Foreground = warn ? Brushes.Firebrick : new SolidColorBrush(Color.FromRgb(0x1E, 0x6B, 0x34));
        Summary.Visibility = Visibility.Visible;
    }

    /// <summary>One row of the check results.</summary>
    sealed class ResultRow(KeyCheckResult result)
    {
        public string Masked { get; } = GeminiKeyChecker.Mask(result.Key);
        public string Message { get; } = result.Message;
        public string Icon { get; } = result.Status switch
        {
            KeyStatus.Works => "✔",
            KeyStatus.Invalid or KeyStatus.NoAccess => "✖",
            _ => "⚠",
        };
        public Brush Color { get; } = result.Status switch
        {
            KeyStatus.Works => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x6B, 0x34)),
            KeyStatus.Invalid or KeyStatus.NoAccess => Brushes.Firebrick,
            _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8A, 0x6A, 0x00)),
        };
    }
}
