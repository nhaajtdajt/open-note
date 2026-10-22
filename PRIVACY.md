# Open Note privacy policy

_Last updated: 22 October 2026_

Open Note is a sticky-notes app for Windows. It is built so that your notes stay on your PC.

## What the developer collects

**Nothing.** Open Note has no account, no telemetry, no analytics, no crash reporting and no ads. The developer never receives your notes, settings, keys or any information about you or your device.

## What is stored on your PC

Everything Open Note keeps is stored in your Windows user profile, in `%LOCALAPPDATA%\OpenNote`:

| File | Contents |
|---|---|
| `notes.json`, `notes.json.bak` | Your notes and their position, size and color |
| `images\` | Pictures you pasted into notes |
| `settings.json` | Your settings, shortcuts and any AI API keys you added (stored as plain text) |
| `error.log` | Technical error details, only if something goes wrong (at most about 1 MB) |

Only you (and administrators of your PC) can read these files. Uninstalling the app keeps them, so you don't lose notes by accident; delete the folder to remove them.

## When something is sent over the internet

Open Note only connects to the internet when you ask it to:

1. **Check with AI** (`Ctrl+G` or right-click → Check with AI). The text you selected (or the current line) is sent to the AI service you set up, using **your own** API key. By default that is the Google Gemini API. Nothing else from your notes is sent.
2. **Checking an API key** (the Gemini API keys window, `Ctrl+Alt+K`). When you click **Check and save**, Open Note asks Google whether each key works by listing the available models. No note text is sent.
3. **Opening a web page** you clicked on (for example Google AI Studio to create a key) opens it in your browser.

Keys are sent in a request header over HTTPS, never in a web address.

### The AI service's own terms

When you use Check with AI, the text is processed by that service under **its** terms and privacy policy, not this one. For the Google Gemini API:

- [Gemini API Additional Terms of Service](https://ai.google.dev/gemini-api/terms) and the [Google Privacy Policy](https://policies.google.com/privacy).
- On the **free** tier, Google may use what you send, and the answers, to improve its products, and people may review them. **Don't check private, confidential or personal text.** On a paid Google Cloud account Google says it doesn't do this.

## Features that stay on your PC

- **Read aloud** uses the voices installed in Windows and works offline.
- **Spell check** uses the Windows spell checker on your PC.
- **Start with Windows** only adds a startup entry on your PC; you can turn it off in the tray menu or in Windows Settings → Apps → Startup.

## Children

Open Note doesn't collect information from anyone, including children. The AI feature depends on a Google account and Google's own terms, which set their own requirements.

## Changes

Changes to this policy are published in this file, in the app's GitHub repository, with a new date at the top.

## Contact

Questions: open an issue at <https://github.com/nhaajtdajt/open-note/issues>.

---

## Tóm tắt tiếng Việt

- Nhà phát triển **không thu thập gì**: không tài khoản, không telemetry, không quảng cáo.
- Note, ảnh, cài đặt và API key chỉ lưu trên máy bạn, trong `%LOCALAPPDATA%\OpenNote`. API key được lưu dạng văn bản thường trong `settings.json`.
- Chỉ khi bạn bấm **Check with AI**, đoạn chữ bạn chọn mới được gửi tới dịch vụ AI (mặc định là Google Gemini) bằng **key của chính bạn**. Khi bấm **Check and save** trong cửa sổ key, app chỉ hỏi Google xem key có dùng được không, không gửi nội dung note.
- Ở gói **miễn phí**, Google có thể dùng nội dung bạn gửi để cải thiện sản phẩm, và có thể có người xem lại. Đừng kiểm tra văn bản riêng tư hay nhạy cảm.
- Đọc to và kiểm tra chính tả chạy hoàn toàn trên máy, không cần mạng.
