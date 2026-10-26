using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenNote.Core;

namespace OpenNote.Views;

/// <summary>
/// The first thing a new user sees: which language Open Note should speak. Each language is
/// written in itself; the one guessed from Windows is highlighted. Closing it keeps that guess.
/// </summary>
internal sealed class LanguagePickWindow : Window
{
    public LanguagePickWindow(Language suggested)
    {
        Chosen = suggested;
        Title = "Open Note";
        Width = 360;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 14;

        var panel = new StackPanel { Margin = new Thickness(20, 16, 20, 20) };
        panel.Children.Add(new TextBlock
        {
            Text = "Language · Ngôn ngữ · 语言 · Idioma · 言語 · 언어 · Langue",
            FontSize = 13, Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12),
        });
        foreach (var language in Core.Language.All)
        {
            bool suggestedOne = language == suggested;
            var button = new Button
            {
                Content = language.NativeName,
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                FontWeight = suggestedOne ? FontWeights.SemiBold : FontWeights.Normal,
                Background = suggestedOne ? new SolidColorBrush(Color.FromRgb(0xFF, 0xE6, 0x80)) : Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xD8)),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            button.Click += (_, _) =>
            {
                Chosen = language;
                Close();
            };
            panel.Children.Add(button);
            if (suggestedOne)
                Loaded += (_, _) => button.Focus(); // Enter keeps the suggestion
        }
        Content = panel;
    }

    /// <summary>The language picked (the suggestion if the window was just closed).</summary>
    public Language Chosen { get; private set; }
}
