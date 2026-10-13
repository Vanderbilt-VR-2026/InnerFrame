// Painted Bedroom: The room shell: walls (with the openings for the window and doors), floor, ceiling, skirting.
// Unity objects: Room_Walls, Room_Floor, Room_Ceiling, Room_Skirting. The window is in window.js, the doors in doors.js.
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
}

// Colour grading measured against the painting for these materials (see GRADE in core.js).
Object.assign(GRADE, {
  wallBack: { dL: -3.05, kC: 0.535, dh: -44.8 },
  wallLeft: { dL: -3.05, kC: 0.535, dh: -44.8 },
  wallRight: { dL: -3.05, kC: 0.535, dh: -44.8 },
  floor: { dL: 1.0, kC: 1.466, dh: -17.3 },
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
}





