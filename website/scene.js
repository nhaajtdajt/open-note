// The hero: Open Note's sticky notes in 3D. Grab one and drag it, shake it to fade it,
// throw it to make it glide and bounce off the edges, or drop it in the bin at the bottom right.
// The physics constants are the app's own (src/OpenNote/Core/Motion.cs).
import * as THREE from "three";

const hero = document.querySelector(".hero");
const copy = document.querySelector(".hero-copy");
const stage = document.querySelector(".stage");
const canvas = document.querySelector(".scene");
const binEl = document.querySelector(".bin");
const toast = document.querySelector(".toast");
const reduceMotion = matchMedia("(prefers-reduced-motion: reduce)");

const DRAG = 3.0;            // friction per second while gliding
const BOUNCE = 0.72;         // speed kept after hitting an edge
const REST_SPEED = 40;       // px/s: below this a gliding note stops
const THROW_MIN = 260;       // px/s at release to start gliding (the app uses 1100 for whole windows)
const THROW_MAX = 5200;
const BIN_AFTER_PX = 28;     // the bin shows once a note has moved this far...
const BIN_AFTER_MS = 140;    // ...and been held this long
const BIN_REACH = 40;        // the drop zone reaches this far beyond the bin
const SHAKE_STROKE = 60;     // px per shake stroke
const SHAKE_STROKE_MS = 320;
const SHAKE_TURNS = 4;
const MARGIN = 20;           // room around each note's texture for the lit star

const NOTES = [
  { key: "today", color: "#FFC9D8", w: 270, h: 220, star: true, tilt: -3.2, side: [0.22, 0.30], stacked: [0.24, 0.30] },
  { key: "ideas", color: "#BFF3EC", w: 286, h: 200, tilt: 2.4, side: [0.72, 0.22], stacked: [0.77, 0.74] },
  { key: "empty", color: "#E4FAF6", w: 236, h: 176, tilt: -1.6, side: [0.50, 0.56], stacked: null },
  { key: "love", color: "#FFE0EA", w: 262, h: 194, tilt: -1.2, side: [0.80, 0.72], stacked: [0.76, 0.28] },
  { key: "shop", color: "#CDF5EF", w: 228, h: 214, tilt: 3.0, side: [0.25, 0.78], stacked: [0.30, 0.76] },
];

const TEXT = {
  en: {
    today: ["Today", ["[ ] Reply to emails", "[x] Pay rent", "[ ] Book the dentist", "[ ] Call mom"]],
    ideas: ["Ideas", ["**Open Note** keeps your thoughts", "• on your desktop", "• always in sight", "• tiny on memory"]],
    empty: ["", []],
    love: ["love yourself", ["Be kind to yourself today.", "You are doing *enough*."]],
    shop: ["Shopping", ["• oat milk", "• tomatoes", "• coffee", "• a new notebook"]],
  },
  vi: {
    today: ["Hôm nay", ["[ ] Trả lời email", "[x] Đóng tiền nhà", "[ ] Hẹn nha sĩ", "[ ] Gọi cho mẹ"]],
    ideas: ["Ý tưởng", ["**Open Note** giữ suy nghĩ của bạn", "• ngay trên màn hình", "• luôn trong tầm mắt", "• nhẹ, tốn rất ít RAM"]],
    empty: ["", []],
    love: ["love yourself", ["Hôm nay hãy dịu dàng với mình.", "Bạn đang làm *đủ tốt* rồi."]],
    shop: ["Đi chợ", ["• sữa yến mạch", "• cà chua", "• cà phê", "• một cuốn sổ mới"]],
  },
};

let renderer;
try {
  renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, powerPreference: "low-power" });
} catch (e) {
  // No WebGL: show the Store screenshot instead of the live notes.
  stage.hidden = true;
  document.querySelector(".hint").hidden = true;
  document.querySelector(".scene-fallback").hidden = false;
}

if (renderer) start();

async function start() {
  const pixelRatio = Math.min(window.devicePixelRatio || 1, 2);
  renderer.setPixelRatio(pixelRatio);
  renderer.setClearColor(0x000000, 0);

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(30, 1, 10, 6000);
  const raycaster = new THREE.Raycaster();
  const ground = new THREE.Plane(new THREE.Vector3(0, 0, 1), 0);

  let width = 0, height = 0, scale = 1, mode = "side", barHeight = 68;
  const shadowCache = new Map();

  await Promise.all([
    document.fonts.load('700 15px "Be Vietnam Pro"'),
    document.fonts.load('400 15px "Be Vietnam Pro"'),
    document.fonts.load('italic 400 15px "Be Vietnam Pro"'),
  ]).catch(() => {});

  // ---------- drawing ----------

  function roundRect(ctx, x, y, w, h, r) {
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
  }

  // Upright five-point star, the same proportions as the app's star.
  function starPath(ctx, cx, cy, outer) {
    const inner = outer * 0.47;
    ctx.beginPath();
    for (let i = 0; i < 10; i++) {
      const r = i % 2 ? inner : outer;
      const a = -Math.PI / 2 + (i * Math.PI) / 5;
      ctx[i ? "lineTo" : "moveTo"](cx + r * Math.cos(a), cy + r * Math.sin(a));
    }
    ctx.closePath();
  }

  function lang() { return window.openNoteLang === "vi" ? "vi" : "en"; }

  function drawNote(note) {
    const { spec } = note;
    const [title, lines] = TEXT[lang()][spec.key];
    const k = pixelRatio * scale;
    const c = note.canvas;
    c.width = Math.round((spec.w + 2 * MARGIN) * k);
    c.height = Math.round((spec.h + 2 * MARGIN) * k);
    const ctx = c.getContext("2d");
    ctx.setTransform(k, 0, 0, k, 0, 0);
    ctx.clearRect(0, 0, spec.w + 2 * MARGIN, spec.h + 2 * MARGIN);
    ctx.translate(MARGIN, MARGIN);
    const w = spec.w, h = spec.h;

    // glass body: the note color, a little see-through, lighter at the top
    roundRect(ctx, 0, 0, w, h, 12);
    ctx.save();
    ctx.clip();
    ctx.globalAlpha = 0.95;
    ctx.fillStyle = spec.color;
    ctx.fillRect(0, 0, w, h);
    ctx.globalAlpha = 1;
    const sheen = ctx.createLinearGradient(0, 0, 0, h);
    sheen.addColorStop(0, "rgba(255,255,255,0.32)");
    sheen.addColorStop(0.45, "rgba(255,255,255,0.06)");
    sheen.addColorStop(1, "rgba(255,255,255,0)");
    ctx.fillStyle = sheen;
    ctx.fillRect(0, 0, w, h);
    ctx.fillStyle = "rgba(60,40,90,0.05)";
    ctx.fillRect(0, 0, w, 40);
    ctx.fillStyle = "rgba(40,40,90,0.09)";
    ctx.fillRect(0, 40, w, 1);
    ctx.restore();
    roundRect(ctx, 0.75, 0.75, w - 1.5, h - 1.5, 11.5);
    ctx.strokeStyle = "rgba(255,255,255,0.8)";
    ctx.lineWidth = 1.5;
    ctx.stroke();

    // header
    ctx.fillStyle = "#262a40";
    ctx.textBaseline = "middle";
    ctx.font = '700 15px "Be Vietnam Pro", "Segoe UI", sans-serif';
    ctx.fillText(title, 38, 21);

    // body
    ctx.textBaseline = "alphabetic";
    let y = 70;
    if (!lines.length) {
      ctx.font = '400 15px "Be Vietnam Pro", "Segoe UI", sans-serif';
      ctx.fillStyle = "rgba(38,42,64,0.38)";
      ctx.fillText("love basics again", 20, y);
    }
    for (const line of lines) { drawLine(ctx, line, 20, y); y += 26; }

    // footer: when the note was written
    ctx.font = '400 11.5px "Be Vietnam Pro", "Segoe UI", sans-serif';
    ctx.fillStyle = "rgba(38,42,64,0.5)";
    ctx.fillText(note.created, 20, h - 15);

    // the star: an outline in the corner, or lit and tilted, overlapping the corner
    if (note.starred) {
      ctx.save();
      ctx.translate(8, 8);
      ctx.rotate((12 * Math.PI) / 180);
      ctx.shadowColor = "rgba(255,236,140,0.95)";
      ctx.shadowBlur = 12;
      starPath(ctx, 0, 0, 17);
      ctx.fillStyle = "#ffffff";
      ctx.fill();
      ctx.shadowBlur = 0;
      starPath(ctx, 0, 0, 13.5);
      ctx.fillStyle = "#1F2A5C";
      ctx.lineJoin = "round";
      ctx.fill();
      ctx.restore();
    } else {
      starPath(ctx, 20, 21, 9.5);
      ctx.lineJoin = "round";
      ctx.lineWidth = 1.5;
      ctx.strokeStyle = "rgba(38,42,64,0.75)";
      ctx.stroke();
    }
    note.texture.needsUpdate = true;
  }

  // One line of a note: "[ ] " / "[x] " checkboxes, and **bold** / *italic* runs.
  function drawLine(ctx, line, x, y) {
    ctx.fillStyle = "#2b2f45";
    const box = line.match(/^\[( |x)\] /);
    if (box) {
      ctx.lineWidth = 1.4;
      ctx.strokeStyle = "#2b2f45";
      ctx.strokeRect(x + 0.5, y - 12, 12, 12);
      if (box[1] === "x") {
        ctx.beginPath();
        ctx.moveTo(x + 3, y - 6); ctx.lineTo(x + 6, y - 3); ctx.lineTo(x + 11, y - 11);
        ctx.stroke();
      }
      x += 20;
      line = line.slice(4);
    }
    for (const run of line.split(/(\*\*[^*]+\*\*|\*[^*]+\*)/).filter(Boolean)) {
      let text = run, font = '400 15px "Be Vietnam Pro", "Segoe UI", sans-serif';
      if (run.startsWith("**")) { text = run.slice(2, -2); font = '700 15px "Be Vietnam Pro", "Segoe UI", sans-serif'; }
      else if (run.startsWith("*")) { text = run.slice(1, -1); font = 'italic 400 15px "Be Vietnam Pro", "Segoe UI", sans-serif'; }
      ctx.font = font;
      ctx.fillText(text, x, y);
      x += ctx.measureText(text).width;
    }
  }

  // A soft shadow under a note, drawn once per note size.
  function shadowTexture(w, h) {
    const id = w + "x" + h + "@" + scale;
    if (shadowCache.has(id)) return shadowCache.get(id);
    const pad = 46, k = pixelRatio * scale * 0.5;
    const c = document.createElement("canvas");
    c.width = Math.round((w + 2 * pad) * k);
    c.height = Math.round((h + 2 * pad) * k);
    const ctx = c.getContext("2d");
    ctx.scale(k, k);
    ctx.shadowColor = "rgba(8,6,36,0.55)";
    ctx.shadowBlur = 26 * k;
    ctx.shadowOffsetX = 10000 * k;
    roundRect(ctx, pad - 10000, pad, w, h, 14);
    ctx.fill();
    const tex = new THREE.CanvasTexture(c);
    tex.colorSpace = THREE.SRGBColorSpace;
    const entry = { tex, pad };
    shadowCache.set(id, entry);
    return entry;
  }

  // ---------- notes ----------

  const notes = NOTES.map((spec, i) => {
    const note = {
      spec,
      starred: !!spec.star,
      canvas: document.createElement("canvas"),
      created: "",
      x: 0, y: 0, vx: 0, vy: 0,
      homeX: 0, homeY: 0,
      state: "idle", // idle | drag | fly | swallow | gone | appear
      lift: 0, opacity: 0, pop: 0, fadeUntil: 0,
      phase: i * 1.7,
      shownAt: 0,
    };
    note.texture = new THREE.CanvasTexture(note.canvas);
    note.texture.colorSpace = THREE.SRGBColorSpace;
    note.texture.anisotropy = renderer.capabilities.getMaxAnisotropy();
    note.mesh = new THREE.Mesh(undefined, new THREE.MeshBasicMaterial({ map: note.texture, transparent: true, depthTest: false, depthWrite: false }));
    note.shadow = new THREE.Mesh(undefined, new THREE.MeshBasicMaterial({ transparent: true, depthTest: false, depthWrite: false }));
    note.mesh.userData.note = note;
    scene.add(note.shadow, note.mesh);
    return note;
  });
  let order = notes.slice(); // back to front

  function stampDates() {
    const fmt = new Intl.DateTimeFormat(lang() === "vi" ? "vi-VN" : "en-US", { dateStyle: "short", timeStyle: "short" });
    const now = Date.now();
    notes.forEach((n, i) => { n.created = fmt.format(new Date(now - (i * 47 + 12) * 60000)); });
  }

  function buildGeometry(note) {
    const { w, h } = note.spec;
    note.mesh.geometry?.dispose();
    note.mesh.geometry = new THREE.PlaneGeometry((w + 2 * MARGIN) * scale, (h + 2 * MARGIN) * scale, 14, 12);
    note.base = Float32Array.from(note.mesh.geometry.attributes.position.array);
    note.bend = 0;
    const sh = shadowTexture(w, h);
    note.shadow.geometry?.dispose();
    note.shadow.geometry = new THREE.PlaneGeometry((w + 2 * sh.pad) * scale, (h + 2 * sh.pad) * scale);
    note.shadow.material.map = sh.tex;
    note.shadow.material.needsUpdate = true;
  }

  // The paper bends a little when it moves fast: its trailing edge lifts toward you.
  function bendPaper(note, amount, dirX, dirY) {
    if (Math.abs(amount - note.bend) < 0.05 && amount < 0.05) return;
    note.bend = amount;
    const pos = note.mesh.geometry.attributes.position;
    const hw = ((note.spec.w + 2 * MARGIN) * scale) / 2, hh = ((note.spec.h + 2 * MARGIN) * scale) / 2;
    for (let i = 0; i < pos.count; i++) {
      const nx = note.base[i * 3] / hw, ny = note.base[i * 3 + 1] / hh;
      const trail = Math.max(0, -(nx * dirX + ny * dirY));
      pos.array[i * 3 + 2] = amount * trail * trail * 26 * scale - nx * nx * 3 * scale;
    }
    pos.needsUpdate = true;
  }

  function restack() {
    order.forEach((n, i) => {
      n.shadow.renderOrder = i * 2;
      n.mesh.renderOrder = i * 2 + 1;
    });
  }

  function bringToFront(note) {
    order = order.filter((n) => n !== note).concat(note);
    restack();
  }

  // ---------- layout ----------

  function bounds(note) {
    const hw = (note.spec.w * scale) / 2, hh = (note.spec.h * scale) / 2;
    return { x0: -width / 2 + hw + 6, x1: width / 2 - hw - 6, y0: -height / 2 + hh + 6, y1: height / 2 - barHeight - hh };
  }

  function clampTo(note) {
    const b = bounds(note);
    note.x = Math.min(Math.max(note.x, b.x0), Math.max(b.x0, b.x1));
    note.y = Math.min(Math.max(note.y, b.y0), Math.max(b.y0, b.y1));
  }

  function layout() {
    const heroRect = hero.getBoundingClientRect();
    const copyRect = copy.getBoundingClientRect();
    const w = heroRect.width;
    mode = w >= 1080 ? "side" : "stacked";
    const newScale = w < 560 ? 0.6 : w < 1080 ? 0.78 : w < 1400 ? 0.9 : 1;
    const bottom = copyRect.bottom - heroRect.top;
    let area;
    if (mode === "side") {
      area = { x0: copyRect.right - heroRect.left + 24, x1: w - 24, y0: barHeight + 16, y1: heroRect.height - 24 };
      hero.style.minHeight = "";
    } else {
      const need = Math.round(460 * newScale) + 40;
      hero.style.minHeight = Math.max(Math.round(bottom + need + 24), window.innerHeight) + "px";
      const h = hero.getBoundingClientRect().height;
      area = { x0: 16, x1: w - 16, y0: bottom + 24, y1: h - 24 };
    }
    const rescale = newScale !== scale || !notes[0].base;
    scale = newScale;
    resize();
    for (const note of notes) {
      const slot = note.spec[mode];
      note.hidden = !slot;
      note.mesh.visible = note.shadow.visible = !!slot;
      if (rescale) { buildGeometry(note); drawNote(note); }
      if (!slot) continue;
      const px = area.x0 + slot[0] * (area.x1 - area.x0);
      const py = area.y0 + slot[1] * (area.y1 - area.y0);
      note.homeX = px - width / 2;
      note.homeY = height / 2 - py;
      note.x = note.homeX; note.y = note.homeY;
      clampTo(note);
      note.homeX = note.x; note.homeY = note.y;
      note.vx = note.vy = 0;
      if (note.state !== "appear") note.state = "idle";
    }
  }

  function resize() {
    const r = stage.getBoundingClientRect();
    width = r.width; height = r.height;
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    camera.position.set(0, 0, height / 2 / Math.tan(THREE.MathUtils.degToRad(camera.fov / 2)));
    camera.lookAt(0, 0, 0);
    camera.updateProjectionMatrix();
  }

  // ---------- pointer ----------

  const ndc = new THREE.Vector2();
  function toWorld(clientX, clientY) {
    const r = canvas.getBoundingClientRect();
    ndc.set(((clientX - r.left) / r.width) * 2 - 1, -((clientY - r.top) / r.height) * 2 + 1);
    raycaster.setFromCamera(ndc, camera);
    const p = new THREE.Vector3();
    return raycaster.ray.intersectPlane(ground, p) ? p : null;
  }

  // The topmost note under the pointer, and where on it (in the note's own units, from its top-left).
  function hitTest(clientX, clientY) {
    const r = canvas.getBoundingClientRect();
    if (clientX < r.left || clientX > r.right || clientY < r.top || clientY > r.bottom) return null;
    ndc.set(((clientX - r.left) / r.width) * 2 - 1, -((clientY - r.top) / r.height) * 2 + 1);
    raycaster.setFromCamera(ndc, camera);
    const hits = raycaster.intersectObjects(order.filter((n) => n.mesh.visible && (n.state === "idle" || n.state === "fly" || n.state === "appear")).map((n) => n.mesh), false);
    hits.sort((a, b) => b.object.renderOrder - a.object.renderOrder);
    for (const hit of hits) {
      const note = hit.object.userData.note;
      const local = hit.object.worldToLocal(hit.point.clone());
      const u = local.x / scale + note.spec.w / 2, v = -local.y / scale + note.spec.h / 2;
      if (u >= -6 && u <= note.spec.w + 2 && v >= -6 && v <= note.spec.h + 2) return { note, u, v };
    }
    return null;
  }

  let drag = null;
  const root = document.documentElement;

  function onDown(e) {
    if (drag || (e.pointerType === "mouse" && e.button !== 0)) return;
    const hit = hitTest(e.clientX, e.clientY);
    if (!hit) return;
    e.preventDefault();
    const note = hit.note;
    const p = toWorld(e.clientX, e.clientY);
    if (!p) return;
    bringToFront(note);
    note.state = "drag";
    note.vx = note.vy = 0;
    const now = performance.now();
    drag = {
      note, id: e.pointerId, startX: e.clientX, startY: e.clientY, startT: now,
      offX: note.x - p.x, offY: note.y - p.y, moved: false, onStar: hit.u < 40 && hit.v < 40,
      samples: [{ t: now, x: note.x, y: note.y }],
      shake: { dir: 0, turnX: e.clientX, extreme: e.clientX, lastTurn: now, turns: [] },
      hot: false,
    };
    root.classList.add("note-grabbing");
    // whatever sits under the note (a link, a button) must not get this click
    window.addEventListener("click", swallowClick, { capture: true, once: true });
  }

  function swallowClick(e) { e.preventDefault(); e.stopPropagation(); }

  function onMove(e) {
    if (!drag) {
      if (e.pointerType === "mouse") root.classList.toggle("note-grab", !!hitTest(e.clientX, e.clientY));
      return;
    }
    if (e.pointerId !== drag.id) return;
    const note = drag.note, now = performance.now();
    if (!drag.moved && Math.hypot(e.clientX - drag.startX, e.clientY - drag.startY) > 4) drag.moved = true;
    const p = toWorld(e.clientX, e.clientY);
    if (p) {
      note.x = p.x + drag.offX;
      note.y = p.y + drag.offY;
      clampTo(note);
    }
    drag.samples.push({ t: now, x: note.x, y: note.y });
    while (drag.samples.length > 2 && now - drag.samples[0].t > 90) drag.samples.shift();
    trackShake(e.clientX, now);
    trackBin(e.clientX, e.clientY, now);
  }

  function trackShake(x, now) {
    const s = drag.shake;
    if (s.dir === 0) {
      if (Math.abs(x - s.turnX) > 12) { s.dir = Math.sign(x - s.turnX); s.extreme = x; }
      return;
    }
    if ((x - s.extreme) * s.dir > 0) { s.extreme = x; return; }
    if (Math.abs(x - s.extreme) < 16) return;
    // turned around
    const stroke = Math.abs(s.extreme - s.turnX);
    if (stroke >= SHAKE_STROKE && now - s.lastTurn <= SHAKE_STROKE_MS) s.turns.push(now);
    else s.turns = [];
    s.turns = s.turns.filter((t) => now - t < 1500);
    s.dir = -s.dir; s.turnX = s.extreme; s.extreme = x; s.lastTurn = now;
    if (s.turns.length >= SHAKE_TURNS) drag.note.fadeUntil = now + 650;
  }

  function binZone() {
    const r = binEl.getBoundingClientRect();
    return { left: r.left - BIN_REACH, top: r.top - BIN_REACH, cx: r.left + r.width / 2, cy: r.top + r.height * 0.45 };
  }

  function trackBin(x, y, now) {
    if (!binEl.classList.contains("open") && drag.moved && now - drag.startT > BIN_AFTER_MS &&
        Math.hypot(x - drag.startX, y - drag.startY) > BIN_AFTER_PX) binEl.classList.add("open");
    if (!binEl.classList.contains("open")) return;
    const z = binZone();
    drag.hot = x >= z.left && y >= z.top;
    binEl.classList.toggle("hot", drag.hot);
  }

  function onUp(e) {
    if (!drag || e.pointerId !== drag.id) return;
    const { note, moved, onStar, hot } = drag;
    const now = performance.now();
    root.classList.remove("note-grabbing");
    if (!moved) window.setTimeout(() => window.removeEventListener("click", swallowClick, { capture: true }), 0);
    drag.note.fadeUntil = Math.min(drag.note.fadeUntil, now + 200);

    if (e.type === "pointercancel") {
      note.state = "idle";
    } else if (!moved && onStar) {
      note.starred = !note.starred;
      note.pop = 1;
      drawNote(note);
      note.state = "idle";
    } else if (hot) {
      swallow(note);
    } else {
      const [vx, vy] = releaseVelocity(drag.samples, now);
      const speed = Math.hypot(vx, vy);
      if (moved && speed > THROW_MIN) {
        const k = Math.min(1, THROW_MAX / speed);
        note.vx = vx * k; note.vy = vy * k;
        note.state = "fly";
      } else {
        note.state = "idle";
      }
    }
    if (!hot) closeBin();
    drag = null;
  }

  // Speed over the last ~90 ms; zero if the pointer stopped before letting go.
  function releaseVelocity(samples, now) {
    if (samples.length < 2) return [0, 0];
    const last = samples[samples.length - 1];
    if (now - last.t > 110) return [0, 0];
    const first = samples[0];
    const dt = (last.t - first.t) / 1000;
    if (dt <= 0.008) return [0, 0];
    return [(last.x - first.x) / dt, (last.y - first.y) / dt];
  }

  function closeBin() { binEl.classList.remove("hot", "open"); }

  function swallow(note) {
    const z = binZone();
    const p = toWorld(z.cx, z.cy);
    note.state = "swallow";
    note.from = { x: note.x, y: note.y, t: performance.now() };
    note.to = p ? { x: p.x, y: p.y } : { x: note.x, y: note.y };
    window.setTimeout(() => {
      binEl.classList.remove("hot");
      binEl.classList.add("gulp");
      toast.classList.add("show");
    }, 300);
    window.setTimeout(() => { binEl.classList.remove("gulp", "open"); }, 900);
    window.setTimeout(() => { toast.classList.remove("show"); }, 2200);
    window.setTimeout(() => {
      // it comes back where it lives, as if restored from the archive
      note.x = note.homeX; note.y = note.homeY;
      note.state = "appear";
      note.shownAt = performance.now();
      note.opacity = 0;
    }, 1700);
  }

  window.addEventListener("pointerdown", onDown);
  window.addEventListener("pointermove", onMove, { passive: true });
  window.addEventListener("pointerup", onUp);
  window.addEventListener("pointercancel", onUp);
  // On touch screens, a finger on a note moves the note instead of scrolling the page.
  window.addEventListener("touchstart", (e) => {
    const t = e.touches[0];
    if (t && hitTest(t.clientX, t.clientY)) e.preventDefault();
  }, { passive: false });

  // ---------- frame ----------

  const pointer = { x: 0, y: 0 };
  window.addEventListener("pointermove", (e) => {
    pointer.x = (e.clientX / window.innerWidth) * 2 - 1;
    pointer.y = (e.clientY / window.innerHeight) * 2 - 1;
  }, { passive: true });

  const easeOutBack = (t) => 1 + 2.2 * Math.pow(t - 1, 3) + 1.2 * Math.pow(t - 1, 2);
  const easeIn = (t) => t * t * t;
  let last = performance.now(), running = true, visible = true;
  const born = performance.now();

  function frame(now) {
    if (!running) return;
    const dt = Math.min((now - last) / 1000, 1 / 20);
    last = now;
    const still = reduceMotion.matches;
    const t = now / 1000;

    // a hint of depth: the view leans toward the pointer
    if (!still) {
      camera.position.x += (pointer.x * 14 - camera.position.x) * 0.04;
      camera.position.y += (-pointer.y * 10 - camera.position.y) * 0.04;
      camera.lookAt(0, 0, 0);
    }

    order.forEach((note, i) => {
      if (note.hidden) return;
      const m = note.mesh;
      let sx = 1, rotZ = THREE.MathUtils.degToRad(note.spec.tilt), alpha = 1, bob = 0, dirX = 0, dirY = 0, speed = 0;

      // page-load: the notes are put down one after another
      const intro = still ? 1 : Math.min(1, Math.max(0, (now - born - 250 - i * 110) / 520));
      if (note.state === "fly") {
        const decay = Math.exp(-DRAG * dt);
        note.vx *= decay; note.vy *= decay;
        note.x += note.vx * dt; note.y += note.vy * dt;
        const b = bounds(note);
        if (note.x < b.x0) { note.x = b.x0; note.vx = Math.abs(note.vx) * BOUNCE; }
        if (note.x > b.x1) { note.x = b.x1; note.vx = -Math.abs(note.vx) * BOUNCE; }
        if (note.y < b.y0) { note.y = b.y0; note.vy = Math.abs(note.vy) * BOUNCE; }
        if (note.y > b.y1) { note.y = b.y1; note.vy = -Math.abs(note.vy) * BOUNCE; }
        speed = Math.hypot(note.vx, note.vy);
        if (speed < REST_SPEED) { note.state = "idle"; note.vx = note.vy = 0; }
        else { dirX = note.vx / speed; dirY = note.vy / speed; }
        rotZ += note.vx * 0.00009;
      } else if (note.state === "drag" && drag) {
        const s = drag.samples, a = s[0], z = s[s.length - 1];
        const ddt = Math.max((z.t - a.t) / 1000, 0.016);
        const vx = (z.x - a.x) / ddt, vy = (z.y - a.y) / ddt;
        speed = now - z.t > 110 ? 0 : Math.hypot(vx, vy);
        if (speed > 1) { dirX = vx / speed; dirY = vy / speed; }
        rotZ += THREE.MathUtils.clamp(vx * 0.00005, -0.12, 0.12);
      }

      if (note.state === "swallow") {
        const p = Math.min(1, (now - note.from.t) / 380), e = easeIn(p);
        note.x = note.from.x + (note.to.x - note.from.x) * e;
        note.y = note.from.y + (note.to.y - note.from.y) * e;
        sx = 1 - 0.85 * e;
        rotZ += 0.9 * e;
        alpha = 1 - e;
        if (p >= 1) note.state = "gone";
      } else if (note.state === "gone") {
        alpha = 0;
      } else if (note.state === "appear") {
        const p = Math.min(1, (now - note.shownAt) / 420);
        sx = 0.7 + 0.3 * easeOutBack(p);
        alpha = p;
        if (p >= 1) note.state = "idle";
      }

      // lifted while held: closer to you, bigger shadow
      const lifted = note.state === "drag" ? 1 : 0;
      note.lift += (lifted - note.lift) * Math.min(1, dt * 14);
      // shaken: fades so you can see behind it
      const faded = now < note.fadeUntil ? 0.14 : 1;
      note.opacity += (faded * alpha * intro - note.opacity) * Math.min(1, dt * (faded < 1 ? 14 : 7));
      if (note.state === "swallow" || note.state === "gone") note.opacity = alpha;
      note.pop = Math.max(0, note.pop - dt * 4);

      if (!still && note.state === "idle") bob = Math.sin(t * 0.8 + note.phase) * 3.5;
      const drop = (1 - easeOutBack(intro)) * 40;
      const pop = Math.sin(note.pop * Math.PI) * 0.06;

      m.position.set(note.x, note.y + bob + drop, i * 0.4 + note.lift * 42);
      m.rotation.z = rotZ + (still ? 0 : Math.sin(t * 0.6 + note.phase) * 0.006);
      m.rotation.x = THREE.MathUtils.clamp(-dirY * speed * 0.00007, -0.3, 0.3);
      m.rotation.y = THREE.MathUtils.clamp(dirX * speed * 0.00007, -0.3, 0.3);
      const s = sx * (1 + note.lift * 0.035 + pop) * (1 + (1 - intro) * 0.06);
      m.scale.set(s, s, 1);
      m.material.opacity = note.opacity;
      bendPaper(note, still ? 0 : Math.min(1, speed / 2600), dirX, dirY);

      const sh = note.shadow;
      sh.position.set(note.x + (7 + note.lift * 14) * scale, note.y + bob + drop - (12 + note.lift * 22) * scale, i * 0.4 - 0.1);
      sh.rotation.z = m.rotation.z;
      const ss = s * (1 + note.lift * 0.06);
      sh.scale.set(ss, ss, 1);
      sh.material.opacity = note.opacity * (0.75 - note.lift * 0.2);
    });

    renderer.render(scene, camera);
    if (visible && !document.hidden) requestAnimationFrame(frame);
    else running = false;
  }

  function wake() {
    if (running || !visible || document.hidden) return;
    running = true;
    last = performance.now();
    requestAnimationFrame(frame);
  }

  new IntersectionObserver(([entry]) => { visible = entry.isIntersecting; wake(); }).observe(hero);
  document.addEventListener("visibilitychange", wake);

  let lastWidth = 0;
  new ResizeObserver(() => {
    const w = hero.getBoundingClientRect().width;
    if (Math.abs(w - lastWidth) > 1) { lastWidth = w; layout(); }
    else resize();
  }).observe(hero);

  window.addEventListener("langchange", () => {
    stampDates();
    notes.forEach(drawNote);
    layout();
  });

  stampDates();
  lastWidth = hero.getBoundingClientRect().width;
  layout();
  restack();
  requestAnimationFrame(frame);
}
