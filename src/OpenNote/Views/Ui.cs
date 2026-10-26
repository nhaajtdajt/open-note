using System.Windows.Markup;
using OpenNote.Core;

namespace OpenNote.Views;

/// <summary>XAML text in the display language: <c>Text="{v:T 'Your language'}"</c>, read when the window is built.</summary>
[MarkupExtensionReturnType(typeof(string))]
internal sealed class TExtension(string text) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => Ui.T(text);
}
