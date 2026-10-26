using OpenNote.Core;
using OpenNote.Views;

namespace OpenNote.Tests;

public class LanguageTests
{
    static Language L(string code) => Language.Find(code)!;

    [Fact]
    public void SevenLanguages_EachWithItsOwnCode_AndPopupText()
    {
        Assert.Equal(["en", "vi", "zh", "es", "ja", "ko", "fr"], Language.All.Select(l => l.Code));
        Assert.All(Language.All, l => Assert.True(AiPanelText.Covers(l), l.Code));
        Assert.Null(Language.Find("de"));
    }

    [Theory]
    [InlineData("vi", "en", "vi")] // Windows in Vietnamese
    [InlineData("en", "vi", "vi")] // English Windows, Vietnamese region (dates, numbers)
    [InlineData("ja", "en", "ja")]
    [InlineData("en", "en", "en")]
    [InlineData("de", "de", "en")] // not one of the seven
    public void YourLanguage_IsGuessedFromWindows(string display, string format, string expected) =>
        Assert.Equal(expected, Language.Guess(display, format).Code);

    [Fact]
    public void Settings_DefaultToLearningEnglish()
    {
        var s = new AppSettings();
        Assert.Equal("en", s.Learning().Code);
        Assert.Equal("", s.NativeLanguage);
        s.NativeLanguage = "ko";
        Assert.Equal("ko", s.Native().Code);
    }

    [Theory]
    // Vietnamese learning English: Vietnamese text is yours, the rest is English
    [InlineData("vi", "en", "Hôm nay tôi đi học", "vi")]
    [InlineData("vi", "en", "i has finish the report", "en")]
    // English learning Vietnamese: the other way round
    [InlineData("en", "vi", "Hôm nay tôi đi học", "vi")]
    [InlineData("en", "vi", "today I went to school", "en")]
    // scripts
    [InlineData("ja", "en", "今日は学校に行きました", "ja")]
    [InlineData("zh", "en", "我今天去学校", "zh")]
    [InlineData("zh", "ja", "今日は学校に行きました", "ja")]
    [InlineData("ko", "en", "오늘 학교에 갔어요", "ko")]
    [InlineData("ko", "en", "I went to school", "en")]
    // two languages that look alike: counted as the one being learned
    [InlineData("es", "fr", "hoy fui a la escuela", "fr")]
    // same language for both
    [InlineData("en", "en", "anything", "en")]
    public void Text_IsMatchedToOneOfYourLanguages(string native, string learning, string text, string expected) =>
        Assert.Equal(expected, LanguageHint.Guess(text, L(native), L(learning)).Code);

    [Fact]
    public void Instructions_VietnameseLearningEnglish_AreTheSameAsBefore()
    {
        var text = AiCorrector.BuildInstructions(L("vi"), L("en"));
        Assert.StartsWith("You help a Vietnamese-speaking user with short text from their sticky notes. It is usually English they are learning, or Vietnamese they want in English.", text);
        Assert.Contains("- mode: \"translate\" if the text is only Vietnamese (there is no English sentence in it to check), otherwise \"check\".", text);
        Assert.Contains("\"Nếu nghĩa tiếng Việt là '…' thì nên viết '…'. Còn câu bạn viết nghĩa là '…'.\"", text);
        Assert.DoesNotContain("{", text.Replace("JSON object", "")); // every placeholder filled
    }

    [Fact]
    public void Instructions_OtherPairs_NameBothLanguages()
    {
        var text = AiCorrector.BuildInstructions(L("ja"), L("ko"));
        Assert.Contains("Japanese-speaking", text);
        Assert.Contains("translated literally into Japanese", text);
        Assert.Contains("a native Korean speaker", text);
        Assert.Contains("say it in Japanese, in this form", text);

        var same = AiCorrector.BuildInstructions(L("en"), L("en"));
        Assert.Contains("- mode: always \"check\"", same);
    }
}
