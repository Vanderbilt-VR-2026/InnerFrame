// Painted Bedroom: The bed, its pillows and blanket.
// Unity objects: Bed, Pillow_Back, Pillow_Front, Blanket.
// Loaded as a plain script after core.js; all files share one scope (see index.html).
"use strict";

// This object's materials, added to the shared palette M by buildMaterials() in core.js.
function bedMaterials() {
  // the bed: ochre wood with fine dark grain, not blotchy
  M.bedWood = paintMat({ seed: 4, base: '#d68e28', cols: ['#e0a03a', '#c97e20', '#e8b24c', '#b86e1c'], strokes: 2600, angle: Math.PI / 2, spread: 0.08, len: [30, 90], wid: [3, 6], vary: 0.06,
    after: (c, h, S, r) => { for (let i = 0; i < 70; i++) { const x = r() * S, y = r() * S; c.strokeStyle = 'rgba(120,64,14,0.55)'; c.lineWidth = 1.5 + r() * 1.5; c.beginPath(); c.moveTo(x, y); c.lineTo(x + (r() - 0.5) * 4, y + 30 + r() * 90); c.stroke(); } } });
  M.bedPanel = paintMat({ seed: 24, base: '#d48a26', cols: ['#e2a43c', '#c27420', '#eebc58', '#b0661a', '#d89430'], strokes: 3000, angle: Math.PI / 2, spread: 0.06, len: [40, 120], wid: [3, 6], vary: 0.06,
    after: (c, h, S, r) => {
      for (let i = 0; i < 170; i++) { const x = r() * S, y = r() * S; c.strokeStyle = 'rgba(110,56,10,0.6)'; c.lineWidth = 1.2 + r() * 1.8; c.beginPath(); c.moveTo(x, y); c.lineTo(x + (r() - 0.5) * 4, y + 40 + r() * 120); c.stroke(); }
      for (const f of [0.34, 0.68]) { const x = f * S; c.strokeStyle = 'rgba(110,56,10,0.8)'; c.lineWidth = 3; c.beginPath(); c.moveTo(x, 0); c.lineTo(x + 4, S); c.stroke(); h.strokeStyle = '#303030'; h.lineWidth = 4; h.beginPath(); h.moveTo(x, 0); h.lineTo(x + 4, S); h.stroke(); }
    } });
  M.blanket = paintMat({ seed: 5, base: '#d8341c', cols: ['#dc3a1e', '#c92a14', '#e2462a', '#c0260f', '#d63018'], strokes: 1400, angle: 0.6, spread: 1.4, wid: [7, 14], vary: 0.05, bumpScale: 1.2 });
  M.sheet = paintMat({ flat: 0.5, seed: 6, base: '#f4f1d4', cols: ['#faf7e2', '#ebe7b4', '#f6f4e4', '#e2dea0', '#fcf9ea'], strokes: 1500, angle: 0.4, spread: 1.4, wid: [6, 12], vary: 0.05 });
  M.pillow = paintMat({ flat: 0.5, seed: 25, base: '#f4f0d4', cols: ['#f8f6e2', '#ece6b4', '#dcd690', '#fbfaee', '#f0eac6'], strokes: 1300, angle: 0.6, spread: 2.4, len: [20, 50], wid: [10, 18], vary: 0.04, bumpScale: 1.4 });
}

// Colour grading measured against the painting for these materials (see GRADE in core.js).
Object.assign(GRADE, {
  bedWood: { dL: 1.12, kC: 0.933, dh: 2.0 },
  bedPanel: { dL: 1.12, kC: 0.933, dh: 2.0 },
  blanket: { dL: 1.25, kC: 0.795, dh: 3.6 },
  sheet: { dL: -13, kC: 1.98, dh: -14 },
  pillow: { dL: -13, kC: 1.98, dh: -14 },
});

// Fit to the painting: offset, scale and extra height for this object (see DISTORT in core.js).
DISTORT.bed = { dx: 0.000, dz: 0.000, s: 1.000, sy: 1.000 };

// A pillow: a rounded-square ellipsoid, plump in the middle and thin at the edges.
function pillowGeo(w, h, d, seed = 1) {
  const g = new THREE.SphereGeometry(1, 40, 24); const p = g.attributes.position; const r = rng(seed);
  for (let i = 0; i < p.count; i++) {
    let x = p.getX(i), y = p.getY(i), z = p.getZ(i);
    const sq = v => Math.sign(v) * Math.pow(Math.abs(v), 0.55);
    x = sq(x); z = sq(z);
    const edge = Math.max(Math.abs(x), Math.abs(z));
    y = y * (1 - 0.55 * edge * edge);
    p.setXYZ(i, x * w / 2 + Math.sin(z * 5 + seed) * 0.01, y * h / 2 + Math.sin(x * 4 + z * 3 + seed) * 0.012, z * d / 2);
  }
  g.computeVertexNormals();
  return worldUV(g, 0.7);
}

// ---------------------------------------------------------------------------
// The bed: foot at the front, head in the back corner, its long side against
// the slanting wall.
const BED = { x: 1.065, z: 1.786, yaw: -0.159, w: 1.24, l: 1.79, foot: 1.4, head: 1.52 }; // fitted to the painted posts
function buildBed() {
  const g = group(BED.x, 0, BED.z, BED.yaw); OBJ.bed = g;
  const hw = BED.w / 2, L = BED.l, pw = 0.1;
  // square posts with softly rounded tops
  for (const [x, z, ph] of [[-hw, 0, BED.foot], [hw, 0, BED.foot], [-hw, L, BED.head], [hw, L, BED.head]]) {
    rbox(pw, ph, pw, 0.03, M.bedWood, x, ph / 2, z, g, 0.6);
  }
  // footboard: the panel's crest rises above the posts, as in the painting: shoulders
  // just below the post tops, then a broad arch about 15 cm higher
  const fw = BED.w - pw + 0.02;
  const fRise = 0.08, fShoulder = BED.foot - 0.05, fBase = 0.4;  // the painted legs stand clear below the rail
  const foot = archedBoard(fw, fShoulder - fBase + fRise, fRise, 0.055, M.bedPanel, -0.05); foot.position.set(0, fBase, 0); g.add(foot);
  // raised moulding following the arch, with a second line inside it
  const mould = archedBoard(fw - 0.12, fShoulder - fBase + fRise - 0.16, fRise * 0.9, 0.014, M.bedPanel, -0.04); mould.position.set(0, fBase + 0.1, -0.034); g.add(mould);
  const mould2 = archedBoard(fw - 0.2, fShoulder - fBase + fRise - 0.26, fRise * 0.8, 0.014, M.bedPanel, -0.03); mould2.position.set(0, fBase + 0.16, -0.046); g.add(mould2);
  // heavy bottom rail, slightly bowed
  const rg = new THREE.ExtrudeGeometry((() => { const s = new THREE.Shape(); s.moveTo(-BED.w / 2 - 0.02, 0.35); s.quadraticCurveTo(0, 0.32, BED.w / 2 + 0.02, 0.35); s.lineTo(BED.w / 2 + 0.02, 0.47); s.quadraticCurveTo(0, 0.49, -BED.w / 2 - 0.02, 0.47); s.closePath(); return s; })(), { depth: 0.09, bevelEnabled: true, bevelThickness: 0.02, bevelSize: 0.02, bevelSegments: 3, curveSegments: 16 });
  rg.translate(0, 0, -0.1);
  g.add(new THREE.Mesh(worldUV(rg, 0.6), M.bedWood));
  // headboard: same shape, taller, its crest also above the posts
  const hRise = 0.085, hShoulder = BED.head - 0.06, hBase = 0.32;
  const head = archedBoard(fw, hShoulder - hBase + hRise, hRise, 0.055, M.bedPanel, -0.05); head.position.set(0, hBase, L); g.add(head);
  // deep sleigh sides: the lower edge sweeps down toward the foot
  const sideShape = new THREE.Shape(); sideShape.moveTo(0, 0.13);
  sideShape.bezierCurveTo(L * 0.35, 0.16, L * 0.7, 0.3, L, 0.4); sideShape.lineTo(L, 0.62);
  sideShape.bezierCurveTo(L * 0.7, 0.48, L * 0.3, 0.48, 0, 0.58); sideShape.closePath();
  for (const x of [-hw, hw]) {
    const sgeo = new THREE.ExtrudeGeometry(sideShape, { depth: 0.04, bevelEnabled: true, bevelThickness: 0.012, bevelSize: 0.012, bevelSegments: 2, curveSegments: 20 });
    const side = new THREE.Mesh(worldUV(sgeo, 0.9, true), M.bedWood); side.rotation.y = -Math.PI / 2; side.position.set(x + 0.02, 0, 0); g.add(side);
  }
  // mattress under the sheet; the sheet falls over the room side near the pillows
  rbox(BED.w - 0.06, 0.3, L - 0.08, 0.12, M.sheet, 0, 0.62, L / 2, g);
  // two big, soft pillows: the back one heaped against the headboard, the front one lying flatter
  const p1 = new THREE.Mesh(pillowGeo(BED.w - 0.08, 0.3, 0.7, 3), M.pillow); p1.position.set(0.03, 1.14, L - 0.24); p1.rotation.x = 1.0; p1.userData.exportName = 'Pillow_Back'; g.add(p1);
  const p2 = new THREE.Mesh(pillowGeo(BED.w - 0.16, 0.26, 0.62, 5), M.pillow); p2.position.set(-0.05, 1.0, L - 0.55); p2.rotation.set(0.45, 0.08, 0.06); p2.userData.exportName = 'Pillow_Front'; g.add(p2);
  // the turned-down sheet: a broad white fold across the bed
  // the red blanket: a high, soft mound whose top edge curves back on the room side,
  // leaving white sheet showing there, and which drapes deep over that side
  const bw = BED.w - 0.08, bl = 1.12, dr = 0.4;
  const geo = new THREE.PlaneGeometry(bw + dr, bl, 44, 34); const p = geo.attributes.position;
  for (let i = 0; i < p.count; i++) {
    const x = p.getX(i), y = p.getY(i);
    const ex = (x + (bw + dr) / 2) / (bw + dr);                       // 0 at the room-side hem
    // the edge toward the pillows bows out in the middle, leaving white sheet above it
    const yy = y - (y > 0 ? (1 - Math.min(1, ex / 0.45)) * 0.22 * (y / (bl / 2)) : 0); // falls short on the room side, showing the sheet
    let px = x + dr / 2, py = 0.02 + Math.sin(Math.min(1, Math.max(0, ex - 0.2) * 1.5) * Math.PI * 0.5) * 0.2 * Math.max(0.15, Math.cos((yy + bl / 2) / bl * 1.6 - 0.2));
    if (ex < 0.2) { const k = (0.2 - ex) / 0.2; px = -bw / 2 - 0.035 - Math.sin(k * 1.4) * 0.03 + Math.sin(yy * 7) * 0.012; py = -0.22 * k; }   // stays above the dip of the side board instead of passing through it
    p.setXYZ(i, px, py + Math.sin(yy * 8 + x * 3) * 0.008, yy);
  }
  geo.computeVertexNormals();
  const blanket = new THREE.Mesh(worldUV(geo, 0.7), M.blanket.clone()); blanket.material.side = THREE.DoubleSide;
  blanket.position.set(0.0, 0.78, 0.1 + bl / 2); blanket.userData.exportName = 'Blanket'; g.add(blanket);
}
