// Painted Bedroom: The room shell: walls, floor, ceiling, skirting, window and the view outside, doors.
// Unity objects: Room_Walls, Room_Floor, Room_Ceiling, Room_Skirting, Window, Window_View, Door_Left, Door_Right, Door_Frames.
// Loaded as a plain script after core.js; all files share one scope (see index.html).
"use strict";

// This object's materials, added to the shared palette M by buildMaterials() in core.js.
function roomMaterials() {
  // walls: clear light blue with fine vertical strokes, lighter toward the window
  M.wallBack = paintMat({ seed: 1, base: '#86b0e4', cols: ['#94bce8', '#6f9ddc', '#aacaee', '#7ea8e0', '#e4eef6', '#5f8fd4', '#d2e2f2'], strokes: 3400, angle: Math.PI / 2, spread: 0.22, len: [18, 50], wid: [3, 7], vary: 0.06, bumpScale: 1.4 });
  M.wallLeft = paintMat({ seed: 22, base: '#76a2dc', cols: ['#86aee2', '#5f90d6', '#9cc0ea', '#6e9ad8', '#dce8f4', '#4f82cc'], strokes: 3400, angle: Math.PI / 2, spread: 0.2, len: [18, 50], wid: [3, 7], vary: 0.06, bumpScale: 1.4 });
  M.wallRight = paintMat({ seed: 23, base: '#93bbe8', cols: ['#a4c6ec', '#86b0e4', '#cfe0f4', '#e8f0f8', '#b2cff0', '#7ea8e0', '#f2f6fa'], strokes: 3600, angle: Math.PI / 2, spread: 0.1, len: [26, 80], wid: [3, 6], vary: 0.05, bumpScale: 1.4 });
  M.ceiling = paintMat({ seed: 2, base: '#a8cac4', cols: ['#b9d6cc', '#98c0bc', '#c9e0d4', '#a4c8c0'], strokes: 1400, angle: 0.3, spread: 0.9 });
  M.floor = floorCanvas();
  M.base = new THREE.MeshStandardMaterial({ color: '#244a48', roughness: 0.9 });
  // doors: light blue with paler, greenish mottling
  M.door = paintMat({ seed: 10, base: '#62a2e2', cols: ['#72aee6', '#4f90d8', '#8cc0ec', '#a9cfee', '#9cc4cc', '#5a9ade'], strokes: 2200, angle: Math.PI / 2, spread: 0.3, len: [12, 40], wid: [5, 11], vary: 0.06 });
  M.doorTrim = paintMat({ seed: 11, base: '#3f78c0', cols: ['#4a84c8', '#336cb4', '#5890d0'], strokes: 900, angle: Math.PI / 2, spread: 0.2 });
  M.casing = paintMat({ seed: 26, base: '#244e36', cols: ['#2f5e42', '#1c402c', '#3a6a4c'], strokes: 900, angle: Math.PI / 2, spread: 0.2 });
  M.casingPale = paintMat({ seed: 30, base: '#cfe0ee', cols: ['#e2ecf4', '#bcd2e6', '#f0f4f6'], strokes: 900, angle: Math.PI / 2, spread: 0.1 });
  M.windowFrame = paintMat({ seed: 12, base: '#1f4a2a', cols: ['#2a5c34', '#173c22', '#357040', '#204e2c'], strokes: 1100, angle: Math.PI / 2, spread: 0.12 });
  M.sill = paintMat({ seed: 31, base: '#4f8f96', cols: ['#5c9ca2', '#43808a', '#6aa8aa'], strokes: 600, angle: 0, spread: 0.2 });
  // window panes: yellow-green with vertical streaks of white and deeper green
  M.glass = paintMat({ seed: 13, base: '#cddc62', cols: ['#e4ec8a', '#bfd252', '#f2f2b6', '#a8c444', '#d8e470', '#8fae3c'], strokes: 1600, angle: Math.PI / 2, spread: 0.08, len: [30, 90], wid: [5, 10], vary: 0.06 }, { emissive: new THREE.Color('#a8c442'), emissiveIntensity: 0.3 });
  M.outside = outsideMat();
  // the landing behind each door: a dim, cool hallway
  M.recess = paintMat({ seed: 42, base: '#3c5a74', cols: ['#4a6a86', '#2f4a62', '#56789a', '#34506a'], strokes: 900, angle: Math.PI / 2, spread: 0.3 });
  // outside: grass of the square far below, tree trunks, painted foliage and a sky
  M.grass = paintMat({ seed: 43, base: '#7fa63c', cols: ['#94b84a', '#6a9432', '#a9c75c', '#5c8a2c', '#b8d070'], strokes: 1600, angle: 0.3, spread: 1.4, wid: [5, 11] });
  M.trunk = paintMat({ seed: 44, base: '#6b4a2a', cols: ['#7d5a34', '#58391f', '#8a6a40'], strokes: 500, angle: Math.PI / 2, spread: 0.2 });
  M.foliage = paintMat({ seed: 45, base: '#5f8f2e', cols: ['#77a83a', '#4c7a24', '#9cc44e', '#3e6a1e', '#b4d464'], strokes: 1500, circular: true, wid: [5, 11] });
  M.sky = new THREE.MeshBasicMaterial({ map: toTextures(strokeCanvas({ seed: 46, base: '#bcd8d8', cols: ['#cfe4e0', '#a6c8cc', '#e2eee4', '#98bcc4'], strokes: 1400, angle: 0.1, spread: 0.6, len: [30, 80], wid: [6, 14] })).map, side: THREE.DoubleSide });
  M.houseWall = paintMat({ seed: 47, base: '#e0c070', cols: ['#ecd088', '#d0aa58', '#f2dc9c'], strokes: 700 });
  M.roof = paintMat({ seed: 48, base: '#c0603a', cols: ['#d0704a', '#a84e2c', '#dc8458'], strokes: 600, angle: 0 });
}

// Colour grading measured against the painting for these materials (see GRADE in core.js).
Object.assign(GRADE, {
  wallBack: { dL: -3.05, kC: 0.535, dh: -44.8 },
  wallLeft: { dL: -3.05, kC: 0.535, dh: -44.8 },
  wallRight: { dL: -3.05, kC: 0.535, dh: -44.8 },
  floor: { dL: 1.0, kC: 1.466, dh: -17.3 },
  door: { dL: -0.47, kC: 0.63, dh: -28.1 },
  doorTrim: { dL: -0.47, kC: 0.63, dh: -28.1 },
  casing: { dL: -0.47, kC: 0.63, dh: -28.1 },
  casingPale: { dL: -0.47, kC: 0.63, dh: -28.1 },
  windowFrame: { dL: -14, kC: 1.2, dh: -8 },
  sill: { dL: 15, kC: 1.1, dh: -15 },
  glass: { dL: -24, kC: 0.85, dh: 2 },
});

// The painted floor is one large, non-repeating canvas laid over the whole room,
// so the pale worn patch in front of the bed sits where Van Gogh put it.
const FLOOR = { x0: -2.45, z0: -0.75, size: 4.9 };
function floorCanvas() {
  // Short, mostly lengthwise dabs of teal, mint and cream, with a worn pale patch
  // in front of the bed, khaki and brown toward the painter, and the broken dark
  // joints that make the boards read almost like tiles.
  // 3072 px over 4.9 m keeps the strokes crisp right in front of the viewer;
  // at 2048 the near floor was magnified into a soft blur.
  const S = 3072, toPx = S / FLOOR.size, k = S / 2048;
  // the floor's colour grade is applied to its palette (cheap) instead of to every pixel
  const G = c => gradeHex(c, GRADE.floor);
  const teal = ['#62a890', '#74b49e', '#58987e', '#8fc4a8', '#6aac96', '#80b8a2', '#548e7c', '#a0ceb2', '#9ab894'].map(G);
  const cream = ['#e6dfbf', '#d8cfa5', '#efe8cc', '#cdc7a0', '#e0d6b0', '#bfd0a8'].map(G);
  const khaki = ['#a99f74', '#8f8a62', '#b8ab7c', '#7f8a5e', '#c4b48a'].map(G);
  const pale = (x, z) => {
    // placed by projecting the painting's pale floor patches back onto the floor
    const a = Math.exp(-(((x + 0.5) / 0.75) ** 2 + ((z - 2.0) / 0.45) ** 2));
    const b = Math.exp(-(((x - 0.6) / 0.35) ** 2 + ((z - 1.72) / 0.25) ** 2)) * 0.7;
    return Math.min(1, a * 1.25 + b);
  };
  const front = z => Math.max(0, Math.min(1, (1.75 - z) / 0.4));   // the khaki-brown strip along the bottom of the canvas
  const o = strokeCanvas({
    size: S, seed: 3, base: G('#6aac94'), strokes: Math.round(52000 * k * k), angle: Math.PI / 2, spread: 0.14, len: [18 * k, 56 * k], wid: [7 * k, 14 * k], vary: 0.08, bumpScale: 2.4, tile: false,
    pick: (u, v, r) => {
      // colour is chosen per patch of board, not per stroke, so each plank segment
      // reads as one uneven slab of colour the way Van Gogh laid the floor in
      const x = FLOOR.x0 + u * FLOOR.size, z = FLOOR.z0 + (1 - v) * FLOOR.size;  // canvas top is the far end of the room
      const board = Math.floor((x - FLOOR.x0) / 0.12), seg = Math.floor(z / 0.38 + board * 0.37);
      const h = Math.abs(Math.sin(board * 12.9898 + seg * 78.233) * 43758.5453) % 1;
      const pick = arr => arr[(Math.abs(Math.sin(board * 3.1 + seg * 7.7) * 9173.1) | 0) % arr.length];
      const q = r() < 0.75 ? h : r();               // most strokes follow their patch, some break it up
      if (q < front(z) * 0.55) return r() < 0.7 ? pick(khaki) : khaki[(r() * khaki.length) | 0];
      if (q < front(z) * 0.85 || q < pale(x, z)) return r() < 0.7 ? pick(cream) : cream[(r() * cream.length) | 0];
      return r() < 0.7 ? pick(teal) : teal[(r() * teal.length) | 0];
    },
    after: (c, h, S, r) => {
      const bw = 0.12 * toPx;
      for (let x = 0; x < S; x += bw) {
        // broken seams between boards
        for (let y = 0; y < S;) {
          const L = (30 + r() * 120) * k; if (r() < 0.55) {
            c.strokeStyle = 'rgba(32,62,58,0.7)'; c.lineWidth = 2.2 * k;
            c.beginPath(); c.moveTo(x + (r() - 0.5) * 3, y); c.lineTo(x + (r() - 0.5) * 3, y + L); c.stroke();
            h.strokeStyle = '#505050'; h.lineWidth = 3 * k; h.beginPath(); h.moveTo(x, y); h.lineTo(x, y + L); h.stroke();
          }
          y += L + r() * 60 * k;
        }
        // butt joints every few tens of centimetres, staggered board to board
        let y = r() * 0.4 * toPx;
        while (y < S) {
          c.strokeStyle = 'rgba(28,52,50,0.85)'; c.lineWidth = 3 * k;
          c.beginPath(); c.moveTo(x + 1, y); c.lineTo(x + bw - 1, y + (r() - 0.5) * 5); c.stroke();
          y += (0.25 + r() * 0.4) * toPx;
        }
      }
    },
  });
  const t = toTextures(o, false); t.map.anisotropy = t.bump.anisotropy = 16;
  const fm = new THREE.MeshStandardMaterial({ map: t.map, bumpMap: t.bump, bumpScale: 2.4, roughness: 0.85 }); fm.userData.pregraded = true; return fm;
}

// The view through the window: bright sunlit green.
function outsideMat() {
  const t = toTextures(strokeCanvas({ seed: 77, base: '#c4da6c', cols: ['#d7e48a', '#a9c94f', '#8fbd45', '#e9ee9e', '#6e9f3a', '#c6dc6c'], strokes: 1800, angle: -1.2, spread: 1.4, len: [12, 34], wid: [5, 12] }));
  return new THREE.MeshBasicMaterial({ map: t.map });
}

// One window pane: greener on the left, lemon toward the right where the light
// comes through, with pale vertical streaks dragged down the glass.
function paneMat(seed) {
  const W = 96, H = 160, r = rng(seed);
  const cv = document.createElement('canvas'); cv.width = W; cv.height = H; const c = cv.getContext('2d');
  const gr = c.createLinearGradient(0, 0, W, 0);
  gr.addColorStop(0, '#93b544'); gr.addColorStop(0.45, '#c3d65a'); gr.addColorStop(1, '#eef08c');
  c.fillStyle = gr; c.fillRect(0, 0, W, H); c.lineCap = 'round';
  for (let i = 0; i < 70; i++) {
    const x = r() * W, y = r() * H, L = 20 + r() * 60, f = x / W;
    const cols = f < 0.4 ? ['#86a83e', '#a8c24c', '#6f9436', '#b7cf58'] : ['#e6ea7a', '#f6f4b4', '#d6e06a', '#ffffff'];
    c.strokeStyle = jitter(cols[(r() * cols.length) | 0], r, 0.08); c.globalAlpha = 0.85; c.lineWidth = 3 + r() * 5;
    c.beginPath(); c.moveTo(x, y); c.lineTo(x + (r() - 0.5) * 3, y + L); c.stroke();
  }
  c.globalAlpha = 1;
  const t = new THREE.CanvasTexture(cv); t.colorSpace = THREE.SRGBColorSpace; t.wrapS = THREE.RepeatWrapping; t.repeat.x = -1;
  gradeCanvas(cv, GRADE.glass);
  const pm = new THREE.MeshStandardMaterial({ map: t, roughness: 0.6, emissive: new THREE.Color('#ffffff'), emissiveMap: t, emissiveIntensity: 0.45, side: THREE.DoubleSide }); pm.userData.cat = 'glass'; return pm;
}

// ---------------------------------------------------------------------------
// The room shell.
const WIN = { x0: -1.72, x1: -0.6, y0: 1.33, y1: 3.27 };
const DOOR_L = { z0: 2.25, z1: 3.3, h: 2.75 };      // left wall, beside the towel
const DOOR_R = { z0: 0.75, z1: 1.9, h: 2.3 };     // bed wall, just past the bed foot

function buildRoom() {
  const H = ROOM.h;
  const back = wallSlab(ROOM.rB.x - ROOM.xL, H, [[WIN.x0 - ROOM.xL, WIN.y0, WIN.x1 - ROOM.xL, WIN.y1]], M.wallBack);
  back.position.set(ROOM.xL, 0, ROOM.zB); world.add(back);
  const front = wallSlab(ROOM.rF.x - ROOM.xL, H, [], M.wallLeft); front.position.set(ROOM.xL, 0, ROOM.zF - 0.14); world.add(front);
  const left = wallSlab(ROOM.zB - ROOM.zF, H, [[DOOR_L.z0 - ROOM.zF, 0, DOOR_L.z1 - ROOM.zF, DOOR_L.h]], M.wallLeft);
  left.rotation.y = -Math.PI / 2; left.position.set(ROOM.xL, 0, ROOM.zF); world.add(left);
  const right = wallSlab(RD.L, ROOM.hR, [[rightU(DOOR_R.z1), 0, rightU(DOOR_R.z0), DOOR_R.h]], M.wallRight);
  right.rotation.y = R_ANG; right.position.set(ROOM.rB.x, 0, ROOM.rB.z); world.add(right);

  // floor (one painted canvas) and ceiling
  const fg = new THREE.PlaneGeometry(FLOOR.size, FLOOR.size); fg.rotateX(-Math.PI / 2);
  // plane UVs: u along +x, v along -z after rotation; flip v so the canvas reads from the front of the room
  const uv = fg.attributes.uv; for (let i = 0; i < uv.count; i++) uv.setY(i, 1 - uv.getY(i));
  mesh(fg, M.floor, FLOOR.x0 + FLOOR.size / 2, 0, FLOOR.z0 + FLOOR.size / 2);
  mesh(worldUV(new THREE.BoxGeometry(5.2, 0.04, 5.2), 1.6), M.ceiling, 0, H + 0.02, 1.65);
  // the ceiling slopes down onto the bed wall: the room sits under the roof
  const sg = group(ROOM.rB.x, 0, ROOM.rB.z, R_ANG);
  const sw = 0.55, drop = H - ROOM.hR;
  const slope = box(RD.L + 0.3, 0.04, Math.hypot(sw, drop), M.ceiling, RD.L / 2, (H + ROOM.hR) / 2, -sw / 2, sg);
  slope.rotation.x = Math.atan2(drop, sw);
  // skirting: a dark teal line where walls meet the floor, as in the painting
  box(ROOM.rB.x - ROOM.xL, 0.06, 0.02, M.base, (ROOM.xL + ROOM.rB.x) / 2, 0.03, ROOM.zB - 0.01);
  box(0.02, 0.06, DOOR_L.z0 - ROOM.zF, M.base, ROOM.xL + 0.01, 0.03, (ROOM.zF + DOOR_L.z0) / 2);
  box(0.02, 0.06, ROOM.zB - DOOR_L.z1, M.base, ROOM.xL + 0.01, 0.03, (DOOR_L.z1 + ROOM.zB) / 2);
  box(RD.L, 0.06, 0.02, M.base, RD.L / 2, 0.03, -0.01, sg);

  // the world beyond the window
  buildOutside();
  buildWindow(); buildDoors();
}

// What the window looks out on, in 3D: the bedroom is upstairs, over a square of
// grass and trees, with a few houses beyond and a pale sky. Seen with parallax as you move.
function buildOutside() {
  const og = new THREE.Group(); og.userData.exportName = 'Window_View'; world.add(og);
  const cx = (WIN.x0 + WIN.x1) / 2, z0 = ROOM.zB + 0.2, ground = -3.4;
  const gr = new THREE.PlaneGeometry(40, 30, 1, 1); gr.rotateX(-Math.PI / 2); mesh(worldUV(gr, 1.6), M.grass, cx, ground, z0 + 15, og);
  const r = rng(907);
  const tree = (x, z, s) => {
    const th = 2.6 * s;
    cyl(0.09 * s, 0.14 * s, th, M.trunk, x, ground + th / 2, z, og, 8);
    for (let k = 0; k < 6; k++) {
      const b = new THREE.Mesh(worldUV(new THREE.IcosahedronGeometry(0.9 * s * (0.7 + r() * 0.5), 1), 0.8), M.foliage);
      b.position.set(x + (r() - 0.5) * 1.4 * s, ground + th + (r() - 0.2) * 1.3 * s, z + (r() - 0.5) * 1.2 * s);
      b.scale.y = 0.85 + r() * 0.3; og.add(b);
    }
  };
  for (const [x, z, s] of [[-3.2, 5, 1.2], [-0.6, 6.5, 1.35], [1.8, 5.5, 1.1], [3.8, 8, 1.3], [-5.5, 9, 1.4], [0.8, 11, 1.5], [-2.4, 13, 1.3], [5.5, 13.5, 1.4]]) tree(cx + x, z0 + z, s);
  const house = (x, z, w, h, d) => {
    box(w, h, d, M.houseWall, x, ground + h / 2, z, og, 1.2);
    const roof = new THREE.Mesh(worldUV(new THREE.CylinderGeometry(0.01, d * 0.62, w, 3, 1).rotateZ(Math.PI / 2).rotateX(Math.PI / 6), 1), M.roof);
    roof.scale.set(1, 0.55, 1); roof.position.set(x, ground + h + d * 0.16, z); og.add(roof);
  };
  house(cx - 6, z0 + 17, 5, 6, 3); house(cx + 1, z0 + 19, 6, 7.5, 3.2); house(cx + 8, z0 + 16.5, 4.5, 5.5, 3);
  const sky = new THREE.CylinderGeometry(26, 26, 30, 32, 1, true, Math.PI * 0.6, Math.PI * 0.8);
  const sk = mesh(sky, M.sky, cx, ground + 12, z0, og); sk.rotation.y = Math.PI;
}

function buildWindow() {
  const w = WIN.x1 - WIN.x0, h = WIN.y1 - WIN.y0, z = ROOM.zB - 0.005, cx = (WIN.x0 + WIN.x1) / 2;
  // only a thin dark reveal and a teal ledge show around the opening; the casements are the window
  box(w + 0.06, 0.035, 0.12, M.sill, cx, WIN.y0 - 0.0175, z - 0.05);
  box(w + 0.1, 0.035, 0.03, M.windowFrame, cx, WIN.y0 - 0.05, z - 0.11);
  // muntin heights read off the painting: rails at 1.36, 2.08, 2.58 and 3.06 m
  const rails = [0, (2.08 - WIN.y0) / h, (2.58 - WIN.y0) / h, (3.06 - WIN.y0) / h];
  let seed = 60;
  const leaf = (hingeX, dir, angle) => {
    const g = group(hingeX, WIN.y0, z - 0.04, dir * angle);
    const lw = w / 2, s = dir, stile = 0.07, rail = 0.05, depth = 0.06;
    box(stile, h, depth, M.windowFrame, s * stile / 2, h / 2, 0, g, 0.5);
    box(stile, h, depth, M.windowFrame, s * (lw - stile / 2), h / 2, 0, g, 0.5);
    box(lw, 0.075, depth, M.windowFrame, s * lw / 2, 0.0375, 0, g, 0.5);             // heavier bottom rail
    for (const f of rails.slice(1)) box(lw - stile, rail * 0.8, depth * 0.7, M.windowFrame, s * lw / 2, f * h, 0, g, 0.5);
    box(lw, rail, depth, M.windowFrame, s * lw / 2, h - rail / 2, 0, g, 0.5);
    // individual panes between the bars
    const edges = [0.075, ...rails.slice(1).map(f => f * h), h - rail];
    for (let i = 0; i < edges.length - 1; i++) {
      const y0 = edges[i] + (i ? rail * 0.4 : 0), y1 = edges[i + 1] - rail * 0.4, ph = y1 - y0;
      if (ph <= 0.02) continue;
      const pane = new THREE.Mesh(new THREE.PlaneGeometry(lw - 2 * stile, ph), paneMat(seed++));
      pane.position.set(s * lw / 2, (y0 + y1) / 2, 0);
      g.add(pane);
    }
  };
  leaf(WIN.x0, 1, 0.36);  // positive: swings into the room
  leaf(WIN.x1, -1, 0.3);
}

function panelDoor(parent, w, h) {
  box(w, h, 0.045, M.door, 0, h / 2, 0, parent, 0.7);
  // two tall upper panels and two short lower panels: a dark outline with a pale bevel inside
  for (const [y, ph] of [[h * 0.64, h * 0.5], [h * 0.19, h * 0.24]]) for (const x of [-w * 0.22, w * 0.22]) {
    box(w * 0.34, ph, 0.012, M.doorTrim, x, y, 0.027, parent, 0.5);
    box(w * 0.3, ph - 0.04, 0.012, M.casingPale, x, y, 0.031, parent, 0.5);
    box(w * 0.27, ph - 0.08, 0.012, M.door, x, y, 0.037, parent, 0.5);
  }
  const knob = new THREE.Mesh(new THREE.SphereGeometry(0.028, 12, 10), M.peg); knob.position.set(w * 0.42, h * 0.42, 0.06); parent.add(knob);
}
function buildDoors() {
  // left door: light blue, taller than the frame of the picture, with a pale jamb and a dark green casing line
  const lw = DOOR_L.z1 - DOOR_L.z0;
  const gl = group(ROOM.xL + 0.035, 0, (DOOR_L.z0 + DOOR_L.z1) / 2, Math.PI / 2); gl.userData.exportName = 'Door_Left';
  panelDoor(gl, lw - 0.03, DOOR_L.h - 0.02);
  box(0.05, DOOR_L.h + 0.1, 0.07, M.casingPale, ROOM.xL + 0.02, (DOOR_L.h + 0.1) / 2, DOOR_L.z1 + 0.035);
  box(0.06, DOOR_L.h + 0.1, 0.04, M.casing, ROOM.xL + 0.03, (DOOR_L.h + 0.1) / 2, DOOR_L.z1 + 0.09);
  box(0.06, DOOR_L.h + 0.1, 0.06, M.casing, ROOM.xL + 0.03, (DOOR_L.h + 0.1) / 2, DOOR_L.z0 - 0.03);
  box(0.06, 0.1, lw + 0.16, M.casing, ROOM.xL + 0.03, DOOR_L.h + 0.05, (DOOR_L.z0 + DOOR_L.z1) / 2);
  // right door, in the bed wall
  const rw = rightU(DOOR_R.z0) - rightU(DOOR_R.z1), um = (rightU(DOOR_R.z0) + rightU(DOOR_R.z1)) / 2;
  const sg = group(ROOM.rB.x, 0, ROOM.rB.z, R_ANG);
  const gr = group(um, 0, -0.035, Math.PI, sg); gr.userData.exportName = 'Door_Right';
  panelDoor(gr, rw - 0.03, DOOR_R.h - 0.02);
  for (const u of [rightU(DOOR_R.z1) - 0.05, rightU(DOOR_R.z0) + 0.05]) box(0.1, DOOR_R.h + 0.1, 0.05, M.doorTrim, u, (DOOR_R.h + 0.1) / 2, -0.025, sg);
  box(rw + 0.2, 0.1, 0.05, M.doorTrim, um, DOOR_R.h + 0.05, -0.025, sg);
  // behind each door, a shallow landing, so an opened (or carried-off) door shows a real space, not the void
  const recess = (parent, wdt, hgt, deep, cu, sideAxis) => {
    const rg = new THREE.Group(); rg.userData.exportName = 'Door_Frames'; parent.add(rg);
    const t = 0.04;
    if (sideAxis === 'x') {   // left wall: the landing runs out along -x, its width along z
      box(t, hgt + 0.06, wdt + 0.1, M.recess, ROOM.xL - deep, hgt / 2, cu, rg, 0.8);
      for (const zz of [cu - wdt / 2 - t / 2, cu + wdt / 2 + t / 2]) box(deep, hgt + 0.06, t, M.recess, ROOM.xL - deep / 2, hgt / 2, zz, rg, 0.8);
      box(deep, t, wdt + 0.1, M.recess, ROOM.xL - deep / 2, hgt + 0.02, cu, rg, 0.8);
      box(deep, t, wdt + 0.1, M.recess, ROOM.xL - deep / 2, -t / 2, cu, rg, 0.8);
    } else {                  // bed wall (local frame): out along +z, width along u
      box(wdt + 0.1, hgt + 0.06, t, M.recess, cu, hgt / 2, deep, rg, 0.8);
      for (const uu of [cu - wdt / 2 - t / 2, cu + wdt / 2 + t / 2]) box(t, hgt + 0.06, deep, M.recess, uu, hgt / 2, deep / 2, rg, 0.8);
      box(wdt + 0.1, t, deep, M.recess, cu, hgt + 0.02, deep / 2, rg, 0.8);
      box(wdt + 0.1, t, deep, M.recess, cu, -t / 2, deep / 2, rg, 0.8);
    }
  };
  recess(world, lw, DOOR_L.h, 0.9, (DOOR_L.z0 + DOOR_L.z1) / 2, 'x');
  recess(sg, rw, DOOR_R.h, 0.9, um, 'u');
}
