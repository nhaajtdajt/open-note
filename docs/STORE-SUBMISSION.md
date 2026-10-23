# Phát hành Open Note: thủ tục pháp lý, GitHub và Microsoft Store

> Đây là tổng hợp để tự làm theo, **không phải tư vấn pháp lý**. Nếu sau này bạn bán app, thu tiền hoặc thu thập dữ liệu người dùng, nên hỏi luật sư.

## 1. Pháp lý: đã chuẩn bị gì, bạn còn phải làm gì

| Việc | Trạng thái |
|---|---|
| Giấy phép mã nguồn: MIT ([LICENSE](../LICENSE)) | ✅ Đã có. Ai cũng được dùng, sửa, phân phối lại, miễn giữ dòng bản quyền. |
| Chính sách quyền riêng tư ([PRIVACY.md](../PRIVACY.md)) | ✅ Đã có. Store **bắt buộc** có vì app dùng mạng (`internetClient`). URL: `https://github.com/nhaajtdajt/open-note/blob/main/PRIVACY.md` |
| Ghi nhận bên thứ ba ([THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md)) | ✅ Đã có. Bản phát hành kèm .NET runtime (MIT), nên phải giữ thông báo bản quyền của nó. |
| Nhãn hiệu Google/Gemini | ✅ Chỉ nhắc tên để mô tả ("dùng Gemini API bằng key của bạn"), không dùng logo Google, ghi rõ không liên kết với Google. Giữ đúng như vậy trong mô tả Store. |
| Luật Bảo vệ dữ liệu cá nhân 2025 (Luật 91/2025/QH15, hiệu lực 01/01/2026; Nghị định 356/2025/NĐ-CP thay Nghị định 13/2023) | ✅ App không thu thập, không lưu trên máy chủ, không chuyển dữ liệu cá nhân về nhà phát triển. Dữ liệu nằm trên máy người dùng; AI chỉ chạy khi người dùng bấm, bằng key của chính họ, gửi thẳng tới Google. Nếu sau này thêm máy chủ, đồng bộ hay đăng nhập thì phải xem lại luật này và yêu cầu lưu dữ liệu tại Việt Nam của Luật An ninh mạng 2025. |
| Điều khoản Gemini API | ✅ Đã cảnh báo trong app và PRIVACY.md: gói miễn phí, Google có thể dùng nội dung gửi lên. Người dùng tự đồng ý điều khoản của Google khi tạo key. |
| Tên "Open Note" | ⚠️ **Bạn làm:** giữ tên trong Partner Center (mục 3). Nên tra trước ở [WIPO Global Brand Database](https://branddb.wipo.int) và [IP Việt Nam](https://wipopublish.ipvietnam.gov.vn). Có nhiều app tên gần giống ("OpenNote", "Opennote"). Nếu tên bị giữ, dùng "Open Note – Sticky Notes". |
| Tài khoản nhà phát triển Microsoft | ⚠️ **Bạn làm** (mục 2). Cần giấy tờ tùy thân thật của bạn, mình không làm thay được. |
| Thuế | App miễn phí, không có doanh thu nên không phát sinh gì. Nếu sau này bán, Microsoft thu thuế GTGT ở một số thị trường; thu nhập của bạn vẫn kê khai thuế TNCN ở Việt Nam. |
| Giấy phép kinh doanh | Phát hành app miễn phí, không thu tiền, không phải game hay mạng xã hội thì không cần giấy phép riêng. |

## 2. Đăng ký tài khoản nhà phát triển Microsoft Store (miễn phí cho cá nhân)

Từ 09/2025, Microsoft bỏ phí đăng ký cho **cá nhân** (công ty vẫn trả 99 USD một lần), không cần thẻ tín dụng.

1. Vào <https://storedeveloper.microsoft.com> → **Get started** → đăng nhập bằng tài khoản Microsoft cá nhân.
2. Chọn loại **Individual**, điền tên hiển thị (Publisher display name), ví dụ `nhaajtdajt` hoặc tên thật.
3. Xác minh danh tính: chụp **CCCD hoặc hộ chiếu** và **ảnh selfie**. Thường mất từ vài giờ đến vài ngày.
4. Thông tin liên hệ hỗ trợ: Store yêu cầu một email hoặc website hỗ trợ. Có thể dùng `https://github.com/nhaajtdajt/open-note/issues`.

## 3. Tên đã giữ và thông tin định danh gói (đã làm xong, 23/10/2026)

Tên **"Open Note" đã có người giữ**, nên app được giữ dưới tên **Open Note Sticky Notes** (Store ID `9PBF0GXRK94Z`, trạng thái In draft, giữ trong 3 tháng: phải nộp trước khi hết hạn). Trên Start menu app vẫn hiện là "Open Note".

| Giá trị trong manifest | Giá trị của app này |
|---|---|
| `Package/Identity/Name` | `Lovebasicsagain.OpenNoteStickyNotes` |
| `Package/Identity/Publisher` | `CN=C3B8BC78-4DA3-401C-ABD2-A8AD58E13D75` |
| `Package/Properties/PublisherDisplayName` | `Love basics again` |
| Package Family Name | `Lovebasicsagain.OpenNoteStickyNotes_397akh0xk2en6` |
| Liên kết Store | https://apps.microsoft.com/detail/9PBF0GXRK94Z |

Các giá trị này không phải bí mật (chúng nằm trong gói MSIX công khai), và `build\package.ps1` đã dùng làm mặc định.
## 4. Tạo gói MSIX

```powershell
.\build\package.ps1
```

Kết quả: `publish\OpenNote-<phiên bản>-x64.msix` (đã dùng sẵn thông tin định danh ở mục 3). Gói **không cần ký**: Store sẽ ký khi duyệt. Muốn ra bản mới thì tăng `<Version>` trong `src/OpenNote/OpenNote.csproj`.

Có thể để GitHub Actions build: tạo repository variables `STORE_IDENTITY_NAME`, `STORE_PUBLISHER`, `STORE_PUBLISHER_DISPLAY_NAME` (Settings → Secrets and variables → Actions → Variables), rồi đẩy một tag `v1.0.0`. File MSIX sẽ nằm trong GitHub Release.

## 5. Điền bài nộp (Submission)

### Pricing and availability
- Giá: **Free**. Thị trường: tất cả. Hiển thị: Public (hoặc Private để thử trước).

### Properties
- Category: **Productivity**.
- Privacy policy URL: `https://github.com/nhaajtdajt/open-note/blob/main/PRIVACY.md`
- Website: `https://github.com/nhaajtdajt/open-note`
- System requirements: Windows 10 version 1809 hoặc mới hơn, x64.

### Age ratings (bảng hỏi IARC)
Trả lời đúng sự thật. App không có bạo lực, không mua bán, không chat hay chia sẻ giữa người dùng, không thu thập vị trí. Có điểm nên khai: app có thể hiển thị chữ do AI tạo (Google Gemini) **khi người dùng tự yêu cầu**. Kết quả thường là 3+ / Everyone.

### Packages
Tải file `.msix` lên. Store sẽ hỏi lý do dùng capability hạn chế `runFullTrust`. Dán đoạn sau:

> Open Note is a classic Win32/WPF desktop app packaged with MSIX (Desktop Bridge). runFullTrust is required for every packaged desktop app. It is used to show the sticky-note windows on the desktop, register the user's global keyboard shortcuts (RegisterHotKey), show a notification-area icon, and read text aloud with the Windows speech voices. The app does not use administrator rights, install drivers or services, or change system settings.

### Store listing (English)

**Description**

> Open Note puts lightweight sticky notes on your Windows desktop. Each note is its own small glass window that you can move, resize and color. Notes save as you type and come back exactly where you left them, even across several monitors.
>
> • Global shortcuts: new note (Ctrl+Alt+N), hide or show all notes (Ctrl+Alt+H), choose which notes stay on screen (Ctrl+Alt+P), all notes and archive (Ctrl+Alt+M)
> • Bold and italic, checklists (/> ) and bullets (/- ), pictures pasted straight from screenshots
> • Read text aloud with the offline Windows voices
> • Check with AI: get a corrected and a more natural version of your English, or translate Vietnamese into natural English, using the Google Gemini API with your own free key
> • Low memory use, no account, no ads, no tracking. Your notes stay on your PC.

**Short description:** Lightweight glass sticky notes for the Windows desktop, with global shortcuts and an optional AI writing check.

**Keywords:** sticky notes, notes, desktop notes, post-it, memo, reminder, checklist

**Copyright:** © 2026 nhaajtdajt

### Store listing (Tiếng Việt), thêm ngôn ngữ vi-VN nếu muốn

> Open Note đặt những tờ giấy note nhẹ, trong như kính lên màn hình Windows. Mỗi note là một cửa sổ nhỏ, kéo, đổi cỡ và đổi màu tùy ý. Note tự lưu khi bạn gõ và mở lại đúng chỗ cũ, kể cả khi dùng nhiều màn hình.
>
> • Phím tắt toàn hệ thống: tạo note (Ctrl+Alt+N), ẩn/hiện tất cả (Ctrl+Alt+H), chọn note giữ lại (Ctrl+Alt+P), danh sách và lưu trữ (Ctrl+Alt+M)
> • Chữ đậm, nghiêng, checklist (/> ), gạch đầu dòng (/- ), dán ảnh chụp màn hình
> • Đọc to bằng giọng đọc offline của Windows
> • Kiểm tra bằng AI: sửa câu tiếng Anh cho đúng và tự nhiên, hoặc dịch tiếng Việt sang tiếng Anh tự nhiên, dùng Google Gemini API với key miễn phí của bạn
> • Nhẹ, không tài khoản, không quảng cáo, không theo dõi. Note nằm trên máy bạn.

### Hình ảnh
- **Ảnh chụp màn hình:** ít nhất 1, nên 3–4 ảnh PNG từ 1366×768 trở lên (ví dụ: vài note trên desktop, menu màu, popup AI, danh sách note).
- **Logo Store 1:1:** `packaging/store/Logo300.png`, đã tạo sẵn.

### Submission options → Notes for certification

> Open Note runs in the notification area (tray). On first launch it shows a "Welcome to Open Note" note; press Ctrl+Alt+N to create a note and Ctrl+Alt+M for the list of notes. The tray icon menu has every command. "Check with AI" needs the user's own free Google Gemini API key (Ctrl+Alt+K explains how to get one); all other features work without it and without an internet connection. The app starts with Windows through the declared startup task, which users can turn off in Settings > Apps > Startup.

## 6. Sau khi nộp
- Duyệt (certification) thường mất đến 3 ngày làm việc. Nếu bị từ chối, Partner Center ghi rõ lý do; sửa xong thì nộp lại.
- Bản Store tự cập nhật cho người dùng mỗi khi bạn nộp gói có phiên bản cao hơn.

## 7. Bản tải trên GitHub và cảnh báo SmartScreen
File `.exe` và `.zip` trên GitHub **chưa ký số**. Lúc đầu Windows SmartScreen có thể báo "Windows protected your PC" (người dùng bấm *More info → Run anyway*). Cách bỏ cảnh báo này:
- Hướng người dùng cài từ **Microsoft Store**, vì Store đã ký sẵn. Đây là cách dễ nhất.
- Hoặc mua chứng chỉ ký mã (code signing). Có phí hằng năm và cần xác minh danh tính. Dịch vụ Azure Artifact Signing (trước là Trusted Signing) rẻ hơn, nhưng chỉ nhận cá nhân ở một số quốc gia; hãy kiểm tra điều kiện trước.
