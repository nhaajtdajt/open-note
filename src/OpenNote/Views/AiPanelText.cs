namespace OpenNote.Views;

/// <summary>The words of the Check with AI popup, in the user's own language (the AI writes its comments in it too).</summary>
internal sealed record AiPanelText(
    string Title, string Asking, string YourMeaning, string TranslationNote,
    string Closest, string MoreNatural, string TranslatedClose, string TranslatedNatural,
    string MeaningPrefix, string UseThis, string Copy, string TryAgain, string Close,
    string Good, string Unnatural, string Errors, string WrongMeaning)
{
    static readonly Dictionary<string, AiPanelText> ByLanguage = new()
    {
        ["en"] = new("Check with AI", "Asking the AI…", "Your sentence means", "Translation note",
            "Closest to your wording", "More natural", "Translation, close to your wording", "Translation, as a native would say it",
            "Meaning: ", "Use this", "Copy", "Try again", "Close (Esc)",
            "Correct and natural", "Not natural yet", "Has mistakes", "Wrong meaning"),
        ["vi"] = new("Kiểm tra bằng AI", "Đang hỏi AI…", "Câu của bạn nghĩa là", "Ghi chú khi dịch",
            "Sát với câu của bạn", "Tự nhiên hơn", "Bản dịch sát câu của bạn", "Bản dịch tự nhiên như người bản xứ",
            "Nghĩa: ", "Dùng câu này", "Sao chép", "Thử lại", "Đóng (Esc)",
            "Đúng và tự nhiên", "Chưa tự nhiên", "Có lỗi", "Sai nghĩa"),
        ["zh"] = new("AI 检查", "正在询问 AI…", "你的句子意思是", "翻译说明",
            "最贴近你的原话", "更自然", "贴近原句的翻译", "地道的翻译",
            "意思：", "使用这个", "复制", "重试", "关闭 (Esc)",
            "正确且自然", "不够自然", "有错误", "意思不对"),
        ["es"] = new("Revisar con IA", "Consultando a la IA…", "Tu frase significa", "Nota de traducción",
            "Lo más cercano a tu texto", "Más natural", "Traducción fiel a tu texto", "Traducción natural, como un nativo",
            "Significado: ", "Usar esto", "Copiar", "Reintentar", "Cerrar (Esc)",
            "Correcta y natural", "Poco natural", "Tiene errores", "Significado erróneo"),
        ["ja"] = new("AIでチェック", "AIに問い合わせ中…", "あなたの文の意味", "翻訳メモ",
            "元の文に最も近い", "より自然", "元の文に近い訳", "ネイティブらしい訳",
            "意味：", "これを使う", "コピー", "再試行", "閉じる (Esc)",
            "正しく自然", "不自然", "誤りあり", "意味が違う"),
        ["ko"] = new("AI로 확인", "AI에게 묻는 중…", "당신의 문장 의미", "번역 메모",
            "원래 표현에 가장 가까움", "더 자연스러움", "원문에 가까운 번역", "원어민다운 번역",
            "의미: ", "이걸로 사용", "복사", "다시 시도", "닫기 (Esc)",
            "정확하고 자연스러움", "부자연스러움", "오류 있음", "의미가 다름"),
        ["fr"] = new("Vérifier avec l'IA", "Interrogation de l'IA…", "Votre phrase signifie", "Note de traduction",
            "Au plus près de votre texte", "Plus naturel", "Traduction fidèle à votre texte", "Traduction naturelle, comme un natif",
            "Sens : ", "Utiliser", "Copier", "Réessayer", "Fermer (Échap)",
            "Correct et naturel", "Pas naturel", "Contient des erreurs", "Mauvais sens"),
    };

    public static AiPanelText For(Core.Language language) => ByLanguage.GetValueOrDefault(language.Code) ?? ByLanguage["en"];

    /// <summary>Every language in <see cref="Core.Language.All"/> has its text (checked by a test).</summary>
    public static bool Covers(Core.Language language) => ByLanguage.ContainsKey(language.Code);
}
