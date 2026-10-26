// Language switch (English / Vietnamese) and the download links from the latest GitHub release.
(function () {
  const REPO = "nhaajtdajt/open-note";
  const RELEASES = "https://github.com/" + REPO + "/releases/latest";
  const STORE_LIVE = true; // the Microsoft Store listing is published

  const vi = {
    skip: "Bỏ qua, tới phần tải về",
    navFeatures: "Tính năng",
    navShortcuts: "Phím tắt",
    heroTitle: "Giấy note nằm yên đúng chỗ bạn&nbsp;đặt.",
    heroLede: "Open Note đặt những tờ note nhỏ, trong như kính lên màn hình Windows. Note tự lưu khi bạn gõ và vẫn còn đó sau khi khởi động lại máy. Miễn phí, không cần tài khoản, không quảng cáo.",
    download: "Tải về cho Windows",
    req: "Windows 10 hoặc 11, 64-bit.",
    hint: "Thử đi, cầm một tờ note lên. Lắc, ném, hoặc thả nó vào góc dưới bên phải.",
    binLabel: "Lưu trữ",
    archived: "Đã lưu trữ. Không mất gì cả.",
    featTitle: "Một tờ note làm được gì",
    featLede: "Bốn điều đầu tiên làm được ngay trên các note ở đầu trang, bạn thử ở đó nhé.",
    f1t: "Đánh sao việc quan trọng",
    f1d: "Bấm ngôi sao ở góc. Note có sao luôn nằm đầu danh sách.",
    f2t: "Lắc để nhìn phía sau",
    f2d: "Cầm note lắc mạnh, note mờ đi để bạn đọc được thứ bên dưới. Buông tay là note hiện lại.",
    f3t: "Ném sang một bên",
    f3d: "Quăng mạnh note, nó trượt đi và nảy lại khi chạm mép màn hình.",
    f4t: "Thả vào thùng",
    f4d: "Kéo note tới góc dưới bên phải để lưu trữ. Không xóa gì cả; khôi phục lại trong danh sách note.",
    f5t: "Viết theo cách bạn nghĩ",
    f5d: "Checklist, gạch đầu dòng, chữ đậm, chữ nghiêng, và dán thẳng ảnh chụp màn hình.",
    f6t: "Nghe đọc to",
    f6d: "Đọc bất kỳ dòng nào bằng giọng đọc có sẵn của Windows. Không cần mạng.",
    f7t: "Kiểm tra tiếng Anh",
    f7d: "Tùy chọn: nhận một bản sửa đúng và một bản tự nhiên hơn cho câu của bạn, hoặc dịch tiếng Việt sang tiếng Anh, bằng key Google Gemini miễn phí của chính bạn.",
    f8t: "Không làm phiền",
    f8d: "Nằm ở khay hệ thống và tự chạy cùng Windows. Khoảng 44&nbsp;MB bộ nhớ, không tốn CPU khi rảnh, và note không bao giờ rời khỏi máy bạn.",
    keysTitle: "Chỉ một phím tắt, từ bất kỳ ứng dụng nào",
    keysLede: "Quên phím nào? Nhấn Ctrl+Alt+/ là một tấm thẻ ghi đủ các phím hiện ngay giữa màn hình.",
    anywhere: "Ở bất kỳ đâu",
    inNote: "Trong một note",
    kN: "Note mới ngay chỗ con trỏ chuột",
    kH: "Ẩn hoặc hiện tất cả note",
    kP: "Chọn note nào giữ lại trên màn hình",
    kM: "Tất cả note, tìm kiếm và lưu trữ",
    kHelp: "Cách sử dụng",
    kQ: "Thoát hẳn Open Note",
    kCheck: "Tạo dòng checklist, hoặc đánh dấu xong",
    kBI: "Chữ đậm và chữ nghiêng",
    kStar: "Đánh sao note",
    kR: "Đọc to",
    kG: "Kiểm tra bằng AI",
    kD: "Lưu trữ note",
    getTitle: "Tải Open Note",
    s1t: "Tải bộ cài",
    s1d: "Một file có sẵn mọi thứ bên trong. Bạn không phải cài thêm gì khác.",
    s2t: "Chạy nó",
    s2d: "Nếu Windows SmartScreen báo đã bảo vệ máy bạn, chọn <b>More info</b> rồi <b>Run anyway</b>. Thông báo này hiện vì app chưa được ký số.",
    s3t: "Viết note đầu tiên",
    s3d: "Nhấn <kbd>Ctrl</kbd><kbd>Alt</kbd><kbd>N</kbd>. Từ giờ Open Note tự chạy cùng Windows; bạn tắt được trong menu ở khay hệ thống.",
    altZip: "Không muốn cài đặt?",
    zipLink: "Tải bản portable .zip",
    storeLive: "Tải từ Microsoft Store",
    storeSoon: "Sắp có trên Microsoft Store.",
    license: "Open Note miễn phí và mã nguồn mở theo giấy phép MIT.",
    privacy: "Quyền riêng tư",
    source: "Mã nguồn",
    issues: "Báo lỗi",
    version: "Phiên bản {v}, {size}.",
    title: "Open Note: giấy note cho Windows",
  };

  const nodes = Array.from(document.querySelectorAll("[data-i18n]"));
  const en = {};
  nodes.forEach((n) => { en[n.dataset.i18n] = n.innerHTML; });
  en.version = "Version {v}, {size}.";
  en.title = document.title;
  const dict = { en, vi };

  let release = null;

  function readLang() {
    try { const saved = localStorage.getItem("lang"); if (saved === "en" || saved === "vi") return saved; } catch (e) { /* storage blocked */ }
    return (navigator.language || "").toLowerCase().startsWith("vi") ? "vi" : "en";
  }

  function setLang(lang) {
    const d = dict[lang];
    document.documentElement.lang = lang;
    nodes.forEach((n) => { const s = d[n.dataset.i18n]; if (s != null) n.innerHTML = s; });
    document.title = d.title;
    window.openNoteLang = lang;
    showRelease();
    window.dispatchEvent(new CustomEvent("langchange", { detail: lang }));
  }

  document.querySelector(".lang").addEventListener("click", () => {
    const next = document.documentElement.lang === "vi" ? "en" : "vi";
    try { localStorage.setItem("lang", next); } catch (e) { /* storage blocked */ }
    setLang(next);
  });

  function showRelease() {
    const text = release ? dict[document.documentElement.lang].version.replace("{v}", release.version).replace("{size}", release.size) : "";
    document.querySelectorAll(".release-meta").forEach((n) => { n.textContent = text; });
  }

  function megabytes(bytes) { return Math.round(bytes / 1048576) + " MB"; }

  async function loadRelease() {
    let data = null;
    try { data = JSON.parse(sessionStorage.getItem("release") || "null"); } catch (e) { /* storage blocked */ }
    if (!data) {
      try {
        const res = await fetch("https://api.github.com/repos/" + REPO + "/releases/latest", { headers: { Accept: "application/vnd.github+json" } });
        if (!res.ok) return; // no release yet: the buttons keep pointing at the releases page
        data = await res.json();
        try { sessionStorage.setItem("release", JSON.stringify({ tag_name: data.tag_name, assets: data.assets.map((a) => ({ name: a.name, size: a.size, browser_download_url: a.browser_download_url })) })); } catch (e) { /* storage blocked */ }
      } catch (e) { return; }
    }
    const exe = data.assets.find((a) => /setup.*\.exe$/i.test(a.name));
    const zip = data.assets.find((a) => /\.zip$/i.test(a.name));
    if (exe) document.querySelectorAll(".download-exe").forEach((a) => { a.href = exe.browser_download_url; });
    if (zip) document.querySelectorAll(".download-zip").forEach((a) => { a.href = zip.browser_download_url; });
    const main = exe || zip;
    if (main) { release = { version: data.tag_name.replace(/^v/, ""), size: megabytes(main.size) }; showRelease(); }
  }

  if (STORE_LIVE) {
    document.querySelector(".store-link").hidden = false;
    document.querySelector(".store-soon").hidden = true;
  }

  document.querySelectorAll(".download-exe, .download-zip").forEach((a) => { if (!a.href) a.href = RELEASES; });
  setLang(readLang());
  loadRelease();
})();
