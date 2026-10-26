namespace OpenNote.Core;

/// <summary>
/// Every phrase of the app in the seven display languages, one row per phrase so a translation
/// can never slip onto the wrong line. Columns: English (the key), Tiếng Việt, 中文, Español,
/// 日本語, 한국어, Français. {0}-style blanks must stay the same in every column (a test checks it).
/// </summary>
public static class Translations
{
    static readonly (string En, string Vi, string Zh, string Es, string Ja, string Ko, string Fr)[] Rows =
    [
        // ---------- how-to card ----------
        ("your sticky notes, and the keys that move them", "giấy note của bạn, và các phím để điều khiển", "你的便签，以及操作它们的快捷键", "tus notas adhesivas y las teclas que las mueven", "付箋と、それを動かすキー", "스티커 메모와 그것을 다루는 단축키", "vos notes et les touches qui les pilotent"),
        ("Anywhere", "Ở bất kỳ đâu", "任何地方", "En cualquier lugar", "どこでも", "어디서나", "Partout"),
        ("In a note", "Trong một note", "在便签中", "En una nota", "メモの中で", "메모 안에서", "Dans une note"),
        ("Drag a note by its top bar · throw it on the bin to archive · ☆ marks it important · with {0} on, notes turn see-through while you use other apps; click one to bring them back.",
         "Kéo note bằng thanh trên cùng · ném vào thùng để lưu trữ · ☆ đánh dấu quan trọng · bật {0}: note trong suốt khi bạn dùng app khác, bấm vào note để hiện lại.",
         "拖动顶部栏移动便签 · 扔进回收篮即可归档 · ☆ 标记为重要 · 开启 {0} 后，使用其他应用时便签会变透明，点击任意便签即可恢复。",
         "Arrastra una nota por su barra superior · lánzala a la papelera para archivarla · ☆ la marca como importante · con {0} activado, las notas se vuelven transparentes mientras usas otras apps; haz clic en una para recuperarlas.",
         "上部のバーでメモをドラッグ · ごみ箱に投げるとアーカイブ · ☆ で重要マーク · {0} をオンにすると、他のアプリを使っている間メモが透明になり、クリックすると元に戻ります。",
         "위쪽 막대로 메모를 끌어 옮기기 · 휴지통에 던지면 보관 · ☆ 는 중요 표시 · {0} 을 켜면 다른 앱을 쓰는 동안 메모가 투명해지고, 메모를 클릭하면 다시 보입니다.",
         "Faites glisser une note par sa barre du haut · lancez-la dans la corbeille pour l'archiver · ☆ la marque comme importante · avec {0} activé, les notes deviennent transparentes quand vous utilisez d'autres apps ; cliquez-en une pour les faire revenir."),
        ("Click anywhere or press Esc to close", "Bấm vào bất kỳ đâu hoặc nhấn Esc để đóng", "点击任意处或按 Esc 关闭", "Haz clic en cualquier lugar o pulsa Esc para cerrar", "どこかをクリックするか Esc で閉じる", "아무 곳이나 클릭하거나 Esc를 눌러 닫기", "Cliquez n'importe où ou appuyez sur Échap pour fermer"),
        ("New note", "Tạo note mới", "新建便签", "Nueva nota", "新しいメモ", "새 메모", "Nouvelle note"),
        ("Hide / show all notes", "Ẩn / hiện tất cả note", "隐藏 / 显示所有便签", "Ocultar / mostrar todas las notas", "すべてのメモを隠す / 表示", "모든 메모 숨기기 / 보이기", "Masquer / afficher toutes les notes"),
        ("All notes and archive", "Tất cả note và lưu trữ", "所有便签和归档", "Todas las notas y archivo", "すべてのメモとアーカイブ", "모든 메모와 보관함", "Toutes les notes et archives"),
        ("Choose notes to keep", "Chọn note giữ lại", "选择要保留的便签", "Elegir notas que mantener", "残すメモを選ぶ", "남길 메모 고르기", "Choisir les notes à garder"),
        ("Show the kept notes", "Hiện các note đã chọn", "显示保留的便签", "Mostrar las notas elegidas", "選んだメモを表示", "골라 둔 메모 보이기", "Afficher les notes gardées"),
        ("AI languages & keys", "Ngôn ngữ & key AI", "AI 语言和密钥", "Idiomas y claves de IA", "AI の言語とキー", "AI 언어와 키", "Langues et clés IA"),
        ("Fade notes in other apps", "Mờ note khi ở app khác", "在其他应用中淡化便签", "Atenuar notas en otras apps", "他のアプリ使用中は薄く", "다른 앱 사용 중 흐리게", "Estomper hors de l'app"),
        ("This guide", "Bảng hướng dẫn này", "本指南", "Esta guía", "このガイド", "이 안내", "Ce guide"),
        ("Quit Open Note", "Thoát Open Note", "退出 Open Note", "Salir de Open Note", "Open Note を終了", "Open Note 종료", "Quitter Open Note"),
        ("Bold / italic", "Chữ đậm / nghiêng", "粗体 / 斜体", "Negrita / cursiva", "太字 / 斜体", "굵게 / 기울임", "Gras / italique"),
        ("Bullet / checkbox", "Gạch đầu dòng / ô tick", "项目符号 / 复选框", "Viñeta / casilla", "箇条書き / チェック", "글머리 / 체크박스", "Puce / case à cocher"),
        ("Text size", "Cỡ chữ lớn / nhỏ", "文字大小", "Tamaño del texto", "文字サイズ", "글자 크기", "Taille du texte"),
        ("Check or translate (AI)", "Sửa hoặc dịch (AI)", "检查或翻译 (AI)", "Revisar o traducir (IA)", "チェック・翻訳 (AI)", "확인 또는 번역 (AI)", "Vérifier ou traduire (IA)"),
        ("Read aloud", "Đọc to", "朗读", "Leer en voz alta", "読み上げ", "소리 내어 읽기", "Lire à voix haute"),
        ("Star: important", "Đánh sao: quan trọng", "星标：重要", "Estrella: importante", "星：重要", "별표: 중요", "Étoile : important"),
        ("Close the note", "Đóng note", "关闭便签", "Cerrar la nota", "メモを閉じる", "메모 닫기", "Fermer la note"),
        ("Archive the note", "Lưu trữ note", "归档便签", "Archivar la nota", "メモをアーカイブ", "메모 보관", "Archiver la note"),

        // ---------- the welcome note ----------
        ("Welcome to Open Note", "Chào mừng đến với Open Note", "欢迎使用 Open Note", "Bienvenido a Open Note", "Open Note へようこそ", "Open Note에 오신 것을 환영합니다", "Bienvenue dans Open Note"),
        ("Drag the header to move me", "Kéo thanh trên cùng để di chuyển tôi", "拖动顶部栏来移动我", "Arrastra la barra superior para moverme", "上部のバーをドラッグして移動", "위쪽 막대를 끌어 나를 옮겨 보세요", "Faites glisser la barre du haut pour me déplacer"),
        ("Click the title to rename me", "Bấm vào tiêu đề để đổi tên tôi", "点击标题给我改名", "Haz clic en el título para renombrarme", "タイトルをクリックして名前を変更", "제목을 클릭해 이름을 바꿔 보세요", "Cliquez sur le titre pour me renommer"),
        ("Type /> for a box, /- for a bullet", "Gõ /> để có ô tick, /- để có gạch đầu dòng", "输入 /> 插入复选框，/- 插入项目符号", "Escribe /> para una casilla, /- para una viñeta", "/> でチェック、/- で箇条書き", "/> 를 입력하면 체크박스, /- 는 글머리표", "Tapez /> pour une case, /- pour une puce"),
        ("Right-click the header for colors", "Bấm chuột phải vào thanh trên cùng để đổi màu", "右键点击顶部栏选择颜色", "Haz clic derecho en la barra superior para los colores", "上部のバーを右クリックで色を変更", "위쪽 막대를 오른쪽 클릭하면 색상", "Clic droit sur la barre du haut pour les couleurs"),
        ("new note", "note mới", "新建便签", "nueva nota", "新しいメモ", "새 메모", "nouvelle note"),
        ("show or hide notes", "ẩn hoặc hiện note", "显示或隐藏便签", "mostrar u ocultar notas", "メモを表示・非表示", "메모 보이기/숨기기", "afficher ou masquer les notes"),
        ("all notes", "tất cả note", "所有便签", "todas las notas", "すべてのメモ", "모든 메모", "toutes les notes"),

        // ---------- notification-area messages ----------
        ("Couldn't save the setting", "Không lưu được cài đặt", "无法保存设置", "No se pudo guardar el ajuste", "設定を保存できませんでした", "설정을 저장하지 못했습니다", "Impossible d'enregistrer le réglage"),
        ("Couldn't save the languages", "Không lưu được ngôn ngữ", "无法保存语言设置", "No se pudieron guardar los idiomas", "言語を保存できませんでした", "언어를 저장하지 못했습니다", "Impossible d'enregistrer les langues"),
        ("Open Note couldn't save", "Open Note không lưu được", "Open Note 无法保存", "Open Note no pudo guardar", "Open Note が保存できませんでした", "Open Note가 저장하지 못했습니다", "Open Note n'a pas pu enregistrer"),
        ("Your notes are kept in memory and saving will be retried.", "Note của bạn vẫn được giữ trong bộ nhớ và app sẽ thử lưu lại.", "你的便签仍保存在内存中，稍后会重试保存。", "Tus notas se mantienen en memoria y se volverá a intentar guardarlas.", "メモはメモリに保持され、保存を再試行します。", "메모는 메모리에 남아 있으며 저장을 다시 시도합니다.", "Vos notes restent en mémoire et l'enregistrement sera retenté."),
        ("No notes on the desktop", "Không có note nào trên màn hình", "桌面上没有便签", "No hay notas en el escritorio", "デスクトップにメモがありません", "바탕 화면에 메모가 없습니다", "Aucune note sur le bureau"),
        ("all notes and archive", "tất cả note và lưu trữ", "所有便签和归档", "todas las notas y archivo", "すべてのメモとアーカイブ", "모든 메모와 보관함", "toutes les notes et archives"),
        ("Some shortcuts are unavailable", "Một số phím tắt không dùng được", "部分快捷键不可用", "Algunos atajos no están disponibles", "一部のショートカットが使えません", "일부 단축키를 사용할 수 없습니다", "Certains raccourcis sont indisponibles"),
        ("Change them via the tray icon → Edit settings.", "Đổi chúng ở biểu tượng khay hệ thống → Edit settings.", "可通过托盘图标 → Edit settings 修改。", "Cámbialos en el icono de la bandeja → Edit settings.", "トレイアイコン → Edit settings で変更できます。", "트레이 아이콘 → Edit settings 에서 바꾸세요.", "Modifiez-les via l'icône de la zone de notification → Edit settings."),
        ("Notes fade while you use other apps", "Note sẽ mờ khi bạn dùng app khác", "使用其他应用时便签会变淡", "Las notas se atenúan mientras usas otras apps", "他のアプリを使う間、メモは薄くなります", "다른 앱을 쓰는 동안 메모가 흐려집니다", "Les notes s'estompent quand vous utilisez d'autres apps"),
        ("Notes stay solid", "Note giữ nguyên độ rõ", "便签保持清晰", "Las notas se quedan opacas", "メモは常にはっきり表示", "메모가 그대로 선명하게 보입니다", "Les notes restent opaques"),
        ("Click a note to bring them back. {0} turns this off.", "Bấm vào một note để hiện lại. {0} để tắt.", "点击任意便签即可恢复。按 {0} 关闭此功能。", "Haz clic en una nota para recuperarlas. {0} lo desactiva.", "メモをクリックすると元に戻ります。{0} でオフ。", "메모를 클릭하면 다시 보입니다. {0} 로 끕니다.", "Cliquez sur une note pour les faire revenir. {0} désactive cela."),
        ("{0} turns fading back on.", "{0} để bật lại chế độ mờ.", "按 {0} 重新开启淡化。", "{0} vuelve a activar la atenuación.", "{0} で再びオン。", "{0} 로 다시 켭니다.", "{0} réactive l'estompage."),
        ("not a valid shortcut", "không phải phím tắt hợp lệ", "不是有效的快捷键", "no es un atajo válido", "有効なショートカットではありません", "올바른 단축키가 아닙니다", "n'est pas un raccourci valide"),
        ("already used by another app", "đã được app khác dùng", "已被其他应用占用", "ya lo usa otra app", "他のアプリが使用中です", "다른 앱이 이미 사용 중입니다", "déjà utilisé par une autre app"),

        // ---------- notification-area menu ----------
        ("Show all notes", "Hiện tất cả note", "显示所有便签", "Mostrar todas las notas", "すべてのメモを表示", "모든 메모 보이기", "Afficher toutes les notes"),
        ("Hide all notes", "Ẩn tất cả note", "隐藏所有便签", "Ocultar todas las notas", "すべてのメモを隠す", "모든 메모 숨기기", "Masquer toutes les notes"),
        ("Choose notes to keep…", "Chọn note giữ lại…", "选择要保留的便签…", "Elegir notas que mantener…", "残すメモを選ぶ…", "남길 메모 고르기…", "Choisir les notes à garder…"),
        ("Show saved selection", "Hiện các note đã chọn", "显示保存的选择", "Mostrar la selección guardada", "保存した選択を表示", "저장한 선택 보이기", "Afficher la sélection enregistrée"),
        ("All notes and archive…", "Tất cả note và lưu trữ…", "所有便签和归档…", "Todas las notas y archivo…", "すべてのメモとアーカイブ…", "모든 메모와 보관함…", "Toutes les notes et archives…"),
        ("Keep notes on top", "Giữ note luôn ở trên cùng", "便签始终置顶", "Mantener las notas encima", "メモを常に手前に表示", "메모를 항상 위에", "Garder les notes au premier plan"),
        ("Note effects (shake, throw, archive bin)", "Hiệu ứng note (lắc, ném, thùng lưu trữ)", "便签效果（摇晃、抛出、归档篮）", "Efectos de notas (agitar, lanzar, papelera)", "メモの効果（振る・投げる・ごみ箱）", "메모 효과 (흔들기, 던지기, 보관함)", "Effets des notes (secouer, lancer, corbeille)"),
        ("Fade notes while I use other apps", "Làm mờ note khi tôi dùng app khác", "使用其他应用时淡化便签", "Atenuar las notas mientras uso otras apps", "他のアプリ使用中はメモを薄く", "다른 앱을 쓰는 동안 메모 흐리게", "Estomper les notes quand j'utilise d'autres apps"),
        ("AI languages and keys…", "Ngôn ngữ và key AI…", "AI 语言和密钥…", "Idiomas y claves de IA…", "AI の言語とキー…", "AI 언어와 키…", "Langues et clés IA…"),
        ("Start with Windows…", "Chạy cùng Windows…", "开机启动…", "Iniciar con Windows…", "Windows と同時に起動…", "Windows 시작 시 실행…", "Démarrer avec Windows…"),
        ("Start with Windows", "Chạy cùng Windows", "开机启动", "Iniciar con Windows", "Windows と同時に起動", "Windows 시작 시 실행", "Démarrer avec Windows"),
        ("Edit settings…", "Sửa cài đặt…", "编辑设置…", "Editar ajustes…", "設定を編集…", "설정 편집…", "Modifier les réglages…"),
        ("Reload settings", "Tải lại cài đặt", "重新加载设置", "Recargar ajustes", "設定を再読み込み", "설정 다시 불러오기", "Recharger les réglages"),
        ("Open data folder", "Mở thư mục dữ liệu", "打开数据文件夹", "Abrir la carpeta de datos", "データフォルダーを開く", "데이터 폴더 열기", "Ouvrir le dossier des données"),
        ("How to use…", "Cách sử dụng…", "使用说明…", "Cómo se usa…", "使い方…", "사용 방법…", "Mode d'emploi…"),
        ("Exit", "Thoát", "退出", "Salir", "終了", "종료", "Quitter"),

        // ---------- read aloud ----------
        ("There is nothing to read.", "Không có gì để đọc.", "没有可朗读的内容。", "No hay nada que leer.", "読み上げる内容がありません。", "읽을 내용이 없습니다.", "Il n'y a rien à lire."),
        ("Windows speech isn't available on this PC.", "Máy này không có chức năng đọc của Windows.", "此电脑无法使用 Windows 语音。", "La voz de Windows no está disponible en este PC.", "この PC では Windows の音声を使えません。", "이 PC에서는 Windows 음성을 사용할 수 없습니다.", "La voix de Windows n'est pas disponible sur ce PC."),
        ("No {0} voice is installed. Add one in Settings → Time & language → Speech.", "Chưa cài giọng đọc {0}. Thêm trong Settings → Time & language → Speech.", "未安装{0}语音。请在 设置 → 时间和语言 → 语音 中添加。", "No hay ninguna voz en {0} instalada. Añade una en Configuración → Hora e idioma → Voz.", "{0}の音声がインストールされていません。設定 → 時刻と言語 → 音声 で追加してください。", "{0} 음성이 설치되어 있지 않습니다. 설정 → 시간 및 언어 → 음성 에서 추가하세요.", "Aucune voix {0} n'est installée. Ajoutez-en une dans Paramètres → Heure et langue → Voix."),
        ("Couldn't read this text aloud.", "Không đọc to được đoạn này.", "无法朗读这段文字。", "No se pudo leer este texto en voz alta.", "この文章を読み上げられませんでした。", "이 텍스트를 읽지 못했습니다.", "Impossible de lire ce texte à voix haute."),
        ("English", "tiếng Anh", "英语", "inglés", "英語", "영어", "anglais"),
        ("Vietnamese", "tiếng Việt", "越南语", "vietnamita", "ベトナム語", "베트남어", "vietnamien"),
        ("Simplified Chinese", "tiếng Trung giản thể", "简体中文", "chino simplificado", "簡体字中国語", "중국어 간체", "chinois simplifié"),
        ("Spanish", "tiếng Tây Ban Nha", "西班牙语", "español", "スペイン語", "스페인어", "espagnol"),
        ("Japanese", "tiếng Nhật", "日语", "japonés", "日本語", "일본어", "japonais"),
        ("Korean", "tiếng Hàn", "韩语", "coreano", "韓国語", "한국어", "coréen"),
        ("French", "tiếng Pháp", "法语", "francés", "フランス語", "프랑스어", "français"),

        // ---------- settings file ----------
        ("settings.json has a mistake in it, so it wasn't changed. Fix it (tray icon → Edit settings), then Reload settings.", "settings.json có lỗi nên chưa được thay đổi. Sửa nó (biểu tượng khay → Edit settings), rồi chọn Reload settings.", "settings.json 有错误，因此未更改。请修正（托盘图标 → Edit settings），然后选择 Reload settings。", "settings.json tiene un error, así que no se cambió. Corrígelo (icono de la bandeja → Edit settings) y luego Reload settings.", "settings.json に誤りがあるため変更されませんでした。修正してから（トレイアイコン → Edit settings）Reload settings を選んでください。", "settings.json 에 오류가 있어 바꾸지 않았습니다. 고친 뒤(트레이 아이콘 → Edit settings) Reload settings 를 누르세요.", "settings.json contient une erreur, il n'a donc pas été modifié. Corrigez-le (icône de notification → Edit settings), puis Reload settings."),
        ("settings.json couldn't be saved: {0}", "Không lưu được settings.json: {0}", "无法保存 settings.json：{0}", "No se pudo guardar settings.json: {0}", "settings.json を保存できませんでした: {0}", "settings.json 을 저장하지 못했습니다: {0}", "Impossible d'enregistrer settings.json : {0}"),
        ("settings.json could not be read ({0}). Using defaults until it's fixed.", "Không đọc được settings.json ({0}). Dùng cài đặt mặc định cho tới khi sửa xong.", "无法读取 settings.json（{0}）。修正前将使用默认设置。", "No se pudo leer settings.json ({0}). Se usan los valores predeterminados hasta que se corrija.", "settings.json を読み込めませんでした（{0}）。修正されるまで既定の設定を使います。", "settings.json 을 읽지 못했습니다 ({0}). 고칠 때까지 기본값을 사용합니다.", "Impossible de lire settings.json ({0}). Les valeurs par défaut sont utilisées en attendant."),

        // ---------- Check with AI ----------
        ("No AI key yet. Press Ctrl+Alt+K to add a free Gemini API key.", "Chưa có key AI. Nhấn Ctrl+Alt+K để thêm key Gemini miễn phí.", "还没有 AI 密钥。按 Ctrl+Alt+K 添加免费的 Gemini API 密钥。", "Aún no hay clave de IA. Pulsa Ctrl+Alt+K para añadir una clave gratuita de Gemini.", "AI キーがまだありません。Ctrl+Alt+K で無料の Gemini API キーを追加してください。", "아직 AI 키가 없습니다. Ctrl+Alt+K 를 눌러 무료 Gemini API 키를 추가하세요.", "Pas encore de clé IA. Appuyez sur Ctrl+Alt+K pour ajouter une clé Gemini gratuite."),
        ("Couldn't reach the AI service. Check your internet connection.", "Không kết nối được dịch vụ AI. Kiểm tra kết nối mạng.", "无法连接 AI 服务。请检查网络连接。", "No se pudo conectar con el servicio de IA. Revisa tu conexión a internet.", "AI サービスに接続できませんでした。インターネット接続を確認してください。", "AI 서비스에 연결하지 못했습니다. 인터넷 연결을 확인하세요.", "Impossible de joindre le service d'IA. Vérifiez votre connexion internet."),
        ("All API keys are out of quota for now. Try again later.", "Tất cả key đã hết lượt dùng lúc này. Thử lại sau.", "所有 API 密钥的额度暂时用完了。请稍后再试。", "Todas las claves han agotado su cuota por ahora. Inténtalo más tarde.", "すべての API キーの上限に達しています。後でもう一度お試しください。", "모든 API 키의 한도를 지금은 다 썼습니다. 나중에 다시 시도하세요.", "Toutes les clés ont épuisé leur quota pour l'instant. Réessayez plus tard."),
        ("The AI service rejected the API keys. Check them in settings.json.", "Dịch vụ AI từ chối các key. Kiểm tra lại trong settings.json.", "AI 服务拒绝了 API 密钥。请在 settings.json 中检查。", "El servicio de IA rechazó las claves. Revísalas en settings.json.", "AI サービスが API キーを拒否しました。settings.json を確認してください。", "AI 서비스가 API 키를 거부했습니다. settings.json 에서 확인하세요.", "Le service d'IA a refusé les clés. Vérifiez-les dans settings.json."),
        ("The AI service is busy right now. Try again in a moment.", "Dịch vụ AI đang bận. Thử lại sau giây lát.", "AI 服务正忙。请稍后再试。", "El servicio de IA está ocupado ahora. Inténtalo en un momento.", "AI サービスが混み合っています。少し待ってから再試行してください。", "AI 서비스가 지금 바쁩니다. 잠시 후 다시 시도하세요.", "Le service d'IA est occupé. Réessayez dans un instant."),
        ("Check with AI failed. Please try again.", "Kiểm tra bằng AI thất bại. Vui lòng thử lại.", "AI 检查失败。请重试。", "La revisión con IA falló. Inténtalo de nuevo.", "AI チェックに失敗しました。もう一度お試しください。", "AI 확인에 실패했습니다. 다시 시도하세요.", "La vérification par l'IA a échoué. Réessayez."),
        ("The text changed while the AI was answering, so the suggestion was copied to the clipboard instead.", "Đoạn chữ đã thay đổi trong lúc AI trả lời, nên gợi ý được sao chép vào bộ nhớ tạm thay vì thay thế.", "AI 回答时文字已改变，因此建议已复制到剪贴板。", "El texto cambió mientras la IA respondía, así que la sugerencia se copió al portapapeles.", "AI の回答中に文章が変わったため、提案はクリップボードにコピーしました。", "AI가 답하는 동안 텍스트가 바뀌어 제안을 클립보드에 복사했습니다.", "Le texte a changé pendant la réponse de l'IA : la suggestion a été copiée dans le presse-papiers."),

        // ---------- the AI languages and keys window ----------
        ("Open Note – Check with AI", "Open Note – Kiểm tra bằng AI", "Open Note – AI 检查", "Open Note – Revisar con IA", "Open Note – AI でチェック", "Open Note – AI로 확인", "Open Note – Vérifier avec l'IA"),
        ("Check with AI: languages and keys", "Kiểm tra bằng AI: ngôn ngữ và key", "AI 检查：语言和密钥", "Revisar con IA: idiomas y claves", "AI でチェック：言語とキー", "AI로 확인: 언어와 키", "Vérifier avec l'IA : langues et clés"),
        ("Check with AI (Ctrl+G) needs a free Gemini key. Your keys and choices stay on this PC.", "Kiểm tra bằng AI (Ctrl+G) cần một key Gemini miễn phí. Key và lựa chọn của bạn chỉ lưu trên máy này.", "AI 检查 (Ctrl+G) 需要一个免费的 Gemini 密钥。你的密钥和选择只保存在这台电脑上。", "Revisar con IA (Ctrl+G) necesita una clave gratuita de Gemini. Tus claves y opciones se quedan en este PC.", "AI チェック (Ctrl+G) には無料の Gemini キーが必要です。キーと設定はこの PC にだけ保存されます。", "AI 확인 (Ctrl+G) 에는 무료 Gemini 키가 필요합니다. 키와 선택은 이 PC에만 저장됩니다.", "Vérifier avec l'IA (Ctrl+G) nécessite une clé Gemini gratuite. Vos clés et vos choix restent sur ce PC."),
        ("Languages", "Ngôn ngữ", "语言", "Idiomas", "言語", "언어", "Langues"),
        ("Your language", "Ngôn ngữ của bạn", "你的语言", "Tu idioma", "あなたの言語", "내 언어", "Votre langue"),
        ("Language you're learning", "Ngôn ngữ bạn đang học", "你在学习的语言", "Idioma que aprendes", "学習中の言語", "배우는 언어", "Langue que vous apprenez"),
        ("Your {0} is checked and made more natural. Explanations are in {1}.", "Câu {0} của bạn được kiểm tra và viết lại tự nhiên hơn. Giải thích bằng {1}.", "你的{0}会被检查并改得更自然。说明使用{1}。", "Tu {0} se revisa y se hace más natural. Las explicaciones están en {1}.", "あなたの{0}をチェックし、より自然にします。説明は{1}です。", "당신의 {0} 문장을 확인하고 더 자연스럽게 다듬습니다. 설명은 {1}로 나옵니다.", "Votre {0} est vérifié et rendu plus naturel. Les explications sont en {1}."),
        ("{0} text is translated into {1}, and {1} text is checked. Explanations are in {0}. Spell check and read aloud follow these two languages.", "Chữ {0} được dịch sang {1}, chữ {1} được kiểm tra. Giải thích bằng {0}. Kiểm tra chính tả và đọc to theo hai ngôn ngữ này.", "{0}文字会翻译成{1}，{1}文字会被检查。说明使用{0}。拼写检查和朗读也按这两种语言进行。", "El texto en {0} se traduce al {1}, y el texto en {1} se revisa. Las explicaciones están en {0}. La ortografía y la lectura en voz alta siguen estos dos idiomas.", "{0}の文は{1}に翻訳され、{1}の文はチェックされます。説明は{0}です。スペルチェックと読み上げもこの二つの言語に従います。", "{0} 텍스트는 {1}로 번역되고, {1} 텍스트는 확인됩니다. 설명은 {0}로 나옵니다. 맞춤법 검사와 읽기도 이 두 언어를 따릅니다.", "Le texte en {0} est traduit en {1}, et le texte en {1} est vérifié. Les explications sont en {0}. L'orthographe et la lecture à voix haute suivent ces deux langues."),
        ("How to get a key", "Cách lấy key", "如何获取密钥", "Cómo conseguir una clave", "キーの入手方法", "키 받는 방법", "Comment obtenir une clé"),
        ("1. Open Google AI Studio and sign in with your Google account.", "1. Mở Google AI Studio và đăng nhập bằng tài khoản Google.", "1. 打开 Google AI Studio，用你的 Google 账号登录。", "1. Abre Google AI Studio e inicia sesión con tu cuenta de Google.", "1. Google AI Studio を開き、Google アカウントでログインします。", "1. Google AI Studio 를 열고 Google 계정으로 로그인합니다.", "1. Ouvrez Google AI Studio et connectez-vous avec votre compte Google."),
        ("2. Click \"Create API key\", choose a project (or create one), then copy the key.", "2. Bấm \"Create API key\", chọn một project (hoặc tạo mới), rồi sao chép key.", "2. 点击 \"Create API key\"，选择一个项目（或新建），然后复制密钥。", "2. Haz clic en \"Create API key\", elige un proyecto (o crea uno) y copia la clave.", "2. 「Create API key」をクリックし、プロジェクトを選ぶ（または作成）してキーをコピーします。", "2. \"Create API key\" 를 누르고 프로젝트를 고르거나 만든 뒤 키를 복사합니다.", "2. Cliquez sur « Create API key », choisissez un projet (ou créez-en un), puis copiez la clé."),
        ("3. Paste it below (one key per line) and click \"Check and save\".", "3. Dán vào ô bên dưới (mỗi dòng một key) rồi bấm \"Kiểm tra và lưu\".", "3. 粘贴到下方（每行一个密钥），然后点击 \"检查并保存\"。", "3. Pégala abajo (una clave por línea) y haz clic en \"Comprobar y guardar\".", "3. 下に貼り付け（1 行に 1 つ）、「確認して保存」をクリックします。", "3. 아래에 붙여 넣고(한 줄에 키 하나) \"확인 후 저장\" 을 누릅니다.", "3. Collez-la ci-dessous (une clé par ligne) et cliquez sur « Vérifier et enregistrer »."),
        ("Open aistudio.google.com/apikey", "Mở aistudio.google.com/apikey", "打开 aistudio.google.com/apikey", "Abrir aistudio.google.com/apikey", "aistudio.google.com/apikey を開く", "aistudio.google.com/apikey 열기", "Ouvrir aistudio.google.com/apikey"),
        ("Tip: more free requests.", "Mẹo: thêm lượt dùng miễn phí.", "提示：获得更多免费请求。", "Consejo: más solicitudes gratis.", "ヒント：無料リクエストを増やす。", "팁: 무료 요청 늘리기.", "Astuce : plus de requêtes gratuites."),
        ("The free quota belongs to a Google Cloud project, so keys in the same project share it. Create keys in several projects (choose \"Create API key in new project\" each time) and paste them all. Open Note uses them in turn and skips one that has run out for the day.", "Lượt miễn phí tính theo project Google Cloud, nên các key cùng project dùng chung. Hãy tạo key ở nhiều project (mỗi lần chọn \"Create API key in new project\") rồi dán tất cả vào. Open Note dùng lần lượt và bỏ qua key đã hết lượt trong ngày.", "免费额度按 Google Cloud 项目计算，同一项目的密钥共享额度。请在多个项目中创建密钥（每次选择 \"Create API key in new project\"）并全部粘贴。Open Note 会轮流使用，并跳过当天额度用完的密钥。", "La cuota gratuita pertenece a un proyecto de Google Cloud, así que las claves del mismo proyecto la comparten. Crea claves en varios proyectos (elige \"Create API key in new project\" cada vez) y pégalas todas. Open Note las usa por turnos y salta la que se haya agotado ese día.", "無料枠は Google Cloud のプロジェクト単位なので、同じプロジェクトのキーは枠を共有します。複数のプロジェクトでキーを作り（毎回「Create API key in new project」を選択）、すべて貼り付けてください。Open Note は順番に使い、その日の枠を使い切ったキーは飛ばします。", "무료 한도는 Google Cloud 프로젝트 단위라서 같은 프로젝트의 키는 한도를 함께 씁니다. 여러 프로젝트에서 키를 만들어(매번 \"Create API key in new project\" 선택) 모두 붙여 넣으세요. Open Note가 차례로 쓰고 그날 한도를 다 쓴 키는 건너뜁니다.", "Le quota gratuit appartient à un projet Google Cloud : les clés d'un même projet le partagent. Créez des clés dans plusieurs projets (choisissez « Create API key in new project » à chaque fois) et collez-les toutes. Open Note les utilise à tour de rôle et passe celle qui est épuisée pour la journée."),
        ("Your keys (one per line)", "Key của bạn (mỗi dòng một key)", "你的密钥（每行一个）", "Tus claves (una por línea)", "あなたのキー（1 行に 1 つ）", "내 키 (한 줄에 하나)", "Vos clés (une par ligne)"),
        ("Privacy: when you use Check with AI, only the text you selected (with the note's title and the few words before it, for context) is sent to Google. On the free tier Google may use it to improve its products, so don't check private or sensitive text.", "Quyền riêng tư: khi bạn dùng Kiểm tra bằng AI, chỉ đoạn chữ bạn chọn (kèm tiêu đề note và vài từ phía trước để hiểu ngữ cảnh) được gửi tới Google. Ở gói miễn phí, Google có thể dùng nó để cải thiện sản phẩm, nên đừng kiểm tra nội dung riêng tư hay nhạy cảm.", "隐私：使用 AI 检查时，只有你选中的文字（以及便签标题和前面几个词，用于理解上下文）会发送给 Google。免费版中 Google 可能用它改进产品，请勿检查私密或敏感内容。", "Privacidad: al usar Revisar con IA, solo se envía a Google el texto seleccionado (con el título de la nota y unas palabras anteriores, como contexto). En el nivel gratuito Google puede usarlo para mejorar sus productos, así que no revises texto privado o sensible.", "プライバシー：AI チェックでは、選んだ文章（文脈のためメモのタイトルと直前の数語を含む）だけが Google に送られます。無料枠では Google が製品改善に使う場合があるので、個人的・機密の文章はチェックしないでください。", "개인정보: AI 확인을 쓰면 선택한 텍스트(문맥을 위해 메모 제목과 바로 앞의 몇 단어 포함)만 Google로 보냅니다. 무료 등급에서는 Google이 제품 개선에 쓸 수 있으니 개인적이거나 민감한 내용은 확인하지 마세요.", "Confidentialité : avec Vérifier avec l'IA, seul le texte sélectionné (avec le titre de la note et quelques mots qui précèdent, pour le contexte) est envoyé à Google. En formule gratuite, Google peut l'utiliser pour améliorer ses produits : ne vérifiez pas de texte privé ou sensible."),
        ("Close", "Đóng", "关闭", "Cerrar", "閉じる", "닫기", "Fermer"),
        ("Check and save", "Kiểm tra và lưu", "检查并保存", "Comprobar y guardar", "確認して保存", "확인 후 저장", "Vérifier et enregistrer"),
        ("No browser could be opened. The address was copied: paste it into your browser.", "Không mở được trình duyệt. Địa chỉ đã được sao chép: hãy dán vào trình duyệt.", "无法打开浏览器。地址已复制，请粘贴到浏览器中。", "No se pudo abrir el navegador. La dirección se copió: pégala en tu navegador.", "ブラウザーを開けませんでした。アドレスをコピーしたので、ブラウザーに貼り付けてください。", "브라우저를 열지 못했습니다. 주소를 복사했으니 브라우저에 붙여 넣으세요.", "Aucun navigateur n'a pu s'ouvrir. L'adresse a été copiée : collez-la dans votre navigateur."),
        ("No keys saved. Check with AI stays off until you add one.", "Chưa lưu key nào. Kiểm tra bằng AI sẽ tắt cho tới khi bạn thêm key.", "未保存任何密钥。添加密钥前 AI 检查保持关闭。", "No se guardó ninguna clave. Revisar con IA seguirá desactivado hasta que añadas una.", "キーは保存されていません。追加するまで AI チェックはオフのままです。", "저장된 키가 없습니다. 키를 추가할 때까지 AI 확인은 꺼져 있습니다.", "Aucune clé enregistrée. Vérifier avec l'IA reste désactivé jusqu'à ce que vous en ajoutiez une."),
        ("Checking your key with Google…", "Đang kiểm tra key với Google…", "正在向 Google 检查你的密钥…", "Comprobando tu clave con Google…", "Google でキーを確認しています…", "Google에서 키를 확인하는 중…", "Vérification de votre clé auprès de Google…"),
        ("Checking {0} keys with Google…", "Đang kiểm tra {0} key với Google…", "正在向 Google 检查 {0} 个密钥…", "Comprobando {0} claves con Google…", "Google で {0} 個のキーを確認しています…", "Google에서 키 {0}개를 확인하는 중…", "Vérification de {0} clés auprès de Google…"),
        ("None of these keys can be used, so nothing was saved. Check that you copied the whole key.", "Không key nào dùng được nên chưa lưu gì. Kiểm tra xem bạn đã sao chép đủ key chưa.", "这些密钥都无法使用，因此没有保存。请确认复制了完整的密钥。", "Ninguna de estas claves sirve, así que no se guardó nada. Comprueba que copiaste la clave completa.", "使えるキーがないため保存しませんでした。キー全体をコピーしたか確認してください。", "쓸 수 있는 키가 없어 저장하지 않았습니다. 키 전체를 복사했는지 확인하세요.", "Aucune de ces clés n'est utilisable : rien n'a été enregistré. Vérifiez que vous avez copié la clé entière."),
        ("Saved 1 working key. Check with AI (Ctrl+G) is ready.", "Đã lưu 1 key dùng được. Kiểm tra bằng AI (Ctrl+G) đã sẵn sàng.", "已保存 1 个可用密钥。AI 检查 (Ctrl+G) 已就绪。", "Se guardó 1 clave que funciona. Revisar con IA (Ctrl+G) está listo.", "使えるキーを 1 つ保存しました。AI チェック (Ctrl+G) を使えます。", "작동하는 키 1개를 저장했습니다. AI 확인 (Ctrl+G) 을 쓸 수 있습니다.", "1 clé fonctionnelle enregistrée. Vérifier avec l'IA (Ctrl+G) est prêt."),
        ("Saved {0} working keys. Check with AI (Ctrl+G) is ready.", "Đã lưu {0} key dùng được. Kiểm tra bằng AI (Ctrl+G) đã sẵn sàng.", "已保存 {0} 个可用密钥。AI 检查 (Ctrl+G) 已就绪。", "Se guardaron {0} claves que funcionan. Revisar con IA (Ctrl+G) está listo.", "使えるキーを {0} 個保存しました。AI チェック (Ctrl+G) を使えます。", "작동하는 키 {0}개를 저장했습니다. AI 확인 (Ctrl+G) 을 쓸 수 있습니다.", "{0} clés fonctionnelles enregistrées. Vérifier avec l'IA (Ctrl+G) est prêt."),
        ("Saved {0} keys ({1} working right now). Keys that were rejected were left out.", "Đã lưu {0} key ({1} key đang dùng được). Các key bị từ chối đã bị bỏ ra.", "已保存 {0} 个密钥（当前 {1} 个可用）。被拒绝的密钥未保存。", "Se guardaron {0} claves ({1} funcionan ahora). Las rechazadas se dejaron fuera.", "{0} 個のキーを保存しました（今使えるのは {1} 個）。拒否されたキーは除きました。", "키 {0}개를 저장했습니다 (지금 작동하는 키 {1}개). 거부된 키는 뺐습니다.", "{0} clés enregistrées ({1} fonctionnent en ce moment). Les clés refusées ont été écartées."),
        ("Works", "Dùng được", "可用", "Funciona", "使用可能", "작동함", "Fonctionne"),
        ("Works, but its free quota is used up for now", "Dùng được, nhưng đã hết lượt miễn phí lúc này", "可用，但免费额度暂时用完", "Funciona, pero su cuota gratuita está agotada por ahora", "使用可能ですが、無料枠を使い切っています", "작동하지만 지금은 무료 한도를 다 썼습니다", "Fonctionne, mais son quota gratuit est épuisé pour l'instant"),
        ("Not a valid key", "Key không hợp lệ", "无效的密钥", "No es una clave válida", "無効なキーです", "올바른 키가 아닙니다", "Clé non valide"),
        ("Not allowed to use the Gemini API", "Không được phép dùng Gemini API", "无权使用 Gemini API", "No tiene permiso para usar la API de Gemini", "Gemini API の使用が許可されていません", "Gemini API 사용 권한이 없습니다", "Non autorisée à utiliser l'API Gemini"),
        ("Couldn't reach Google (no internet?)", "Không kết nối được Google (mất mạng?)", "无法连接 Google（没有网络？）", "No se pudo conectar con Google (¿sin internet?)", "Google に接続できません（オフライン？）", "Google에 연결하지 못했습니다 (인터넷 없음?)", "Impossible de joindre Google (pas d'internet ?)"),
        ("Google had a problem; try again later", "Google đang gặp sự cố; thử lại sau", "Google 出现问题，请稍后再试", "Google tuvo un problema; inténtalo más tarde", "Google 側で問題が発生しました。後でお試しください", "Google에 문제가 있습니다. 나중에 다시 시도하세요", "Google a rencontré un problème ; réessayez plus tard"),

        // ---------- all notes ----------
        ("Search notes  (Ctrl+F)", "Tìm note  (Ctrl+F)", "搜索便签  (Ctrl+F)", "Buscar notas  (Ctrl+F)", "メモを検索  (Ctrl+F)", "메모 검색  (Ctrl+F)", "Rechercher  (Ctrl+F)"),
        ("Archive", "Lưu trữ", "归档", "Archivo", "アーカイブ", "보관함", "Archives"),
        ("Restore to desktop", "Đưa lại ra màn hình", "恢复到桌面", "Devolver al escritorio", "デスクトップに戻す", "바탕 화면으로 복원", "Remettre sur le bureau"),
        ("Delete permanently", "Xóa vĩnh viễn", "永久删除", "Eliminar para siempre", "完全に削除", "영구 삭제", "Supprimer définitivement"),
        ("Double-click a note to open it. Press Delete to archive. {0} shows or hides your notes from anywhere.", "Nhấp đúp một note để mở. Nhấn Delete để lưu trữ. {0} ẩn hoặc hiện note ở bất kỳ đâu.", "双击便签打开。按 Delete 归档。在任何地方按 {0} 显示或隐藏便签。", "Haz doble clic en una nota para abrirla. Pulsa Supr para archivarla. {0} muestra u oculta tus notas desde cualquier lugar.", "メモをダブルクリックで開きます。Delete でアーカイブ。{0} でどこからでもメモを表示・非表示。", "메모를 두 번 클릭하면 열립니다. Delete 를 누르면 보관. {0} 로 어디서나 메모를 보이거나 숨깁니다.", "Double-cliquez sur une note pour l'ouvrir. Suppr pour l'archiver. {0} affiche ou masque vos notes depuis n'importe où."),
        ("Notes", "Note", "便签", "Notas", "メモ", "메모", "Notes"),
        ("No notes match your search.", "Không có note nào khớp với tìm kiếm.", "没有符合搜索的便签。", "Ninguna nota coincide con tu búsqueda.", "検索に一致するメモはありません。", "검색과 일치하는 메모가 없습니다.", "Aucune note ne correspond à votre recherche."),
        ("Archived notes show up here.\nArchive a note with Ctrl+D.", "Note đã lưu trữ sẽ hiện ở đây.\nLưu trữ một note bằng Ctrl+D.", "归档的便签会显示在这里。\n按 Ctrl+D 归档便签。", "Las notas archivadas aparecen aquí.\nArchiva una nota con Ctrl+D.", "アーカイブしたメモはここに表示されます。\nCtrl+D でメモをアーカイブ。", "보관한 메모가 여기에 나타납니다.\nCtrl+D 로 메모를 보관하세요.", "Les notes archivées apparaissent ici.\nArchivez une note avec Ctrl+D."),
        ("No notes yet.\nPress {0} anywhere to create one.", "Chưa có note nào.\nNhấn {0} ở bất kỳ đâu để tạo note.", "还没有便签。\n在任何地方按 {0} 新建。", "Aún no hay notas.\nPulsa {0} en cualquier lugar para crear una.", "まだメモがありません。\nどこでも {0} で作成できます。", "아직 메모가 없습니다.\n어디서나 {0} 를 눌러 만드세요.", "Pas encore de notes.\nAppuyez sur {0} n'importe où pour en créer une."),
        ("Delete \"{0}\" permanently?\nThis can't be undone.", "Xóa vĩnh viễn \"{0}\"?\nKhông thể hoàn tác.", "永久删除 \"{0}\"？\n此操作无法撤销。", "¿Eliminar \"{0}\" para siempre?\nNo se puede deshacer.", "「{0}」を完全に削除しますか？\n元に戻せません。", "\"{0}\" 을(를) 영구 삭제할까요?\n되돌릴 수 없습니다.", "Supprimer « {0} » définitivement ?\nAction irréversible."),
        ("(empty note)", "(note trống)", "（空便签）", "(nota vacía)", "（空のメモ）", "(빈 메모)", "(note vide)"),
        ("closed", "đã đóng", "已关闭", "cerrada", "閉じています", "닫힘", "fermée"),
        ("locked", "đã khóa", "已锁定", "bloqueada", "ロック中", "잠김", "verrouillée"),
        ("[image]", "[ảnh]", "[图片]", "[imagen]", "[画像]", "[이미지]", "[image]"),

        // ---------- choose notes to keep ----------
        ("Choose notes", "Chọn note", "选择便签", "Elegir notas", "メモを選ぶ", "메모 고르기", "Choisir des notes"),
        ("Keep these notes on screen", "Giữ những note này trên màn hình", "将这些便签留在屏幕上", "Mantener estas notas en pantalla", "これらのメモを画面に残す", "이 메모들을 화면에 남기기", "Garder ces notes à l'écran"),
        ("Space or click selects a note. Enter keeps the selected ones and hides the rest. Esc cancels.", "Space hoặc bấm chuột để chọn note. Enter giữ các note đã chọn và ẩn phần còn lại. Esc để hủy.", "按空格或点击选择便签。Enter 保留所选并隐藏其余。Esc 取消。", "Espacio o clic selecciona una nota. Intro mantiene las elegidas y oculta el resto. Esc cancela.", "スペースかクリックでメモを選択。Enter で選んだメモを残し、他を隠します。Esc で取り消し。", "스페이스나 클릭으로 메모를 고릅니다. Enter 는 고른 메모만 남기고 나머지를 숨깁니다. Esc 는 취소.", "Espace ou clic sélectionne une note. Entrée garde la sélection et masque le reste. Échap annule."),
        ("Select at least one note.", "Hãy chọn ít nhất một note.", "请至少选择一个便签。", "Elige al menos una nota.", "少なくとも 1 つ選んでください。", "메모를 하나 이상 고르세요.", "Sélectionnez au moins une note."),

        // ---------- a note ----------
        ("Locked. Ctrl+L to unlock.", "Đã khóa. Ctrl+L để mở khóa.", "已锁定。按 Ctrl+L 解锁。", "Bloqueada. Ctrl+L para desbloquear.", "ロック中。Ctrl+L で解除。", "잠김. Ctrl+L 로 잠금 해제.", "Verrouillée. Ctrl+L pour déverrouiller."),
        ("Note options", "Tùy chọn note", "便签选项", "Opciones de la nota", "メモのオプション", "메모 옵션", "Options de la note"),
        ("Close note (Ctrl+W)", "Đóng note (Ctrl+W)", "关闭便签 (Ctrl+W)", "Cerrar nota (Ctrl+W)", "メモを閉じる (Ctrl+W)", "메모 닫기 (Ctrl+W)", "Fermer la note (Ctrl+W)"),
        ("Important. Click to remove the star (Ctrl+Shift+S)", "Quan trọng. Bấm để bỏ sao (Ctrl+Shift+S)", "重要。点击取消星标 (Ctrl+Shift+S)", "Importante. Haz clic para quitar la estrella (Ctrl+Shift+S)", "重要。クリックで星を外す (Ctrl+Shift+S)", "중요. 클릭하면 별표 해제 (Ctrl+Shift+S)", "Importante. Cliquez pour retirer l'étoile (Ctrl+Shift+S)"),
        ("Mark as important (Ctrl+Shift+S)", "Đánh dấu quan trọng (Ctrl+Shift+S)", "标记为重要 (Ctrl+Shift+S)", "Marcar como importante (Ctrl+Shift+S)", "重要としてマーク (Ctrl+Shift+S)", "중요 표시 (Ctrl+Shift+S)", "Marquer comme importante (Ctrl+Shift+S)"),
        ("Remove from your saved notes (Ctrl+T)", "Bỏ khỏi các note đã chọn (Ctrl+T)", "从保存的便签中移除 (Ctrl+T)", "Quitar de tus notas guardadas (Ctrl+T)", "保存したメモから外す (Ctrl+T)", "저장한 메모에서 빼기 (Ctrl+T)", "Retirer de vos notes enregistrées (Ctrl+T)"),
        ("Add to your saved notes (Ctrl+T)", "Thêm vào các note đã chọn (Ctrl+T)", "添加到保存的便签 (Ctrl+T)", "Añadir a tus notas guardadas (Ctrl+T)", "保存したメモに追加 (Ctrl+T)", "저장한 메모에 추가 (Ctrl+T)", "Ajouter à vos notes enregistrées (Ctrl+T)"),
        ("Untitled", "Chưa có tên", "无标题", "Sin título", "無題", "제목 없음", "Sans titre"),
        ("Click the title to rename, drag to move", "Bấm tiêu đề để đổi tên, kéo để di chuyển", "点击标题改名，拖动移动", "Haz clic en el título para renombrar, arrastra para mover", "タイトルをクリックで名前変更、ドラッグで移動", "제목을 클릭해 이름 바꾸기, 끌어서 옮기기", "Cliquez sur le titre pour renommer, faites glisser pour déplacer"),
        ("Click to select, double-click to open", "Bấm để chọn, nhấp đúp để mở", "单击选择，双击打开", "Clic para seleccionar, doble clic para abrir", "クリックで選択、ダブルクリックで開く", "클릭해 선택, 두 번 클릭해 열기", "Clic pour sélectionner, double-clic pour ouvrir"),
        ("No spelling suggestions", "Không có gợi ý chính tả", "没有拼写建议", "No hay sugerencias", "スペルの候補はありません", "맞춤법 제안 없음", "Aucune suggestion"),
        ("Ignore", "Bỏ qua", "忽略", "Ignorar", "無視", "무시", "Ignorer"),
        ("Check this line with AI", "Kiểm tra dòng này bằng AI", "用 AI 检查这一行", "Revisar esta línea con IA", "この行を AI でチェック", "이 줄을 AI로 확인", "Vérifier cette ligne avec l'IA"),
        ("Check with AI", "Kiểm tra bằng AI", "AI 检查", "Revisar con IA", "AI でチェック", "AI로 확인", "Vérifier avec l'IA"),
        ("Stop reading", "Dừng đọc", "停止朗读", "Dejar de leer", "読み上げを止める", "읽기 중지", "Arrêter la lecture"),
        ("Bold", "Đậm", "粗体", "Negrita", "太字", "굵게", "Gras"),
        ("Italic", "Nghiêng", "斜体", "Cursiva", "斜体", "기울임", "Italique"),
        ("Cut", "Cắt", "剪切", "Cortar", "切り取り", "잘라내기", "Couper"),
        ("Copy", "Sao chép", "复制", "Copiar", "コピー", "복사", "Copier"),
        ("Paste", "Dán", "粘贴", "Pegar", "貼り付け", "붙여넣기", "Coller"),
        ("Spell check", "Kiểm tra chính tả", "拼写检查", "Revisión ortográfica", "スペルチェック", "맞춤법 검사", "Correcteur orthographique"),
        ("Glass tint {0}", "Độ đậm kính {0}", "玻璃色调 {0}", "Tinte del cristal {0}", "ガラスの濃さ {0}", "유리 농도 {0}", "Teinte du verre {0}"),
        ("Ctrl+[ and Ctrl+]", "Ctrl+[ và Ctrl+]", "Ctrl+[ 和 Ctrl+]", "Ctrl+[ y Ctrl+]", "Ctrl+[ と Ctrl+]", "Ctrl+[ 와 Ctrl+]", "Ctrl+[ et Ctrl+]"),
        ("Text size {0}", "Cỡ chữ {0}", "文字大小 {0}", "Tamaño del texto {0}", "文字サイズ {0}", "글자 크기 {0}", "Taille du texte {0}"),
        ("Ctrl+wheel, or Ctrl+= / Ctrl+- (Ctrl+0 resets)", "Ctrl + lăn chuột, hoặc Ctrl+= / Ctrl+- (Ctrl+0 về mặc định)", "Ctrl+滚轮，或 Ctrl+= / Ctrl+-（Ctrl+0 恢复）", "Ctrl+rueda, o Ctrl+= / Ctrl+- (Ctrl+0 restablece)", "Ctrl+ホイール、または Ctrl+= / Ctrl+-（Ctrl+0 で戻す）", "Ctrl+휠, 또는 Ctrl+= / Ctrl+- (Ctrl+0 은 초기화)", "Ctrl+molette, ou Ctrl+= / Ctrl+- (Ctrl+0 réinitialise)"),
        ("Rename", "Đổi tên", "重命名", "Renombrar", "名前を変更", "이름 바꾸기", "Renommer"),
        ("Font", "Phông chữ", "字体", "Fuente", "フォント", "글꼴", "Police"),
        ("Mono (Consolas)", "Đơn cách (Consolas)", "等宽 (Consolas)", "Monoespaciada (Consolas)", "等幅 (Consolas)", "고정폭 (Consolas)", "Chasse fixe (Consolas)"),
        ("Sans", "Không chân", "无衬线", "Sans serif", "ゴシック", "고딕", "Sans empattement"),
        ("Handwriting", "Viết tay", "手写", "Manuscrita", "手書き", "손글씨", "Manuscrite"),
        ("In saved notes (Ctrl+Alt+P list)", "Trong các note đã chọn (danh sách Ctrl+Alt+P)", "在保存的便签中（Ctrl+Alt+P 列表）", "En las notas guardadas (lista de Ctrl+Alt+P)", "保存したメモに含める（Ctrl+Alt+P の一覧）", "저장한 메모에 포함 (Ctrl+Alt+P 목록)", "Dans les notes enregistrées (liste Ctrl+Alt+P)"),
        ("Lock editing", "Khóa chỉnh sửa", "锁定编辑", "Bloquear edición", "編集をロック", "편집 잠금", "Verrouiller la modification"),
        ("Checklist item", "Dòng checklist", "清单项", "Elemento de lista", "チェック項目", "체크리스트 항목", "Élément de liste"),
        ("Close note", "Đóng note", "关闭便签", "Cerrar nota", "メモを閉じる", "메모 닫기", "Fermer la note"),
        ("Archive note", "Lưu trữ note", "归档便签", "Archivar nota", "メモをアーカイブ", "메모 보관", "Archiver la note"),
        ("All notes…", "Tất cả note…", "所有便签…", "Todas las notas…", "すべてのメモ…", "모든 메모…", "Toutes les notes…"),
        ("Canary", "Vàng hoàng yến", "金丝雀黄", "Canario", "カナリア", "카나리아", "Canari"),
        ("Apricot", "Mơ", "杏色", "Albaricoque", "アプリコット", "살구", "Abricot"),
        ("Rose", "Hồng", "玫瑰", "Rosa", "ローズ", "장미", "Rose"),
        ("Lilac", "Tử đinh hương", "丁香紫", "Lila", "ライラック", "라일락", "Lilas"),
        ("Sky", "Xanh trời", "天蓝", "Cielo", "スカイ", "하늘", "Ciel"),
        ("Mint", "Bạc hà", "薄荷绿", "Menta", "ミント", "민트", "Menthe"),
        ("Index card", "Giấy thẻ", "索引卡", "Ficha", "情報カード", "인덱스 카드", "Fiche bristol"),
        ("Graphite", "Than chì", "石墨", "Grafito", "グラファイト", "흑연", "Graphite"),
        ("Couldn't add the picture: {0}", "Không thêm được ảnh: {0}", "无法添加图片：{0}", "No se pudo añadir la imagen: {0}", "画像を追加できませんでした: {0}", "그림을 추가하지 못했습니다: {0}", "Impossible d'ajouter l'image : {0}"),
        ("[picture not found]", "[không tìm thấy ảnh]", "[找不到图片]", "[imagen no encontrada]", "[画像が見つかりません]", "[그림을 찾을 수 없음]", "[image introuvable]"),
        ("[picture can't be shown]", "[không hiển thị được ảnh]", "[无法显示图片]", "[no se puede mostrar la imagen]", "[画像を表示できません]", "[그림을 표시할 수 없음]", "[impossible d'afficher l'image]"),
    ];

    /// <summary>Language code → (English → translation).</summary>
    public static readonly IReadOnlyDictionary<string, Dictionary<string, string>> Table = Build();

    /// <summary>Every English phrase that has a row.</summary>
    public static IEnumerable<string> English => Rows.Select(r => r.En);

    static Dictionary<string, Dictionary<string, string>> Build()
    {
        var table = new Dictionary<string, Dictionary<string, string>>();
        foreach (var row in Rows)
        {
            foreach (var (code, text) in new[] { ("vi", row.Vi), ("zh", row.Zh), ("es", row.Es), ("ja", row.Ja), ("ko", row.Ko), ("fr", row.Fr) })
            {
                if (!table.TryGetValue(code, out var column))
                    table[code] = column = [];
                column[row.En] = text;
            }
        }
        return table;
    }
}
