// Painted Bedroom: The window: frame, casements, panes and sill, and the 3D view through it (grass, trees, houses, sky).
// Unity objects: Window, Window_View. Its opening in the back wall (WIN) is in room.js.
// Loaded as a plain script after core.js; all files share one scope (see index.html).
"use strict";

// This object's materials, added to the shared palette M by buildMaterials() in core.js.
function windowMaterials() {
  M.windowFrame = paintMat({ seed: 12, base: '#1f4a2a', cols: ['#2a5c34', '#173c22', '#357040', '#204e2c'], strokes: 1100, angle: Math.PI / 2, spread: 0.12 });
  M.sill = paintMat({ seed: 31, base: '#4f8f96', cols: ['#5c9ca2', '#43808a', '#6aa8aa'], strokes: 600, angle: 0, spread: 0.2 });
  // window panes: yellow-green with vertical streaks of white and deeper green
  M.glass = paintMat({ seed: 13, base: '#cddc62', cols: ['#e4ec8a', '#bfd252', '#f2f2b6', '#a8c444', '#d8e470', '#8fae3c'], strokes: 1600, angle: Math.PI / 2, spread: 0.08, len: [30, 90], wid: [5, 10], vary: 0.06 }, { emissive: new THREE.Color('#a8c442'), emissiveIntensity: 0.3 });
  M.outside = outsideMat();
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
  windowFrame: { dL: -14, kC: 1.2, dh: -8 },
  sill: { dL: 15, kC: 1.1, dh: -15 },
  glass: { dL: -24, kC: 0.85, dh: 2 },
});

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
