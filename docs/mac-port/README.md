# Porting guides for the macOS app

> Mỗi lần bản Windows ra phiên bản mới, owner thêm một file ở đây (`<phiên bản>.md`) liệt kê đúng những gì phía Mac phải làm theo. Người làm bản Mac chỉ cần đọc các file mới hơn phiên bản mình đang khớp, theo thứ tự.

The Windows app in `src/OpenNote/` is the specification (see `AGENTS.md`). Every Windows release gets one guide here that lists exactly what changed and what the Mac app must follow: new features, changed behavior, new texts, new settings and data fields, bug fixes to mirror, tests to port, and open questions for the owner.

| Version | Guide | Windows commit |
|---|---|---|
| 2.0.0 | [2.0.0.md](2.0.0.md) | `5feda5b` (from 1.1.0, `8c3f10c`) |

## How to follow a new release

1. Bring the Mac branch up to date with `main` (it carries the Windows code, the art and these guides):
   `git switch feat/develop-macos-app && git pull && git merge origin/main`
2. Open the guide for each version newer than the one the Mac app matches (the version line in `AGENTS.md` says which), oldest first.
3. Port each item, tick it in your pull request description, and add any new line to `mac/DIFFERENCES.md`.
4. The exact Windows changes are always `git diff <previous commit>..<release commit> -- src/OpenNote tests` (commits in the table above).
5. Screens of the current Windows app: [`docs/screens/`](../screens/README.md).
6. Anything the guide marks **Owner question** is not decided yet: ask in the pull request (a draft is fine), don't pick on your own.
