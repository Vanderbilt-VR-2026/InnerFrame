// Painted Bedroom: Things on the walls: pictures and mirror (with their painted canvases), towel, peg rail, clothes, hat.
// Unity objects: Mirror, Picture_*, Towel, PegRail, Smock, Clothes_Dark, Cloth_Pale, Hat.
// Loaded as a plain script after core.js; all files share one scope (see index.html).
"use strict";

// This object's materials, added to the shared palette M by buildMaterials() in core.js.
function wallMaterials() {
  M.frame = paintMat({ seed: 16, base: '#d08a2e', cols: ['#dc9a3a', '#b87424', '#e6aa4c'], strokes: 800, angle: 0, spread: 0.2 });
  M.frameDark = paintMat({ seed: 32, base: '#a2703a', cols: ['#b08040', '#8a5a2a', '#bc8c4a'], strokes: 800, angle: 0, spread: 0.2 });
  M.blackFrame = new THREE.MeshStandardMaterial({ color: '#141c1c', roughness: 0.7 });
  M.string = new THREE.MeshBasicMaterial({ color: '#1f2a40' });
  // the towel: pale yellow-green linen with darker green shadow strokes in the folds,
  // a dark green border along both edges and a couple of orange-red woven stripes
  M.towel = paintMat({ flat: 0.45, seed: 18, base: '#c9d99a', cols: ['#d6e2aa', '#b8cc86', '#e2eab8', '#a8c07a', '#cfdc9e'], strokes: 1100, angle: Math.PI / 2, spread: 0.2, wid: [5, 9], vary: 0.05, tile: false,
    after: (c, h, S, r) => {
      c.lineCap = 'round';
      for (let i = 0; i < 70; i++) { const x = r() * S, y = r() * S; c.strokeStyle = `rgba(${70 + r() * 30 | 0},${130 + r() * 30 | 0},${70 + r() * 20 | 0},${0.35 + r() * 0.3})`; c.lineWidth = 6 + r() * 8; c.beginPath(); c.moveTo(x, y); c.lineTo(x + (r() - 0.5) * 10, y + 40 + r() * 80); c.stroke(); }
      c.fillStyle = 'rgba(36,96,56,0.95)'; c.fillRect(0, 0, S * 0.045, S); c.fillRect(S * 0.955, 0, S * 0.045, S);
      c.fillStyle = 'rgba(90,150,90,0.7)'; c.fillRect(S * 0.045, 0, S * 0.03, S); c.fillRect(S * 0.925, 0, S * 0.03, S);
      c.strokeStyle = 'rgba(206,92,48,0.9)'; c.lineWidth = S * 0.02;
      for (const y0 of [0.58, 0.8]) { c.beginPath(); c.moveTo(S * 0.06, S * y0); c.quadraticCurveTo(S * 0.5, S * (y0 + 0.05), S * 0.94, S * (y0 - 0.03)); c.stroke(); }
      c.strokeStyle = 'rgba(206,92,48,0.85)'; c.lineWidth = S * 0.016; c.beginPath(); c.moveTo(S * 0.3, S * 0.12); c.lineTo(S * 0.42, S * 0.38); c.stroke();
    } });
  M.smock = paintMat({ flat: 0.38, seed: 19, base: '#9cc6d6', cols: ['#b4d6e2', '#7aaccc', '#cce4ec', '#4f84b8', '#a6cede', '#3f74aa'], strokes: 1400, angle: Math.PI / 2, spread: 0.35, wid: [5, 10], vary: 0.06 });
  M.clothDark = paintMat({ seed: 28, base: '#2f62ac', cols: ['#3c72bc', '#24509a', '#5a8ccc', '#1f4488'], strokes: 1000, angle: Math.PI / 2, spread: 0.4 });
  M.straw = paintMat({ seed: 20, base: '#dcb04a', cols: ['#e8c460', '#c4962e', '#f0d888', '#b88a2a'], strokes: 1100, circular: true, wid: [3, 7] });
  M.paperEdge = new THREE.MeshStandardMaterial({ color: '#d8cf80', roughness: 0.9 });
  M.hatBand = new THREE.MeshStandardMaterial({ color: '#1f3050', roughness: 0.8 });
  M.peg = paintMat({ seed: 21, base: '#b4702a', cols: ['#c4823a', '#965a20'], strokes: 400 });
  // backs of the pictures: raw linen on a stretcher, so a picture turned around is still a real object
  M.canvasBack = paintMat({ seed: 41, base: '#b49b6c', cols: ['#c2aa7c', '#a08658', '#cdb88e', '#957a4e'], strokes: 500, angle: 0, spread: 0.2, wid: [3, 6] });
}

// Colour grading measured against the painting for these materials (see GRADE in core.js).
Object.assign(GRADE, {
  smock: { dL: 6.93, kC: 0.702, dh: -28.2 },
  clothDark: { dL: 6.93, kC: 0.702, dh: -28.2 },
  towel: { dL: 0, kC: 1.0, dh: 0 },
});

// Small canvases for the pictures on the walls.
function canvasTex(W, H, seed, draw) {
  const cv = document.createElement('canvas'); cv.width = W; cv.height = H;
  const c = cv.getContext('2d'); c.lineCap = 'round'; const r = rng(seed);
  const dab = (x, y, a, L, w, col) => {
    c.strokeStyle = jitter(col, r, 0.15); c.lineWidth = w; c.beginPath();
    c.moveTo(x - Math.cos(a) * L / 2, y - Math.sin(a) * L / 2); c.lineTo(x + Math.cos(a) * L / 2, y + Math.sin(a) * L / 2); c.stroke();
  };
  draw(c, r, dab, W, H);
  const t = new THREE.CanvasTexture(cv); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 8;
  // the scene is built mirrored (left-handed layout), so pictures are flipped back to read the right way round
  t.wrapS = THREE.RepeatWrapping; t.repeat.x = -1; return t;
}
const PICTURES = {
  // the small landscape: a cream sky, a dark olive tree just left of centre with a
  // visible trunk, a blue-green wooded hill on the left, a softer yellow-green rise on
  // the right, and a yellow-green field laid in short horizontal strokes
  landscape: () => canvasTex(480, 330, 41, (c, r, dab, W, H) => {
    const sky = c.createLinearGradient(0, 0, 0, H * 0.55);
    sky.addColorStop(0, '#f4f2e2'); sky.addColorStop(1, '#ebe8cc');
    c.fillStyle = sky; c.fillRect(0, 0, W, H);
    for (let i = 0; i < 420; i++) dab(r() * W, r() * H * 0.55, 0.05 + (r() - 0.5) * 0.3, 26, 7, ['#f6f4e6', '#ece9cf', '#faf9ee', '#e2e4cc', '#efe9d0'][i % 5]);
    // distant blue-green hill on the left
    c.fillStyle = '#2f7a6e'; c.beginPath(); c.moveTo(0, H * 0.66); c.quadraticCurveTo(W * 0.08, H * 0.5, W * 0.2, H * 0.55);
    c.quadraticCurveTo(W * 0.32, H * 0.6, W * 0.4, H * 0.64); c.lineTo(W * 0.4, H * 0.7); c.lineTo(0, H * 0.72); c.fill();
    for (let i = 0; i < 160; i++) dab(r() * W * 0.38, H * 0.55 + r() * H * 0.12, (r() - 0.5) * 0.5, 16, 7, ['#2c6e6a', '#3a8476', '#245e5c', '#4a9080'][i % 4]);
    // softer yellow-green rise on the right
    for (let i = 0; i < 200; i++) dab(W * 0.6 + r() * W * 0.4, H * 0.52 + r() * H * 0.16, -0.25 + (r() - 0.5) * 0.4, 18, 7, ['#b8c45a', '#a4b84e', '#c8cc68', '#98b04a'][i % 4]);
    // the field
    for (let i = 0; i < 520; i++) { const y = H * 0.66 + r() * H * 0.34; dab(r() * W, y, 0.03 + (r() - 0.5) * 0.15, 30, 8, ['#c8c252', '#b4b848', '#ddd06a', '#a6aa44', '#d2c25c', '#8ea040'][i % 6]); }
    // tree: dark olive crown in clumps, highlights on the upper right, a short trunk
    c.strokeStyle = '#4a3a1c'; c.lineWidth = 9; c.beginPath(); c.moveTo(W * 0.43, H * 0.48); c.lineTo(W * 0.42, H * 0.72); c.stroke();
    c.lineWidth = 5; c.beginPath(); c.moveTo(W * 0.43, H * 0.55); c.lineTo(W * 0.37, H * 0.46); c.moveTo(W * 0.43, H * 0.52); c.lineTo(W * 0.5, H * 0.44); c.stroke();
    for (const [cx, cy, rr] of [[0.36, 0.38, 0.11], [0.47, 0.32, 0.13], [0.55, 0.42, 0.1], [0.42, 0.46, 0.1]]) {
      for (let i = 0; i < 90; i++) { const a = r() * 6.28, d = Math.sqrt(r()); const lit = Math.cos(a - 5.5) * d > 0.3;
        dab(W * cx + Math.cos(a) * W * rr * d, H * cy + Math.sin(a) * H * rr * 1.4 * d, r() * 3, 12, 8, lit ? ['#8a9a3a', '#9aa848', '#7e8e34'][i % 3] : ['#4f6a26', '#5e7a2c', '#3e5620', '#6b7a2a'][i % 4]); }
    }
    // shadow of the tree on the field
    for (let i = 0; i < 40; i++) dab(W * 0.36 + r() * W * 0.18, H * 0.72 + r() * H * 0.05, 0, 18, 6, '#7e8a3a');
  }),
  // a man, head and shoulders, turned three-quarters to the right: fair yellow hair,
  // pale face modelled in yellow and pink, dark blue coat over a white collar, on a
  // pale green-blue ground
  portraitA: () => canvasTex(360, 460, 43, (c, r, dab, W, H) => {
    c.fillStyle = '#cfdcd2'; c.fillRect(0, 0, W, H);
    for (let i = 0; i < 520; i++) dab(r() * W, r() * H, 1.4 + (r() - 0.5) * 0.7, 26, 9, ['#cfdcd2', '#bed2cc', '#dfe8dc', '#b4ccd0', '#d8e2c8'][i % 5]);
    // coat
    c.fillStyle = '#2f4f86'; c.beginPath(); c.moveTo(W * 0.02, H); c.quadraticCurveTo(W * 0.08, H * 0.72, W * 0.38, H * 0.66);
    c.lineTo(W * 0.66, H * 0.66); c.quadraticCurveTo(W * 0.96, H * 0.74, W, H); c.closePath(); c.fill();
    for (let i = 0; i < 260; i++) { const x = W * 0.05 + r() * W * 0.9, y = H * 0.7 + r() * H * 0.3; dab(x, y, 1.2 + (x / W - 0.5) * 0.8, 20, 8, ['#2f4f86', '#3e62a0', '#22406e', '#4e74b0', '#1c3460'][i % 5]); }
    // white collar and a dark tie
    c.fillStyle = '#f2f2ea'; c.beginPath(); c.moveTo(W * 0.4, H * 0.64); c.lineTo(W * 0.6, H * 0.64); c.lineTo(W * 0.56, H * 0.74); c.lineTo(W * 0.5, H * 0.7); c.lineTo(W * 0.44, H * 0.74); c.closePath(); c.fill();
    c.fillStyle = '#26305a'; c.beginPath(); c.moveTo(W * 0.48, H * 0.69); c.lineTo(W * 0.53, H * 0.69); c.lineTo(W * 0.51, H * 0.8); c.closePath(); c.fill();
    // neck and face
    c.fillStyle = '#e8c49a'; c.fillRect(W * 0.44, H * 0.54, W * 0.13, H * 0.12);
    c.fillStyle = '#f2d4a4'; c.beginPath(); c.ellipse(W * 0.52, H * 0.42, W * 0.17, H * 0.17, 0.08, 0, Math.PI * 2); c.fill();
    for (let i = 0; i < 160; i++) { const a = r() * 6.28, d = Math.sqrt(r()); const x = W * 0.52 + Math.cos(a) * W * 0.15 * d, y = H * 0.42 + Math.sin(a) * H * 0.15 * d;
      dab(x, y, a + 1.57, 9, 5, x < W * 0.46 ? ['#d8a878', '#cc9a6e'][i % 2] : ['#f4d8a8', '#eec694', '#f8e2b8', '#e8b48a'][i % 4]); }
    // ear on the left side, the far cheek in shadow
    c.fillStyle = '#d89c74'; c.beginPath(); c.ellipse(W * 0.37, H * 0.44, W * 0.03, H * 0.045, 0, 0, Math.PI * 2); c.fill();
    // hair: a fair cap swept to the right
    c.fillStyle = '#e4c45a'; c.beginPath(); c.ellipse(W * 0.5, H * 0.3, W * 0.2, H * 0.1, -0.1, Math.PI, Math.PI * 2.05); c.fill();
    for (let i = 0; i < 110; i++) dab(W * 0.34 + r() * W * 0.34, H * 0.22 + r() * H * 0.1, -0.3 + (r() - 0.5) * 0.6, 16, 6, ['#ecd06a', '#d8b04a', '#f4e08c', '#c89c3c'][i % 4]);
    // features: brows, eyes looking right, nose, mouth, a short reddish beard line
    c.lineCap = 'round';
    c.strokeStyle = '#8a6a3a'; c.lineWidth = 3; for (const x of [0.47, 0.6]) { c.beginPath(); c.moveTo(W * (x - 0.04), H * 0.375); c.lineTo(W * (x + 0.04), H * 0.37); c.stroke(); }
    c.fillStyle = '#2c3a5a'; for (const x of [0.48, 0.61]) { c.beginPath(); c.ellipse(W * x, H * 0.4, 5, 3.5, 0, 0, 7); c.fill(); }
    c.strokeStyle = '#b07a52'; c.lineWidth = 3; c.beginPath(); c.moveTo(W * 0.56, H * 0.41); c.lineTo(W * 0.59, H * 0.47); c.lineTo(W * 0.55, H * 0.48); c.stroke();
    c.strokeStyle = '#b8544a'; c.lineWidth = 4; c.beginPath(); c.moveTo(W * 0.5, H * 0.52); c.quadraticCurveTo(W * 0.55, H * 0.53, W * 0.59, H * 0.515); c.stroke();
    c.strokeStyle = 'rgba(176,120,60,0.6)'; c.lineWidth = 5; c.beginPath(); c.moveTo(W * 0.4, H * 0.5); c.quadraticCurveTo(W * 0.52, H * 0.6, W * 0.66, H * 0.5); c.stroke();
    // a dark contour round the head, as Van Gogh drew
    c.strokeStyle = 'rgba(60,70,90,0.55)'; c.lineWidth = 3; c.beginPath(); c.ellipse(W * 0.52, H * 0.4, W * 0.19, H * 0.21, 0.08, 0, Math.PI * 2); c.stroke();
  }),
  // a woman, head and shoulders: blonde hair gathered up, pale face, white blouse,
  // on a warm pale ground
  portraitB: () => canvasTex(360, 420, 44, (c, r, dab, W, H) => {
    c.fillStyle = '#e8e4c8'; c.fillRect(0, 0, W, H);
    for (let i = 0; i < 520; i++) dab(r() * W, r() * H, 1.5 + (r() - 0.5) * 0.7, 26, 9, ['#e8e4c8', '#dcdcc0', '#f2efd8', '#d2dccc', '#ece2b8'][i % 5]);
    // blouse
    c.fillStyle = '#f6f4ec'; c.beginPath(); c.moveTo(0, H); c.quadraticCurveTo(W * 0.06, H * 0.66, W * 0.4, H * 0.6);
    c.lineTo(W * 0.62, H * 0.6); c.quadraticCurveTo(W * 0.96, H * 0.68, W, H); c.closePath(); c.fill();
    for (let i = 0; i < 240; i++) { const x = W * 0.05 + r() * W * 0.9, y = H * 0.64 + r() * H * 0.36; dab(x, y, 1.3 + (x / W - 0.5) * 0.9, 20, 8, ['#f6f4ec', '#e4e4dc', '#fbfaf4', '#d6dad4', '#eceadc'][i % 5]); }
    c.strokeStyle = 'rgba(150,160,170,0.6)'; c.lineWidth = 3; c.beginPath(); c.moveTo(W * 0.42, H * 0.62); c.quadraticCurveTo(W * 0.5, H * 0.7, W * 0.6, H * 0.62); c.stroke();
    // neck and face
    c.fillStyle = '#efd2aa'; c.fillRect(W * 0.44, H * 0.5, W * 0.12, H * 0.12);
    c.fillStyle = '#f6dcb6'; c.beginPath(); c.ellipse(W * 0.5, H * 0.4, W * 0.15, H * 0.16, 0, 0, Math.PI * 2); c.fill();
    for (let i = 0; i < 130; i++) { const a = r() * 6.28, d = Math.sqrt(r()); dab(W * 0.5 + Math.cos(a) * W * 0.13 * d, H * 0.4 + Math.sin(a) * H * 0.14 * d, a + 1.57, 8, 5, ['#f6dcb6', '#efcca0', '#fae6c8', '#eec0a0'][i % 4]); }
    // hair: parted, gathered high, falling to the shoulders on both sides
    c.fillStyle = '#e8c650'; c.beginPath(); c.ellipse(W * 0.5, H * 0.27, W * 0.19, H * 0.11, 0, Math.PI, Math.PI * 2); c.fill();
    c.beginPath(); c.ellipse(W * 0.34, H * 0.4, W * 0.05, H * 0.13, 0.1, 0, 7); c.fill(); c.beginPath(); c.ellipse(W * 0.66, H * 0.4, W * 0.05, H * 0.13, -0.1, 0, 7); c.fill();
    c.beginPath(); c.ellipse(W * 0.5, H * 0.14, W * 0.1, H * 0.06, 0, 0, 7); c.fill();
    for (let i = 0; i < 170; i++) { const a = r() * 6.28; if (Math.sin(a) > 0.35) continue; dab(W * 0.5 + Math.cos(a) * W * 0.17, H * 0.32 + Math.sin(a) * H * 0.17, a + 1.57, 14, 6, ['#ecd060', '#d8b040', '#f6e490', '#c8a038'][i % 4]); }
    // features
    c.lineCap = 'round';
    c.strokeStyle = '#a07a44'; c.lineWidth = 2.5; for (const x of [0.44, 0.56]) { c.beginPath(); c.moveTo(W * (x - 0.035), H * 0.36); c.quadraticCurveTo(W * x, H * 0.35, W * (x + 0.035), H * 0.36); c.stroke(); }
    c.fillStyle = '#3a4a6a'; for (const x of [0.44, 0.56]) { c.beginPath(); c.ellipse(W * x, H * 0.385, 4.5, 3, 0, 0, 7); c.fill(); }
    c.strokeStyle = '#c08a66'; c.lineWidth = 2.5; c.beginPath(); c.moveTo(W * 0.5, H * 0.4); c.lineTo(W * 0.51, H * 0.45); c.lineTo(W * 0.48, H * 0.455); c.stroke();
    c.strokeStyle = '#c45a4a'; c.lineWidth = 4; c.beginPath(); c.moveTo(W * 0.46, H * 0.49); c.quadraticCurveTo(W * 0.5, H * 0.505, W * 0.54, H * 0.49); c.stroke();
    c.fillStyle = 'rgba(232,150,130,0.35)'; for (const x of [0.4, 0.6]) { c.beginPath(); c.ellipse(W * x, H * 0.45, 12, 8, 0, 0, 7); c.fill(); }
    c.strokeStyle = 'rgba(70,80,100,0.5)'; c.lineWidth = 3; c.beginPath(); c.ellipse(W * 0.5, H * 0.37, W * 0.2, H * 0.24, 0, 0, Math.PI * 2); c.stroke();
  }),
  // two drawings pinned unframed: lemon paper, sketched in dark green and blue
  drawingA: () => canvasTex(220, 300, 45, (c, r, dab, W, H) => {
    c.fillStyle = '#f2ec9c'; c.fillRect(0, 0, W, H);
    for (let i = 0; i < 180; i++) dab(r() * W, r() * H, 1.4, 30, 8, ['#f2ec9c', '#ebe28a', '#f8f2b8', '#e6e090'][i % 4]);
    c.strokeStyle = '#2f5a3a'; c.lineWidth = 5; c.lineCap = 'round';
    c.beginPath(); c.moveTo(W * 0.32, H * 0.22); c.lineTo(W * 0.6, H * 0.2); c.stroke();
    c.beginPath(); c.moveTo(W * 0.36, H * 0.26); c.lineTo(W * 0.38, H * 0.78); c.lineTo(W * 0.62, H * 0.8); c.stroke();
    c.strokeStyle = '#7a9a5a'; c.lineWidth = 4;
    c.beginPath(); c.moveTo(W * 0.5, H * 0.3); c.lineTo(W * 0.52, H * 0.7); c.stroke();
    c.strokeStyle = 'rgba(40,60,50,0.55)'; c.lineWidth = 3; c.strokeRect(2, 2, W - 4, H - 4);
  }),
  drawingB: () => canvasTex(320, 230, 46, (c, r, dab, W, H) => {
    c.fillStyle = '#f2ec9c'; c.fillRect(0, 0, W, H);
    for (let i = 0; i < 220; i++) dab(r() * W, r() * H, 0.2, 32, 8, ['#f2ec9c', '#ebe28a', '#f8f2b8', '#e6e090'][i % 4]);
    c.lineCap = 'round';
    c.strokeStyle = '#9aa060'; c.lineWidth = 3; c.strokeRect(W * 0.1, H * 0.22, W * 0.8, H * 0.6);
    for (let i = 0; i < 8; i++) {
      const x = W * (0.16 + i * 0.095) + (r() - 0.5) * 6, top = H * (0.4 + r() * 0.15);
      c.strokeStyle = i % 2 ? '#2f5a6a' : '#2a4a5a'; c.lineWidth = 6 + r() * 3;
      c.beginPath(); c.moveTo(x, top); c.lineTo(x + 2, H * 0.78); c.stroke();
    }
    c.strokeStyle = 'rgba(40,60,50,0.55)'; c.lineWidth = 3; c.strokeRect(2, 2, W - 4, H - 4);
  }),
  // the mirror: bright cream-white glass with a pale green streak down the left
  mirror: () => canvasTex(200, 300, 31, (c, r, dab, W, H) => {
    c.fillStyle = '#f6f6ea'; c.fillRect(0, 0, W, H);
    for (let i = 0; i < 160; i++) dab(r() * W, r() * H, 1.5 + (r() - 0.5) * 0.3, 50, 9, ['#fbfbf4', '#eeeedc', '#f6f4e2', '#ffffff'][i % 4]);
    for (let i = 0; i < 30; i++) dab(W * 0.08 + r() * W * 0.12, r() * H, 1.57, 40, 7, ['#b8d4a8', '#9cc49a', '#cfe2c0'][i % 3]);
    for (let i = 0; i < 16; i++) dab(W * 0.85 + r() * W * 0.1, r() * H, 1.57, 30, 5, '#d8e0d8');
  }),
};

// A garment hung from a peg: narrow at the collar, out to the shoulders, then
// falling in folds to an uneven hem. Origin at the peg.
function garment(collar, shoulder, hem, h, mat, folds, depth, seed) {
  const g = new THREE.PlaneGeometry(1, h, 26, 34); const p = g.attributes.position; const r = rng(seed);
  const ph = r() * 6, hemWave = r() * 6;
  for (let i = 0; i < p.count; i++) {
    const u = p.getX(i), t = 0.5 - p.getY(i) / h; // u -0.5..0.5, t 0 at top
    const width = t < 0.14 ? collar + (shoulder - collar) * Math.sin((t / 0.14) * Math.PI / 2) : shoulder + (hem - shoulder) * ((t - 0.14) / 0.86);
    const x = u * width;
    let y = -t * h;
    if (t > 0.92) y += Math.sin(u * 9 + hemWave) * 0.03 * (t - 0.92) / 0.08; // ragged hem
    const z = Math.sin(u * Math.PI * folds + ph + t * 2) * depth * (0.25 + t) + Math.sin(u * 23 + t * 7) * depth * 0.2 + t * t * 0.05;
    p.setXYZ(i, x, y, z);
  }
  g.computeVertexNormals();
  const m = new THREE.Mesh(worldUV(g, 0.5), mat.clone()); m.material.side = THREE.DoubleSide; return m;
}

// ---------------------------------------------------------------------------
// Things on the walls.
// picture on a wall: anchor point, inward normal, size; tilt rotates it in the wall plane
function hangPicture(tex, w, h, frameMat, pos, nx, nz, { tilt = 0, lean = 0.04, string = null, frame = 0.035, glow = 0.35, gap = 0.004 } = {}) {
  const g = group(pos[0], pos[1], pos[2], faceYaw(nx, nz));
  const tg = new THREE.Group(); tg.rotation.z = tilt; g.add(tg);
  const inner = new THREE.Group(); inner.rotation.x = lean; inner.position.z = Math.sin(lean) * h / 2 + gap; tg.add(inner);   // small air gap so the frame never touches the wall
  // picture surfaces carry their own colour as light, so pale papers and canvases stay pale
  const cm = new THREE.MeshStandardMaterial({ map: tex, roughness: 0.85, emissive: new THREE.Color('#ffffff'), emissiveMap: tex, emissiveIntensity: glow });
  mesh(new THREE.PlaneGeometry(w, h), cm, 0, 0, 0.016, inner);
  if (frame) {
    const f = frame, fd = 0.028;
    // mitred look: the two long rails run the full width, the sides sit between them, all flush at the back
    box(w + 2 * f, f, fd, frameMat, 0, h / 2 + f / 2, fd / 2, inner, 0.3);
    box(w + 2 * f, f, fd, frameMat, 0, -h / 2 - f / 2, fd / 2, inner, 0.3);
    box(f, h, fd, frameMat, -w / 2 - f / 2, 0, fd / 2, inner, 0.3);
    box(f, h, fd, frameMat, w / 2 + f / 2, 0, fd / 2, inner, 0.3);
    box(w, h, 0.01, M.canvasBack, 0, 0, 0.007, inner, 0.4);   // the back of the canvas, behind the painted face
  } else {
    // an unframed sheet: a little card thickness so it has an edge
    box(w, h, 0.006, M.paperEdge, 0, 0, 0.01, inner, 0.3);
  }
  if (string) { // a cord from the back of both top corners up to a nail
    const top = h / 2 + frame, zt = Math.sin(lean) * h - 0.004;
    cord([-w / 2 - frame * 0.4, top - 0.01, zt], [0, top + string, 0.008], tg, 0.0035);
    cord([w / 2 + frame * 0.4, top - 0.01, zt], [0, top + string, 0.008], tg, 0.0035);
    const nail = new THREE.Mesh(new THREE.SphereGeometry(0.011, 8, 6), M.string); nail.position.set(0, top + string, 0.008); tg.add(nail);
  }
  return g;
}

function buildWallThings() {
  const zb = ROOM.zB - 0.015;
  // back wall: the mirror on its cord, left of the window
  hangPicture(PICTURES.mirror(), 0.33, 0.53, M.blackFrame, [-2.07, 2.08, zb], 0, -1, { string: 0.32, frame: 0.03, lean: 0.04, glow: 0.6 }).userData.exportName = 'Mirror';
  // the landscape over the peg rail
  hangPicture(PICTURES.landscape(), 0.72, 0.5, M.frame, [0.12, 2.3, zb], 0, -1, { string: 0.33, frame: 0.032, lean: 0.04, glow: 0.45 }).userData.exportName = 'Picture_Landscape';
  // bed wall: two portraits high up, hung crooked, and two drawings pinned below
  const onRight = (z, inset = 0.015) => [rightAt(z) + R_IN.x * inset, 0, z + R_IN.z * inset];
  // turned toward the painter, each picture needs room so its far edge stays clear of the slanting wall
  const pr = (z, y) => { const p = onRight(z, 0.012); p[1] = y; return p; };   // flat against the slanted wall
  const hangR = (tex, w, h, mat, z, y, opts) => { const p = pr(z, y); return hangPicture(tex, w, h, mat, p, R_IN.x, R_IN.z, opts); };
  hangR(PICTURES.portraitA(), 0.42, 0.55, M.frameDark, 3.8, 2.72, { tilt: 0, lean: 0, frame: 0.026, glow: 0.45, gap: 0.015 }).userData.exportName = 'Picture_PortraitMan';
  hangR(PICTURES.portraitB(), 0.46, 0.52, M.frameDark, 2.78, 2.6, { tilt: 0, lean: 0, frame: 0.026, glow: 0.45, gap: 0.015 }).userData.exportName = 'Picture_PortraitWoman';
  hangR(PICTURES.drawingA(), 0.38, 0.54, null, 3.33, 1.8, { tilt: -0.26, lean: 0.0, frame: 0, glow: 0.55 }).userData.exportName = 'Picture_DrawingLeft';
  hangR(PICTURES.drawingB(), 0.54, 0.4, null, 2.36, 1.74, { tilt: -0.2, lean: 0.0, frame: 0, glow: 0.55 }).userData.exportName = 'Picture_DrawingRight';
  // the towel: gathered at a nail near the corner, widening and folding as it falls
  // the painted towel is long, hanging almost to the floor beside the corner
  // The towel, as painted: hung from a small hook, a narrow twisted neck opens into a
  // soft sheet with long vertical folds; its lower part is folded over in front along
  // a slanting crease, the corner of the fold poking out to the left, and the flap
  // narrows to an uneven point. Curved orange-red stripes follow the cloth; every edge
  // and the deepest folds are drawn in dark green-black paint.
  // Local frame: +x is the painter's left, y = 0 at the hook, z out of the wall.
  const towel = new THREE.Group(); towel.userData.exportName = 'Towel';
  {
    const r = rng(1881), nz = (a) => (r() - 0.5) * a;
    const ink = new THREE.MeshBasicMaterial({ color: '#1d2a1c' });
    const line = (pts, rad = 0.0042) => { if (pts.length < 2) return;
      const c = new THREE.CatmullRomCurve3(pts.map(p => new THREE.Vector3(...p)));
      towel.add(new THREE.Mesh(new THREE.TubeGeometry(c, Math.max(8, pts.length * 3), rad, 5, false), ink)); };
    // painted cloth texture: muted yellow-greens in strokes that run along the cloth,
    // darker greens in the valleys, curved orange-red stripes
    const clothMat = (seed, stripes, base = '#c4d394', folds = []) => {
      const W = 256, H = 512, cv = document.createElement('canvas'); cv.width = W; cv.height = H; const c = cv.getContext('2d'), q = rng(seed);
      c.fillStyle = base; c.fillRect(0, 0, W, H); c.lineCap = 'round';
      for (let k = 0; k < 900; k++) { const x = q() * W, y = q() * H, L = 18 + q() * 40;
        c.strokeStyle = jitter(['#d4e0a6', '#bccd88', '#e2e9b8', '#a9bf78', '#97b26a', '#cad89c', '#86a862'][k % 7], q, 0.05); c.lineWidth = 4 + q() * 7;
        c.beginPath(); c.moveTo(x, y); c.quadraticCurveTo(x + (q() - 0.5) * 10, y + L / 2, x + (q() - 0.5) * 8, y + L); c.stroke(); }
      for (let k = 0; k < 40; k++) { const x = q() * W, y = q() * H; c.strokeStyle = `rgba(214,232,180,${0.4 + q() * 0.3})`; c.lineWidth = 3 + q() * 3; c.beginPath(); c.moveTo(x, y); c.lineTo(x + (q() - 0.5) * 6, y + 30 + q() * 40); c.stroke(); }
      for (const [u0, v0, v1] of folds) {            // long fold lines painted in dark green, with a lit edge beside each
        const pts = []; for (let k = 0; k <= 12; k++) { const v = v0 + (v1 - v0) * k / 12; pts.push([W * (u0 + Math.sin(v * 7 + u0 * 9) * 0.05), H * v]); }
        c.strokeStyle = 'rgba(44,72,40,0.85)'; c.lineWidth = 5; c.beginPath(); pts.forEach(([x, y], k) => k ? c.lineTo(x, y) : c.moveTo(x, y)); c.stroke();
        c.strokeStyle = 'rgba(226,236,190,0.7)'; c.lineWidth = 4; c.beginPath(); pts.forEach(([x, y], k) => k ? c.lineTo(x + 7, y) : c.moveTo(x + 7, y)); c.stroke();
      }
      c.strokeStyle = '#cc5530'; for (const [y0, bend, w] of stripes) { c.lineWidth = w; c.beginPath(); c.moveTo(-10, y0 * H); c.bezierCurveTo(W * 0.3, (y0 + bend) * H, W * 0.7, (y0 + bend * 1.6) * H, W + 10, (y0 + bend * 0.4) * H); c.stroke();
        c.strokeStyle = 'rgba(232,120,70,0.6)'; c.lineWidth = w * 0.4; c.stroke(); c.strokeStyle = '#cc5530'; }
      const t = new THREE.CanvasTexture(cv); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 8;
      return new THREE.MeshStandardMaterial({ map: t, vertexColors: true, roughness: 0.95, side: THREE.DoubleSide, emissive: new THREE.Color('#ffffff'), emissiveMap: t, emissiveIntensity: 0.32 });
    };
    // build a cloth panel from rows: edges(s) gives [leftPoint, rightPoint] in x/y, depth(u, s) the z
    const panel = (rows, cols, edges, depth, mat) => {
      const pos = [], uv = [], col = [], idx = [];
      for (let a = 0; a <= rows; a++) { const sN = a / rows, [Lp, Rp] = edges(sN);
        for (let b = 0; b <= cols; b++) { const u = b / cols, x = Lp[0] + (Rp[0] - Lp[0]) * u, y = Lp[1] + (Rp[1] - Lp[1]) * u, z = depth(u, sN);
          pos.push(x, y, z); uv.push(u, 1 - sN);
          const shade = 0.78 + 0.22 * Math.max(0, Math.min(1, 0.5 + (z - depth(Math.min(1, u + 0.05), sN)) * 25)); col.push(shade, shade, shade); } }
      for (let a = 0; a < rows; a++) for (let b = 0; b < cols; b++) { const p = a * (cols + 1) + b; idx.push(p, p + cols + 1, p + 1, p + 1, p + cols + 1, p + cols + 2); }
      const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
      g.setAttribute('color', new THREE.Float32BufferAttribute(col, 3)); g.setIndex(idx); g.computeVertexNormals();
      const m = new THREE.Mesh(g, mat); towel.add(m); return m;
    };
    const wob = (s, k, a) => Math.sin(s * k + 1.3) * a + Math.sin(s * k * 2.7 + 0.4) * a * 0.4;   // hand-drawn irregularity of an edge
    // 1. the sheet: twisted neck, then widening, long vertical folds; ends at the crease
    const cL = [0.16, -0.98], cR = [-0.15, -1.1];                                         // crease ends
    const sheetEdges = s => { const y = -s * 1.0;
      const w = s < 0.3 ? 0.022 + 0.035 * (s / 0.3) : 0.057 + 0.27 * Math.sin(Math.min(1, (s - 0.3) / 0.45) * Math.PI / 2);
      const cx = 0.012 * Math.sin(s * 7) - 0.02 * s;
      let yl = y, yr = y; if (s > 0.9) { const k = (s - 0.9) / 0.1; yl = y + (cL[1] - 0.12 - y) * k; yr = y + (cR[1] - 0.12 - y) * k; }   // continues a little behind the flap, so no gap shows
      const kw = Math.min(1, s * 2.5); return [[cx + w / 2 + wob(s, 9, 0.008) * kw, yl], [cx - w / 2 + wob(s, 11, 0.007) * kw, yr]]; };
    const sheetDepth = (u, s) => { const tw = s < 0.4 ? Math.sin(s * 22) * (1 - s / 0.4) : 0;
      return 0.012 + (u - 0.5) * 0.012 * tw + Math.max(0, s - 0.35) * 0.05 + Math.sin(u * Math.PI * 4 + s * 3) * 0.018 * Math.min(1, Math.max(0, s - 0.3) * 3); };
    panel(40, 12, sheetEdges, sheetDepth, clothMat(51, [[0.1, 0.12, 9], [0.62, 0.1, 7]], '#c4d394', [[0.3, 0.45, 0.98], [0.62, 0.5, 0.98], [0.46, 0.65, 1.0]]));
    // 2. the corner of the fold that pokes out to the painter's left, just under the crease
    const ear = [[0.16, -0.98], [0.25, -1.0], [0.15, -1.1]];
    // 3. the folded-over flap, hanging in front of the sheet to an uneven point
    const tip = [0.06, -1.86];
    const qb = (a, c, b, t) => [(1 - t) * (1 - t) * a[0] + 2 * (1 - t) * t * c[0] + t * t * b[0], (1 - t) * (1 - t) * a[1] + 2 * (1 - t) * t * c[1] + t * t * b[1]];
    const flapEdges = s => { const Lp = qb(cL, [0.18, -1.45], tip, s), Rp = qb(cR, [-0.19, -1.52], tip, s);
      return [[Lp[0] + wob(s, 13, 0.009), Lp[1]], [Rp[0] + wob(s, 10, 0.01), Rp[1] + wob(s, 7, 0.01)]]; };
    const flapDepth = (u, s) => 0.07 + 0.04 * (1 - s) * (1 - s) + Math.sin(u * Math.PI * 2.5 + s * 5) * 0.022 * Math.sin(s * Math.PI) + Math.sin(u * 9 + s * 11) * 0.006;
    panel(30, 12, flapEdges, flapDepth, clothMat(52, [[0.18, 0.2, 10], [0.55, 0.16, 9]], '#bfd08e', [[0.35, 0.05, 0.75], [0.66, 0.1, 0.6]]));
    // a sliver of the underside showing along the flap's right edge: a darker, overlapping layer
    panel(14, 2, s => { const R = flapEdges(s * 0.7)[1]; return [[R[0] + 0.005, R[1]], [R[0] - 0.028 + wob(s, 6, 0.006), R[1] - 0.02]]; },
      (u, s) => 0.05 - u * 0.02, clothMat(53, [], '#7e9c5e'));
    // 4. painted outlines: sheet sides, crease, the ear, flap sides, and the deep folds
    const eS = (side) => { const pts = []; for (let k = 0; k <= 24; k++) { const s = k / 24 * 0.88; const E = sheetEdges(s)[side]; pts.push([E[0], E[1], sheetDepth(side, s) + 0.004]); } return pts; };
    line(eS(0)); line(eS(1), 0.0036);
    line([[cL[0], cL[1], 0.105], [0.0, (cL[1] + cR[1]) / 2 + 0.012, 0.112], [cR[0], cR[1], 0.1]], 0.005);
    line([[ear[0][0], ear[0][1], 0.1], [ear[1][0], ear[1][1], 0.09], [ear[2][0], ear[2][1], 0.085]]);
    const eF = (side) => { const pts = []; for (let k = 0; k <= 24; k++) { const s = k / 24; const E = flapEdges(s)[side]; pts.push([E[0], E[1], flapDepth(side, s) + 0.004]); } return pts; };
    line(eF(0), 0.0048); line(eF(1));
    // the ear itself: a small triangle of cloth
    const eg = new THREE.BufferGeometry(); eg.setAttribute('position', new THREE.Float32BufferAttribute([...ear[0], 0.1, ...ear[1], 0.09, ...ear[2], 0.085].slice(0, 9).length === 9 ? [ear[0][0], ear[0][1], 0.1, ear[1][0], ear[1][1], 0.09, ear[2][0], ear[2][1], 0.085] : [], 3));
    eg.setAttribute('color', new THREE.Float32BufferAttribute([0.85, 0.85, 0.85, 0.85, 0.85, 0.85, 0.85, 0.85, 0.85], 3));
    eg.setAttribute('uv', new THREE.Float32BufferAttribute([0.5, 0.5, 0.6, 0.5, 0.5, 0.6], 2)); eg.computeVertexNormals();
    towel.add(new THREE.Mesh(eg, clothMat(54, [], '#cbd99c')));
    // 5. the hook: a small dark iron hook in the wall, the cloth looped over it
    const hook = new THREE.Mesh(new THREE.TorusGeometry(0.012, 0.0035, 6, 12, Math.PI * 1.3), ink); hook.position.set(0, 0.012, 0.012); hook.rotation.set(0, Math.PI / 2, Math.PI * 0.85); towel.add(hook);
    const pin = new THREE.Mesh(new THREE.CylinderGeometry(0.003, 0.003, 0.02, 6), ink); pin.rotation.x = Math.PI / 2; pin.position.set(0, 0.012, 0.004); towel.add(pin);
    void nz;
  }
  towel.position.set(ROOM.xL + 0.05, 2.42, 3.62); towel.rotation.y = Math.PI / 2 + 0.25; world.add(towel);   // just past the door casing, so the casing doesn't cut through it
  // peg rail behind the head of the bed, with round turned pegs
  box(0.98, 0.07, 0.03, M.peg, 0.12, 1.67, zb - 0.015, world, 0.3);
  for (const x of [-0.3, 0.05, 0.33, 0.58]) {
    const pg = cyl(0.014, 0.014, 0.08, M.peg, x, 1.67, zb - 0.06); pg.rotation.x = Math.PI / 2;
    const kn = new THREE.Mesh(new THREE.SphereGeometry(0.034, 12, 10), M.peg); kn.position.set(x, 1.68, zb - 0.1); kn.scale.z = 0.7; world.add(kn);
  }
  // the pale blue smock hung by its collar from the first peg: full body, a sleeve
  // falling forward on the left, a shorter one tucked behind on the right
  const smock = garment(0.1, 0.36, 0.42, 0.96, M.smock, 4, 0.085, 3); smock.position.set(-0.02, 1.7, zb - 0.1); smock.rotation.set(0, Math.PI, 0.05); smock.userData.exportName = 'Smock'; world.add(smock);
  const sleeveL = garment(0.06, 0.12, 0.13, 0.7, M.smock, 2, 0.04, 4); sleeveL.position.set(-0.15, 1.64, zb - 0.15); sleeveL.rotation.set(-0.05, Math.PI, 0.12); sleeveL.userData.exportName = 'Smock'; world.add(sleeveL);
  const sleeveR = garment(0.05, 0.1, 0.11, 0.5, M.smock, 2, 0.03, 6); sleeveR.position.set(0.12, 1.62, zb - 0.08); sleeveR.rotation.set(0, Math.PI, -0.15); sleeveR.userData.exportName = 'Smock'; world.add(sleeveR);
  // darker blue clothes bunched over the second peg, a pale blue cloth beside them
  const c2 = garment(0.07, 0.2, 0.22, 0.3, M.clothDark, 4, 0.07, 7); c2.position.set(0.3, 1.73, zb - 0.13); c2.rotation.y = Math.PI; c2.userData.exportName = 'Clothes_Dark'; world.add(c2);
  const c3 = garment(0.05, 0.13, 0.16, 0.26, M.smock, 3, 0.05, 8); c3.position.set(0.42, 1.72, zb - 0.16); c3.rotation.y = Math.PI; c3.userData.exportName = 'Cloth_Pale'; world.add(c3);
  // the straw hat hooked on the last peg: crown up and toward the room, brim tipped, dark band
  // hung by its brim on the last peg, as painted
  const hat = group(0.7, 1.73, zb - 0.16); hat.rotation.set(-0.9, 0, -0.7); hat.scale.setScalar(1.3); hat.userData.exportName = 'Hat';   // crown up and to the right, brim sweeping down to the left
  lathe([[0, 0.11], [0.05, 0.11], [0.075, 0.095], [0.085, 0.03], [0.09, 0.01], [0.16, 0.0], [0.19, 0.012], [0.195, 0.02]], M.straw, 0, 0, 0, hat, 28);
  cyl(0.087, 0.09, 0.03, M.hatBand, 0, 0.028, 0, hat, 24);
}
