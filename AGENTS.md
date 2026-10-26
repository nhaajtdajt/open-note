# Rules for AI agents and contributors

Read this whole file before you change anything. It applies to every AI coding agent (Claude Code, Codex, Cursor, Copilot…) and every person working in this repository.

## Who works where

| Who | Works on | Folder |
|---|---|---|
| The owner, **@nhaajtdajt** | The Windows app (the reference), releases, the website, CI, this file | everything |
| Everyone else, including the macOS contributor | **The macOS app only** | `mac/` |

If you are running on macOS, or the person you work for is not @nhaajtdajt, **every rule in "The macOS app" applies to you**. When in doubt, it applies.

## The macOS app

### The goal, in one sentence

Build Open Note for macOS so that it **behaves exactly like the Windows app in `src/OpenNote/`**: the same features, the same shortcuts, the same texts in the same 7 languages, the same defaults and limits, the same data files. **Nothing more and nothing less.**

### Hard rules (never break these)

1. **Only touch `mac/`.** Do not create, edit, move, rename, delete or reformat anything outside `mac/`: not `src/`, `tests/`, `build/`, `packaging/`, `tools/`, `website/`, `docs/`, `.github/`, `README.md`, `PRIVACY.md`, `LICENSE`, `AGENTS.md`, `CLAUDE.md` or the root `.gitignore`. (Put Mac-only ignores in `mac/.gitignore`.) A pull request that changes a file outside `mac/` fails the **mac-only** check and will not be merged. If you believe something outside `mac/` must change, write it in the pull request description for the owner instead.
2. **No new features, no missing features, no redesign, no "improvements".** If the Windows app does it, port it. If the Windows app doesn't do it, don't add it. Not a setting, not a menu item, not a shortcut, not an animation, not a new color.
3. **The Windows code is the specification.** In order of authority:
   1. the code in `src/OpenNote/` (behavior, texts, default values, limits, timings);
   2. the tests in `tests/OpenNote.Tests/` (port them, they define the expected results);
   3. `README.md`.

   When `README.md` and the code disagree, the code wins. Read the Windows file before you write its Mac version; don't guess from the feature's name.
4. **When "identical" is impossible on macOS**, use the closest native equivalent, write it down in `mac/DIFFERENCES.md` (Windows behavior → macOS behavior → why), and point it out in the pull request. A difference that is not in `mac/DIFFERENCES.md` is a bug. The known, accepted ones are listed under "Windows → macOS" below.
5. **Found a bug in the Windows app?** Copy its behavior anyway and report it in the pull request. Don't fix it only on the Mac, or the two apps drift apart.
6. **No third-party packages.** The Windows app uses none. Use Apple's frameworks only. Ask the owner before adding anything.
7. **No releases.** Don't create `v*` tags, GitHub Releases, code signing, notarization, App Store or TestFlight submissions, and don't change the website. The owner releases. (The repository blocks tags and pushes to `main` anyway.)
8. **No secrets or personal data in git.** Never commit API keys, `settings.json`, `notes.json`, pictures from your notes, or `error.log`. Never print a key in a log, a test, or a message.
9. **Ask instead of deciding** when the Windows code doesn't answer the question. Open the pull request as a draft with the question in it.

### Keep the two apps in step

- The Mac app's version is the Windows version it matches (now **1.1.0**, from `<Version>` in `src/OpenNote/OpenNote.csproj`).
- When `main` gets Windows changes in `src/OpenNote/`, port exactly those changes in a follow-up pull request. `git log --oneline -- src/OpenNote` shows what changed since the version you match.

### Git workflow

1. Update first: `git switch main && git pull`.
2. Branch: `git switch -c mac/<short-topic>` (for example `mac/note-window`, `mac/hotkeys`).
3. Commit small steps. First line: what changed, in the imperative ("Port the checklist toggle"). Body: what and why, and which Windows files it ports.
4. Push the branch and open a pull request into `main`. One topic per pull request.
5. Nobody can push to `main` directly or merge without the owner's approval. Checks must pass: **build** (the Windows app still builds and its tests pass) and **mac-only**.
6. Never force-push to `main`, never rewrite someone else's commits.

Every pull request description lists:
- the Windows files it ports (`src/OpenNote/...`) and the tests it ports;
- what you checked by hand, and how;
- any new line in `mac/DIFFERENCES.md`.

### Technology

- **Swift** with **AppKit** for the windows and the menu bar. SwiftUI is fine for simple windows (the keys window, the notes list) if it looks the same as the Windows version.
- Minimum macOS **13 Ventura**. Apple silicon and Intel.
- An Xcode project at `mac/OpenNote.xcodeproj`, app named **Open Note**, bundle identifier `io.github.nhaajtdajt.opennote`. Keep `mac/` buildable with `xcodebuild` from the command line.
- Unit tests with **XCTest** in `mac/OpenNoteTests/`. Port every test file in `tests/OpenNote.Tests/` that tests `Core/` logic, with the same cases and the same expected values.
- Mirror the Windows layout so files can be compared side by side:

  ```
  mac/OpenNote/Core/       one Swift file per src/OpenNote/Core/*.cs, same name (Note.swift, NoteStore.swift, Translations.swift …)
  mac/OpenNote/Services/   NoteManager, HotkeyService, MenuBarService (= TrayService), SpeechService, LoginItem (= StartupRegistration)
  mac/OpenNote/Views/      NoteWindow (+ the same parts: Ai, Menus, Motion, Pictures, Size, Speech), RichText, AiPanel,
                           ApiKeysWindow, HelpWindow, LanguagePickWindow, NotesListWindow, PickNotesWindow, ArchiveBinWindow, PictureView
  mac/OpenNote/Platform/   macOS-only glue (the counterpart of src/OpenNote/Native/)
  mac/OpenNoteTests/
  mac/DIFFERENCES.md
  ```

### Things that must be the same (check each one)

- **Features and behavior:** everything in the "Using it" table and the bullet lists of `README.md`, as the code implements them: hide/show all, choose notes to keep and the saved selection, all notes with search/archive/restore/delete, fade while other apps are active, how-to card, quit, rename by clicking the header or F2, `/-` bullets and `/>` checkboxes (and `//-`, `//>`), bold and italic, checklist ticking with strike-through to the end of the line, colors, glass tint, text size, lock, star (important), saved-notes pin, close vs archive, shrink/restore on double-click, resize from every edge, throw, shake, the archive bin, pictures, read aloud, spell check, Check with AI, the first-run language picker, the welcome note, the how-to card opening on first run.
- **Texts:** port `Core/Translations.cs` row by row: the same English keys and the same 7 languages (en, vi, zh, es, ja, ko, fr), the same `{0}` placeholders, and port `TranslationTests` so every text shown on screen must be in the table. The only allowed changes are platform words ("Windows" → "macOS", "Start with Windows" → "Open at login", "tray" → "menu bar", Ctrl/Alt key names), changed in all 7 languages and listed in `mac/DIFFERENCES.md`.
- **Defaults and limits:** colors (`Palette`), default opacity 0.9 and its 20% floor, font size 14, the AI timeout, the 30 words of AI context, the 1500 characters read aloud, the 0.5 s save delay, picture sizing rules, the hotkey defaults in `AppSettings`.
- **AI check:** port `AiCorrector`, `AiContext`, `GeminiKeyChecker` and `LanguageHint` exactly: the same prompt text, the same provider order (OpenCode, then `aiServices`, then Gemini), the same models and fallbacks, key rotation, the same JSON answer format and the same popup (`AiPanel`, `AiPanelText`). Keys go in a request header, never in the URL.
- **Data:** the same files with the same JSON schema, so a `notes.json` or `settings.json` copied from Windows opens unchanged:
  - camelCase property names, enums as strings, the same property names and defaults as `Core/Note.cs` and `Core/AppSettings.cs`;
  - the same safe writing as `Core/NoteStore.cs`: temp file then swap, `notes.json.bak`, `notes.corrupt-*.json` when unreadable, `settings.json` never overwritten while it has a mistake, `//` comments and trailing commas allowed;
  - pictures in `images/`, referenced in the note text as `![image](images/….png)`, unused ones removed at startup.
- **Command line:** `--data <folder>` (separate data folder; leave the login item alone) and `--exit` (ask the running copy to save and quit). One running copy per data folder.

### Windows → macOS (accepted equivalents)

| Windows | macOS |
|---|---|
| Data in `%LOCALAPPDATA%\OpenNote` | `~/Library/Application Support/OpenNote` (same file names) |
| Global shortcuts `Ctrl+Alt+<key>` (`RegisterHotKey`) | `Control+Option+<key>` with the same keys (Carbon `RegisterEventHotKey`, needs no Accessibility permission). `settings.json` keeps the Windows strings (`"Ctrl+Alt+N"`); read `Ctrl` as Control, `Alt` as Option, `Shift` as Shift, `Win` as Command. |
| Shortcuts inside a note `Ctrl+<key>` | `Command+<key>` with the same keys (`⌘B`, `⌘I`, `⌘G`, `⌘R`, `⌘N`, `⌘W`, `⌘D`, `⌘T`, `⌘L`, `⌘Return`, `⌘1`…`⌘8`, `⌘[`/`⌘]`, `⌘=`/`⌘-`/`⌘0`, `⌘`+scroll). F2 stays F2. |
| Notes are tool windows: not in Alt+Tab or on the taskbar | Agent app (`LSUIElement`): no Dock icon, not in Command+Tab. Note windows are borderless `NSPanel`s that can take keyboard focus. |
| Keep notes on top | Window level `.floating` while `keepNotesOnTop` is on. |
| Tray icon: left click brings notes to the front, right click opens the menu | Menu bar item (`NSStatusItem`): click brings notes to the front, right-click or Control-click opens the same menu. |
| Start with Windows, turned on by itself on the first run (`autoStartConfigured`) | Open at login with `SMAppService.mainApp`, same first-run rule and same flag. |
| Start menu shortcut | Nothing (the app lives in Applications). Skip `StartMenuShortcut` and `startMenuConfigured`. |
| Read aloud with SAPI voices | `AVSpeechSynthesizer` with the installed voices (offline), the same voice choice per language and the same messages. |
| Windows spell checker for the learning language | `NSSpellChecker` (the text view's own spell checking) for the learning language, with the same per-note override. |
| Fonts: Consolas, Segoe UI, Ink Free | Menlo, the system font, Noteworthy. The font names inside texts such as "Mono (Consolas)" may name the Mac font. |
| Positions in physical pixels, top-left origin | Screen points; convert AppKit's bottom-left origin so `x`/`y` still mean top-left. Port `ScreenMath` so notes are always brought back on screen. |
| Windows' "Show animations" turns off note effects | "Reduce motion" (Accessibility) turns them off. |
| Fade while other apps are active | `NSApplication` did-resign-active / did-become-active, same 10% and the same setting and hotkey. |
| Glass: tinted, see-through, **no blur**, 14 px corners | The same: no `NSVisualEffectView` blur. Keep the 20% tint floor. |
| Win32-only parts: memory trimming, MSIX detection, software rendering, ReadyToRun, Inno Setup, MSIX packaging | Not ported. |

### How to check your work

- `xcodebuild -project mac/OpenNote.xcodeproj -scheme "Open Note" build test` passes.
- Run the app with `--data /tmp/opennote-test` so your own notes are never touched, and go through the README's "Using it" table line by line.
- Open a `notes.json` and `settings.json` written by the Windows app (the owner can send a test copy without keys) and check every note, color, size and setting comes back.
- Compare each screen with the Windows code (the XAML in `src/OpenNote/Views/` gives sizes, colors, margins and texts).

## The Windows app (owner only)

- .NET 10 WPF, no third-party packages. Build and test: `dotnet build`, `dotnet test`. Release build: `build/package.ps1`.
- Every user-visible text goes through `Ui.T("…")` (or `{v:T '…'}` in XAML) and needs a row in `Core/Translations.cs` with all 7 languages.
- A behavior change on Windows means the Mac app must follow: say so in the commit message.
- Never commit API keys or personal data (`settings.json`, `notes.json` are git-ignored).
