// Painted Bedroom: Things hanging on the walls: the peg rail with the smock, clothes and hat on its pegs, and the
// towel on its hook by the door. Each piece has its own coordinates: move the rail and the
// clothes and hat together, or they no longer hang on it.
// Unity objects: PegRail, Smock, Clothes_Dark, Cloth_Pale, Hat, Towel.
// Loaded as a plain script after core.js; all files share one scope (see index.html).
"use strict";

// This object's materials, added to the shared palette M by buildMaterials() in core.js.
function hangingsMaterials() {
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
  M.hatBand = new THREE.MeshStandardMaterial({ color: '#1f3050', roughness: 0.8 });
}

// Colour grading measured against the painting for these materials (see GRADE in core.js).
Object.assign(GRADE, {
  smock: { dL: 6.93, kC: 0.702, dh: -28.2 },
  clothDark: { dL: 6.93, kC: 0.702, dh: -28.2 },
  towel: { dL: 0, kC: 1.0, dh: 0 },
});

function buildHangings() {
  const zb = ROOM.zB - 0.015;
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
