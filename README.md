# Open Note

Lightweight sticky notes for Windows 10 and 11. Each note is its own small glass window on the desktop, like paper notes on a desk.
Notes save as you type and come back exactly where you left them.

## Install

Download from the [latest release](https://github.com/nhaajtdajt/open-note/releases/latest). Nothing else needs to be installed: the .NET runtime is included.

- **`OpenNote-Setup-<version>-x64.exe`**: installs for your Windows account only (no administrator prompt) and starts Open Note.
- **`OpenNote-<version>-win-x64.zip`**: portable. Unzip anywhere and run `OpenNote.exe`.

Requires Windows 10 version 1809 or later (64-bit). The downloads aren't code-signed yet, so Windows SmartScreen may warn on first run: click **More info → Run anyway**.

**First run:** Open Note lives in the notification area (tray) and shows a welcome note. It turns on **Start with Windows** by itself, so it is there after every restart; turn that off from the tray menu. Press `Ctrl+Alt+N` for a new note.

## Check with AI: add a free Gemini key

Press **`Ctrl+Alt+K`** (or tray menu → **Gemini API keys…**, or just use Check with AI the first time). The window explains how to:

1. Open [Google AI Studio](https://aistudio.google.com/apikey) and sign in with a Google account.
2. Click **Create API key**, then copy it.
3. Paste it (one key per line) and click **Check and save**. Every key is tested with Google in about a second: ✔ works, ⚠ out of quota for today or no connection (kept), ✖ rejected (left out).

**Tip:** the free quota belongs to a Google Cloud project, so keys in the same project share it. Create keys in several projects ("Create API key in new project") and paste them all: Open Note uses them in turn and skips one that has run out.

On the free tier Google may use the text you check to improve its products, so don't check private text. See [PRIVACY.md](PRIVACY.md).

## Using it

| Where | Shortcut | What it does |
|---|---|---|
| Anywhere | `Ctrl+Alt+N` | New note, placed by the mouse pointer |
| Anywhere | `Ctrl+Alt+H` | **Hide all notes / show all notes.** Hides every note when all are showing; shows them all otherwise, including the rest when only your saved selection is showing. |
| Anywhere | `Ctrl+Alt+P` | **Choose notes to keep.** Shows all notes and opens a picker: arrows move, `Space` or a click selects, `Enter` keeps the selected notes and hides the rest, `Esc` cancels. The choice is saved as the one saved selection. |
| Anywhere | `Ctrl+Alt+J` | **Show the saved selection** (only those notes). Press it again to hide everything. With no saved selection yet, it opens the picker. |
| Anywhere | `Ctrl+Alt+M` | All notes: search, reopen, archive, restore, delete permanently. Press it again to close the list. |
| Anywhere | `Ctrl+Alt+K` | Add or check your Gemini API keys for Check with AI |
| In a note | Click the header, or `F2` | Rename the note (Enter saves, Esc cancels) |
| In a note | `/-` | Bullet `•` (type a slash, then a dash) |
| In a note | `/>` | Checkbox `☐`. Type `//-` or `//>` to keep the characters as typed. |
| In a note | `Ctrl+B` / `Ctrl+I` | **Bold** / *italic* |
| In a note | `Ctrl+G`, or right-click → **Check with AI** | Send the selected text (or the current line) to Gemini. You get two versions: one as close to your wording as possible, and one that reads more naturally. Pick one to replace the text. |
| In a note | `Ctrl+R`, or right-click → **Read aloud** | Read the selected text (or the current line) aloud with the offline Windows voices. Press again to stop. |
| In a note | `Ctrl+N` | New note next to this one |
| In a note | `Ctrl+W`, or the × button | **Close** the note. It stays in the notes list (not in the Archive); reopen it with a double-click there. Empty notes are discarded. |
| In a note | `Ctrl+D` | **Archive** the note (restorable from All notes → Archive) |
| In a note | `Ctrl+T`, or the 📌 button | Add this note to your **saved notes** (the selection of `Ctrl+Alt+P` / `Ctrl+Alt+J`), or take it out |
| In a note | `Ctrl+L` | Lock editing |
| In a note | `Ctrl+Enter` | Make the line a checklist item, or tick/untick it. Clicking a box ticks it too. Enter on a list line starts the next item; Enter on an empty item ends the list. |
| In a note | `Ctrl+1` … `Ctrl+8` | Color |
| In a note | `Ctrl+[` / `Ctrl+]` | Clearer / more solid glass (the text always stays solid) |
| In a note | `Ctrl+wheel`, `Ctrl+=` / `Ctrl+-` / `Ctrl+0` | Text size |

- **Move a note:** drag its header.
- **Rename a note:** click the header without dragging (it starts after a moment, in case you meant to double-click).
- **Shrink and restore a note:** double-click the header. A note you made bigger shrinks to the normal size and remembers how big it was; double-click again to get that size back. If you resize it by hand while it is shrunk, the new size becomes the one remembered. Every note's size and position are saved and restored on restart.
- **Resize a note:** drag any edge.
- **More options:** right-click the header or use the `⋯` button for color, glass tint, font (Mono/Consolas, Sans, Handwriting), lock and more.
- **Tray icon:** a left click brings all notes to the front. The right-click menu has the rest.
- **Pictures:** paste with `Ctrl+V` (a Win+Shift+S screenshot, an image copied from a browser, or image files copied in Explorer), or drag image files onto a note.
  - **Sizing:** a picture fills the note's width and keeps its shape, but never grows past its own pixel size, so it is never blown up into blur.
  - **Sharpness:** it is decoded at the size it's shown, and again more sharply in the background when you widen the note.
  - **Phone photos** are turned upright using their EXIF orientation.
  - **Opening and copying:** double-click a picture to open it full size. Copying it puts the original file on the clipboard.
  - **Storage:** pictures are stored in `%LOCALAPPDATA%\OpenNote\images`. The note keeps a line like `![image](images/….png)` where the picture sits. Files no note uses any more are cleaned up at startup.
  - **Text wins:** clipboard content that also carries text (Word, Excel) pastes as text.
- **Spell check:** Windows' own spell checker (the one Notepad uses) underlines misspelled words in the note you're working in.
  - Right-click a word for suggestions.
  - It's English-only on most PCs, so it switches off by itself for Vietnamese notes.
  - Override it per note with right-click → **Spell check**.

### Read aloud (offline)

- Uses the voices installed in Windows (SAPI), so it works offline and sends nothing anywhere. List markers and pictures are skipped, and at most 1500 characters are read at a time.
- English text uses an English voice (David, Zira or Mark on most PCs). Vietnamese text needs a Vietnamese voice: add one in Windows Settings → Time & language → Speech; without one, a message says so instead of reading it wrongly.
- The speech engine is created only when you start reading. After the first use Windows keeps its speech library loaded, which adds about 12 MB until the app is restarted.

### AI check (Gemini, plus optional OpenAI-compatible services)

- **Vietnamese text is translated, not checked:** if the selection is only Vietnamese, the popup offers two English versions: one close to your Vietnamese and one the way a native speaker would say it, with its Vietnamese meaning (so you can confirm it says what you meant) and a short note on translation choices. **Use this** replaces the Vietnamese with the English.
- **English (or Vietnamese plus English) is checked:** select text (or leave the caret on a line) and press `Ctrl+G`. The popup shows:
  - **Your sentence means:** your original text translated literally into Vietnamese, so you can see what it really says, with a verdict (Đúng và tự nhiên / Chưa tự nhiên / Có lỗi / Sai nghĩa) and a short Vietnamese comment on whether it is right and natural.
  - **Closest to your wording** and **More natural**, each with a **Use this** button; the natural one also shows its Vietnamese meaning.
  - If a note holds a Vietnamese sentence and your English version of it, the AI treats the Vietnamese as what you mean and tells you if the English says something else ("Nếu nghĩa tiếng Việt là … thì nên viết …").
- **Providers, in order:** OpenCode Zen's free models (`big-pickle`, then `deepseek-v4.1-flash-free`, `glm-5.3-flash-free`), then your `aiServices`, then Gemini. A provider without a key is skipped.
- **Other free services (OpenAI-compatible):** add them in `settings.json` under `"aiServices"`; they are tried after OpenCode and before Gemini. Example with OpenRouter (free key at openrouter.ai, no card, 19 free models):
  ```json
  "aiServices": [
    { "name": "OpenRouter", "baseUrl": "https://openrouter.ai/api/v1", "apiKey": "sk-or-...", "models": ["nvidia/nemotron-3-super-120b-a12b:free", "google/gemma-4-31b-it:free"] }
  ]
  ```
  Groq (`https://api.groq.com/openai/v1`), Cerebras (`https://api.cerebras.ai/v1`) and Mistral (`https://api.mistral.ai/v1`) work the same way. Free model names change often: check each service's model list.
- **OpenCode key (pay-as-you-go account):** create an account at opencode.ai, make an API key and put it in `settings.json` as `"openCodeApiKey": "…"` (or set `OPENCODE_API_KEY`). Without a key OpenCode refuses requests (HTTP 403), so Gemini is used instead.
- **Gemini keys:** add them with `Ctrl+Alt+K` (see above). They are stored in `settings.json` as `"geminiApiKeys"`; the `GEMINI_API_KEY`, `GEMINI_API_KEY_2`, … environment variables work too. Keys are used in turn, and one that is out of quota or rejected is skipped for that request.
- **Models:** `"openCodeModels"` and `"geminiModels"` list the models to try in order. When one is overloaded or its answer can't be read, the next is used.
- **Privacy:** nothing is sent unless you ask. Only the selected text (or current line) goes to the AI service, and keys travel in a request header, not in the URL.
- **Formatting:** the replaced text comes back plain, so bold or italic inside that selection is not kept.

## How it behaves on Windows (and why)

- **Alt+Tab:** notes are *tool windows*, so they never show up in Alt+Tab or on the taskbar. Use `Ctrl+Alt+H` or the tray icon to bring them back. The All notes window is a normal window and does appear in Alt+Tab while it's open.
- **Focus and stacking:** by default every note **stays above your other windows**, so clicking into another app doesn't hide it. Turn this off with tray → **Keep notes on top** (or `"keepNotesOnTop": false` in `settings.json`) to let other apps cover notes.
- **Deleting pictures:** WPF can't undo a picture deletion (it restores an empty box), so pictures are removed by hand and the note's Ctrl+Z history restarts at that point.
- **Glass look:** each note is tinted glass with a specular sheen, a bright inner rim and 14px rounded corners. Only the tint fades; the text stays fully solid. The tint never goes below 20%, because a fully transparent pixel lets clicks fall through to whatever is behind it.
  - **No blur, on purpose.** Windows' own acrylic backdrop does blur, but in light mode it lays a heavy grey wash over the note colors, and the undocumented blur APIs rendered white in testing. So the glass is see-through, not frosted. On a busy background, raise the tint with `Ctrl+]`.
- **Multiple monitors and scaling:** positions are stored in physical pixels, and each note is scaled for the monitor it's on. If a note's monitor is unplugged, or the resolution drops, the note is moved back onto a screen at startup and whenever you show all notes.
- **Saving:**
  - Notes save 0.5 s after each change, on exit, and when Windows signs out or shuts down.
  - Each save writes a temporary file and then swaps it in, keeping the previous version as `notes.json.bak`.
  - If `notes.json` is ever unreadable, it is set aside as `notes.corrupt-*.json` and the backup is loaded instead. A file that is only locked for a moment (antivirus, OneDrive) is retried, not treated as damaged.
  - `settings.json` is never overwritten while it has a mistake in it, and changes from the tray menu keep your hand edits. Comments (`//`) and trailing commas are allowed.
- **Text format:** the editor is a RichTextBox for bold and italic only, but notes are stored as plain text with `**bold**` and `*italic*` markers, so `notes.json` stays readable and searchable.
  - Pasting keeps bold and italic and drops all other formatting.
  - Underline, alignment and Word-style lists are blocked, because they couldn't be saved.
- **Footprint:** with 6 notes, about 46–50 MB private memory, and 2–3 MB in Task Manager's Memory column once idle. No CPU is used while idle.
  - **Software rendering:** for small transparent windows this halves memory compared with GPU rendering.
  - **No WinForms:** the tray icon and monitor list use Win32 directly.
  - **ReadyToRun, no tiered JIT:** published builds are precompiled and run as they are, with no background re-compilation: about 10 MB less memory and half the startup CPU.
  - **Idle trimming:** a few seconds after you switch away, idle memory is handed back to Windows.
- **Show Desktop (Win+D)** hides notes like every other window. Press `Ctrl+Alt+H` to bring them back.

## Build from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/OpenNote
```

```bash
dotnet test
```

Build everything that gets released (self-contained zip, Microsoft Store MSIX, and the installer if [Inno Setup 6](https://jrsoftware.org/isinfo.php) is installed) into `publish\`:

```bash
powershell -ExecutionPolicy Bypass -File build/package.ps1
```

Pushing a tag such as `v1.0.0` makes GitHub Actions build these and attach them to a GitHub Release. Publishing to the Microsoft Store, and the legal checklist, are described in [docs/STORE-SUBMISSION.md](docs/STORE-SUBMISSION.md).

## Project layout

```
src/OpenNote/
  Core/        UI-free logic: Note model, NoteStore (JSON + safe writes), settings,
               ScreenMath (keep notes on screen), Checklist, QuickInsert, InlineMarkup
               (bold/italic markers), AiCorrector (AI check, key rotation), GeminiKeyChecker,
               LanguageHint, HotkeyGesture, NoteSearch, Palette
  Native/      Win32 interop: tool-window style, z-order, bounds, hotkeys, monitor work areas,
               tray icon (Shell_NotifyIcon), memory trimming, MSIX package detection
  Services/    NoteManager (single owner of notes and windows, debounced saving),
               HotkeyService, TrayService, StartupRegistration, SpeechService
  Views/       NoteWindow (one note; split into .Pictures, .Ai, .Menus, .Size and .Speech parts),
               RichText (editor <-> stored text), AiPanel (AI suggestions), ApiKeysWindow,
               NotesListWindow (search/manage/archive), PickNotesWindow
tests/OpenNote.Tests/   unit tests
build/package.ps1       release build: zip, MSIX, installer
packaging/              MSIX manifest and logos, Inno Setup script, Store logo
tools/                  icon and logo generators
```

The app uses no third-party packages: only WPF and Win32.

## License

[MIT](LICENSE). See also [PRIVACY.md](PRIVACY.md) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Data

Data is stored in `%LOCALAPPDATA%\OpenNote`:
- `notes.json`: your notes
- `notes.json.bak`: the previous save
- `settings.json`: settings and shortcuts
- `images\`: pasted pictures
- `error.log`: written only if something goes wrong (restarted once it passes 1 MB)

The Microsoft Store version keeps the same files in Windows' private folder for the app; tray menu → **Open data folder** opens it. Uninstalling keeps your notes; delete the folder to remove them.

For testing, run with `--data <folder>` to use a separate folder. Such a copy leaves the **Start with Windows** entry alone.

`OpenNote.exe --exit` asks the running copy to save and quit, and waits until it has (the installer uses it before replacing files).
