using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenNote.Core;

namespace OpenNote.Views;

/// <summary>The "Check with AI" popup: loading, two suggestions to pick from, or an error.</summary>
internal partial class AiPanel : UserControl
{
    readonly AiPanelText _t;

    /// <param name="native">The user's own language: the popup is written in it, like the AI's comments.</param>
    public AiPanel(Language native)
    {
        InitializeComponent();
        _t = AiPanelText.For(native);
        TitleText.Text = _t.Title;
        AskingText.Text = _t.Asking;
        CloseButton.ToolTip = _t.Close;
        RetryButton.Content = _t.TryAgain;
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
        // Text in your own language is translated, not checked: the cards are two translations.
        MinimalLabel.Text = c.Translated ? _t.TranslatedClose : _t.Closest;
        NaturalLabel.Text = c.Translated ? _t.TranslatedNatural : _t.MoreNatural;
        ReviewTitle.Text = c.Translated ? _t.TranslationNote : _t.YourMeaning;
        MinimalText.Text = c.Minimal;
        NaturalText.Text = c.Natural;
        NaturalCard.Visibility = c.Natural == c.Minimal ? Visibility.Collapsed : Visibility.Visible;
        ShowReview(c);
        NaturalMeaningText.Text = c.NaturalMeaning.Length > 0 ? _t.MeaningPrefix + c.NaturalMeaning : "";
        NaturalMeaningText.Visibility = c.NaturalMeaning.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        // The summary line only adds something when there is no fuller comment.
        ChangesText.Text = c.Changes;
        ChangesText.Visibility = c.Changes.Length > 0 && c.Comment.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        UseMinimalButton.Content = UseNaturalButton.Content = canReplace ? _t.UseThis : _t.Copy;
        Show(ResultView);
        UseMinimalButton.Focus();
    }

    static readonly (string Back, string Fore)[] VerdictColors =
    [
        ("#00000000", "#000000"), // unknown: no badge
        ("#DDF3E1", "#1E6B34"),   // good
        ("#FFF0C2", "#7A5B00"),   // unnatural
        ("#FFE0D0", "#9A3412"),   // errors
        ("#FBD5D5", "#9B1C1C"),   // wrong meaning
    ];

    /// <summary>The verdict badge, what the user's sentence means, and the AI's comment.</summary>
    void ShowReview(Correction c)
    {
        var (back, fore) = VerdictColors[(int)c.Verdict];
        var label = c.Verdict switch
        {
            Verdict.Good => _t.Good,
            Verdict.Unnatural => _t.Unnatural,
            Verdict.Errors => _t.Errors,
            Verdict.WrongMeaning => _t.WrongMeaning,
            _ => "",
        };
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
