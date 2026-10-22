using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenNote.Core;

namespace OpenNote.Views;

/// <summary>The "Check with AI" popup: loading, two suggestions to pick from, or an error.</summary>
internal partial class AiPanel : UserControl
{
    public AiPanel()
    {
        InitializeComponent();
        CloseButton.Click += (_, _) => CloseRequested?.Invoke();
        RetryButton.Click += (_, _) => RetryRequested?.Invoke();
        UseMinimalButton.Click += (_, _) => Chosen?.Invoke(MinimalText.Text);
        UseNaturalButton.Click += (_, _) => Chosen?.Invoke(NaturalText.Text);
    }

    public event Action<string>? Chosen;
    public event Action? RetryRequested;
    public event Action? CloseRequested;

    public void ShowLoading() => Show(LoadingView);

    /// <param name="canReplace">False for a locked note: the buttons copy instead of replacing.</param>
    public void ShowResult(Correction c, bool canReplace)
    {
        // Vietnamese-only text is translated, not checked: the cards are two English versions.
        MinimalLabel.Text = c.Translated ? "English, close to your Vietnamese" : "Closest to your wording";
        NaturalLabel.Text = c.Translated ? "English, as a native would say it" : "More natural";
        ReviewTitle.Text = c.Translated ? "Translation note" : "Your sentence means";
        MinimalText.Text = c.Minimal;
        NaturalText.Text = c.Natural;
        NaturalCard.Visibility = c.Natural == c.Minimal ? Visibility.Collapsed : Visibility.Visible;
        ShowReview(c);
        NaturalMeaningText.Text = c.NaturalMeaning.Length > 0 ? "Nghĩa: " + c.NaturalMeaning : "";
        NaturalMeaningText.Visibility = c.NaturalMeaning.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        // The summary line only adds something when there is no fuller comment.
        ChangesText.Text = c.Changes;
        ChangesText.Visibility = c.Changes.Length > 0 && c.Comment.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        UseMinimalButton.Content = UseNaturalButton.Content = canReplace ? "Use this" : "Copy";
        Show(ResultView);
        UseMinimalButton.Focus();
    }

    static readonly (string Label, string Back, string Fore)[] VerdictStyles =
    [
        ("", "#00000000", "#000000"),            // unknown: no badge
        ("Đúng và tự nhiên", "#DDF3E1", "#1E6B34"),
        ("Chưa tự nhiên", "#FFF0C2", "#7A5B00"),
        ("Có lỗi", "#FFE0D0", "#9A3412"),
        ("Sai nghĩa", "#FBD5D5", "#9B1C1C"),
    ];

    /// <summary>The verdict badge, what the user's sentence means, and the AI's comment.</summary>
    void ShowReview(Correction c)
    {
        var (label, back, fore) = VerdictStyles[(int)c.Verdict];
        VerdictBadge.Visibility = label.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        VerdictText.Text = label;
        VerdictBadge.Background = (Brush)new BrushConverter().ConvertFromString(back)!;
        VerdictText.Foreground = (Brush)new BrushConverter().ConvertFromString(fore)!;

        YourMeaningText.Text = c.YourMeaning;
        YourMeaningText.Visibility = c.YourMeaning.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        CommentText.Text = c.Comment;
        CommentText.Visibility = c.Comment.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        ReviewCard.Visibility = label.Length > 0 || c.YourMeaning.Length > 0 || c.Comment.Length > 0
            ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ShowError(string message)
    {
        ErrorText.Text = message;
        Show(ErrorView);
    }

    void Show(FrameworkElement view)
    {
        foreach (var v in new FrameworkElement[] { LoadingView, ResultView, ErrorView })
            v.Visibility = v == view ? Visibility.Visible : Visibility.Collapsed;
    }
}
