using System.Text.RegularExpressions;
using OpenNote.Core;

namespace OpenNote.Tests;

public class TranslationTests
{
    /// <summary>The app's source folder, found from the test's own folder.</summary>
    static string SourceDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var src = Path.Combine(dir.FullName, "src", "OpenNote");
            if (Directory.Exists(src))
                return src;
        }
        throw new DirectoryNotFoundException("src/OpenNote");
    }

    /// <summary>Every phrase the app shows through Ui.T, {v:T '…'} or a menu Item("…").</summary>
    static IEnumerable<(string File, string Phrase)> PhrasesInSource()
    {
        var csharp = new Regex(@"(?:Ui\.T|\bItem|CommandItem)\(""((?:[^""\\]|\\.)*)""");
        var xaml = new Regex(@"\{v:T '((?:[^'\\]|\\.)*)'\}");
        foreach (var file in Directory.EnumerateFiles(SourceDir(), "*.*", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;
            var text = File.ReadAllText(file);
            if (file.EndsWith(".cs"))
            {
                foreach (Match m in csharp.Matches(text))
                    yield return (Path.GetFileName(file), Regex.Unescape(m.Groups[1].Value.Replace("\\\"", "\"")));
            }
            else if (file.EndsWith(".xaml"))
            {
                foreach (Match m in xaml.Matches(text))
                    yield return (Path.GetFileName(file), m.Groups[1].Value.Replace("\\'", "'").Replace("&quot;", "\""));
            }
        }
    }

    [Fact]
    public void EveryPhraseShown_HasARow()
    {
        var known = Translations.English.ToHashSet();
        var phrases = PhrasesInSource().ToList();
        Assert.True(phrases.Count > 100, $"only {phrases.Count} phrases found: is the source scan broken?");
        var missing = phrases.Where(p => p.Phrase.Length > 0 && !known.Contains(p.Phrase)).Select(p => $"{p.File}: {p.Phrase}").Distinct().ToList();
        Assert.True(missing.Count == 0, "No translation for:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void NamesShownFromData_HaveRows()
    {
        var known = Translations.English.ToHashSet();
        Assert.All(Language.All, l => Assert.Contains(l.EnglishName, known));
        Assert.All(Palette.Colors, c => Assert.Contains(c.Name, known));
    }

    [Fact]
    public void EveryLanguage_HasEveryRow_WithTheSameBlanks()
    {
        static string Blanks(string s) => string.Join(",", Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).Distinct().OrderBy(x => x));
        int rows = Translations.English.Distinct().Count();
        Assert.Equal(6, Translations.Table.Count);
        foreach (var (code, column) in Translations.Table)
        {
            Assert.Equal(rows, column.Count);
            foreach (var (english, text) in column)
            {
                Assert.False(string.IsNullOrWhiteSpace(text), $"{code}: empty for {english}");
                Assert.True(Blanks(english) == Blanks(text), $"{code}: blanks differ in \"{english}\" -> \"{text}\"");
            }
        }
    }

    [Fact]
    public void Lookup_FallsBackToEnglish()
    {
        try
        {
            Ui.Use(Language.Find("vi")!);
            Assert.Equal("Tạo note mới", Ui.T("New note"));
            Assert.Equal("Đã lưu 3 key dùng được. Kiểm tra bằng AI (Ctrl+G) đã sẵn sàng.", Ui.T("Saved {0} working keys. Check with AI (Ctrl+G) is ready.", 3));
            Assert.Equal("a phrase nobody translated", Ui.T("a phrase nobody translated"));
        }
        finally
        {
            Ui.Use(Language.English);
        }
    }
}
