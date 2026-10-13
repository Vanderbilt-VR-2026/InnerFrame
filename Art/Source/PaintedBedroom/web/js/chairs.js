// Painted Bedroom: The two rush-seated chairs (one builder, two placements in app.js).
// Unity objects: Chair_Front (chairA), Chair_Back (chairB).
// Loaded as a plain script after core.js; all files share one scope (see index.html).
"use strict";

// This object's materials, added to the shared palette M by buildMaterials() in core.js.
function chairsMaterials() {
  // chairs: lemon yellow with orange-ochre shading
  M.chairWood = chairWoodMat(7);
  M.chairWoodH = chairWoodMat(71);
  M.rush = rushMat(8);
}

// Colour grading measured against the painting for these materials (see GRADE in core.js).
Object.assign(GRADE, {
  rush: { dL: 0, kC: 0.92, dh: -2 },
  chairWood: { dL: -3, kC: 0.95, dh: -2 },
  chairWoodH: { dL: -3, kC: 0.95, dh: -2 },
});

// Fit to the painting: offset, scale and extra height for this object (see DISTORT in core.js).
DISTORT.chairA = { dx: 0.022, dz: -0.025, s: 0.986, sy: 1.154 };
DISTORT.chairB = { dx: 0.000, dz: 0.000, s: 1.150, sy: 0.930 };

// A rush seat is woven from the corners inward: strands run parallel to each
// edge, so they form nested squares with seams along both diagonals and a dark
// knot of shadow in the middle.
// The rush seat as Van Gogh painted it: an olive-yellow cushion of strands, with
// dark olive strokes radiating from a shadowed centre and pale yellow catching
// the light on the crown of the bulge.
// The woven rush seat. A rush seat is woven from the four corners toward the
// middle, so the strands in each quarter run parallel to that quarter's edge and
// meet the neighbouring quarter along a diagonal. Van Gogh painted this as
// muted yellow-green strands crossed by a few dark olive strokes that radiate
// from a shadowed hollow in the centre.
function rushMat(seed) {
  const S = 512, r = rng(seed), C = S / 2;
  const col = document.createElement('canvas'); col.width = col.height = S;
  const hgt = document.createElement('canvas'); hgt.width = hgt.height = S;
  const c = col.getContext('2d'), h = hgt.getContext('2d');
  c.fillStyle = '#b8a83a'; c.fillRect(0, 0, S, S); h.fillStyle = '#707070'; h.fillRect(0, 0, S, S);
  c.lineCap = h.lineCap = 'round';
  const light = ['#d6c650', '#cfc04a', '#e0d264', '#c8bc48'], mid = ['#b9ab3c', '#aea23a', '#c2b442'], dark = ['#8e8a34', '#7e7c30', '#96903a'];
  // four quarters: strands parallel to the edge, from the rim inward
  const rows = 34;
  for (let q = 0; q < 4; q++) {
    const a = q * Math.PI / 2, ca = Math.cos(a), sa = Math.sin(a);
    const P = (u, v) => [C + ca * u - sa * v, C + sa * u + ca * v]; // u along the edge, v toward the rim
    for (let k = 0; k < rows; k++) {
      const v = C - (k + 0.5) * (C / rows);           // distance from centre to this strand
      const half = v;                                   // the strand ends on the two diagonals
      const t = k / rows;                               // 0 at the rim, 1 at the centre
      const pal = t < 0.25 ? mid : t < 0.7 ? light : dark;
      // each strand is laid in a few overlapping strokes with small wobbles
      for (let u = -half; u < half; u += 26 + r() * 22) {
        const e = Math.min(half, u + 30 + r() * 18), wob = (r() - 0.5) * 2.5;
        const [x0, y0] = P(u, v + wob), [x1, y1] = P(e, v - wob);
        c.strokeStyle = jitter(pal[(r() * pal.length) | 0], r, 0.06); c.lineWidth = C / rows * (0.85 + r() * 0.3);
        c.beginPath(); c.moveTo(x0, y0); c.lineTo(x1, y1); c.stroke();
        const g = 130 + r() * 90 | 0; h.strokeStyle = `rgb(${g},${g},${g})`; h.lineWidth = C / rows * 0.7;
        h.beginPath(); h.moveTo(x0, y0); h.lineTo(x1, y1); h.stroke();
      }
      // the gap between strands, darker
      const [gx0, gy0] = P(-half, v - C / rows / 2), [gx1, gy1] = P(half, v - C / rows / 2);
      c.strokeStyle = 'rgba(110,104,40,0.35)'; c.lineWidth = 1.2; c.beginPath(); c.moveTo(gx0, gy0); c.lineTo(gx1, gy1); c.stroke();
    }
  }
  // where the quarters meet: subtle seams along both diagonals
  c.strokeStyle = 'rgba(112,108,40,0.55)'; c.lineWidth = 3;
  for (const [a, b] of [[[6, 6], [S - 6, S - 6]], [[S - 6, 6], [6, S - 6]]]) { c.beginPath(); c.moveTo(...a); c.lineTo(...b); c.stroke(); }
  // Van Gogh's dark olive strokes radiating from the hollow, irregular in length and weight
  for (let i = 0; i < 26; i++) {
    const a = (i / 26) * Math.PI * 2 + (r() - 0.5) * 0.5, d0 = 14 + r() * 40, d1 = d0 + 50 + r() * 150;
    c.strokeStyle = `rgba(${92 + r() * 24 | 0},${88 + r() * 20 | 0},${28 + r() * 12 | 0},${0.5 + r() * 0.3})`; c.lineWidth = 8 + r() * 10;
    const bend = (r() - 0.5) * 0.25;
    c.beginPath(); c.moveTo(C + Math.cos(a) * d0, C + Math.sin(a) * d0);
    c.quadraticCurveTo(C + Math.cos(a + bend) * (d0 + d1) / 2, C + Math.sin(a + bend) * (d0 + d1) / 2, C + Math.cos(a) * d1, C + Math.sin(a) * d1); c.stroke();
  }
  const g1 = c.createRadialGradient(C, C, 2, C, C, S * 0.14);
  g1.addColorStop(0, 'rgba(84,84,30,0.45)'); g1.addColorStop(1, 'rgba(84,84,30,0)');
  c.fillStyle = g1; c.fillRect(0, 0, S, S);
  // olive patches scattered over the cushion: Van Gogh's seat is lumpy, not a neat star
  for (let i = 0; i < 70; i++) {
    const a = r() * Math.PI * 2, d = 40 + r() * S * 0.4, x = C + Math.cos(a) * d, y = C + Math.sin(a) * d, t = a + Math.PI / 2 + (r() - 0.5) * 0.8;
    c.strokeStyle = r() < 0.6 ? `rgba(120,116,40,${0.4 + r() * 0.3})` : `rgba(236,222,110,${0.35 + r() * 0.3})`; c.lineWidth = 10 + r() * 14;
    c.beginPath(); c.moveTo(x - Math.cos(t) * 14, y - Math.sin(t) * 14); c.lineTo(x + Math.cos(t) * 14, y + Math.sin(t) * 14); c.stroke();
  }
  // a darker rim where the rush rolls over the frame
  const g2 = c.createRadialGradient(C, C, S * 0.38, C, C, S * 0.72);
  g2.addColorStop(0, 'rgba(110,100,36,0)'); g2.addColorStop(1, 'rgba(110,100,36,0.55)');
  c.fillStyle = g2; c.fillRect(0, 0, S, S);
  const t = toTextures({ col, hgt }, false);
  return new THREE.MeshStandardMaterial({ map: t.map, bumpMap: t.bump, bumpScale: 2.2, roughness: 0.95, metalness: 0, emissive: new THREE.Color('#ffffff'), emissiveMap: t.map, emissiveIntensity: 0.3 });
}

// ---------------------------------------------------------------------------
// Chair wood: strokes run along the length of each part (the texture's v
// direction), warm yellow over ochre, with a few brown and blue-green accents
// down one side of the limb where Van Gogh drew his contours.
function chairWoodMat(seed) {
  const o = strokeCanvas({ seed, size: 256, base: '#dcaa30', cols: ['#e8c048', '#f2d460', '#d09a28', '#c58c24', '#e2b43a', '#f6dc74'],
    strokes: 900, angle: Math.PI / 2, spread: 0.06, len: [40, 110], wid: [4, 9], vary: 0.05,
    after: (c, h, S, r) => {
      for (let i = 0; i < 26; i++) {           // contour accents, mostly on one side of the limb
        const x = (r() < 0.75 ? 0.04 + r() * 0.1 : 0.55 + r() * 0.1) * S, y = r() * S;
        c.strokeStyle = r() < 0.6 ? 'rgba(120,70,20,0.75)' : 'rgba(60,120,100,0.6)'; c.lineWidth = 2 + r() * 3;
        c.beginPath(); c.moveTo(x, y); c.lineTo(x + (r() - 0.5) * 3, y + 30 + r() * 70); c.stroke();
      }
    } });
  const t = toTextures(o);
  return new THREE.MeshStandardMaterial({ map: t.map, bumpMap: t.bump, bumpScale: 1.2, roughness: 0.95, metalness: 0, emissive: new THREE.Color('#ffffff'), emissiveMap: t.map, emissiveIntensity: 0.3 });
}

// A limb between two points, slightly tapered and gently irregular, with UVs that
// run along its length so the brushstrokes follow the wood. 'flat' squashes the
// cross-section (1 = round); 'face' orients the wide side.
function limb(a, b, r0, r1, mat, parent, seed = 1, wobble = 0.004, flat = 1) {
  const A = new THREE.Vector3(...a), B = new THREE.Vector3(...b), d = B.clone().sub(A), L = d.length();
  const seg = 12, rows = 6, g = new THREE.CylinderGeometry(1, 1, L, seg, rows, false);
  const p = g.attributes.position, uv = g.attributes.uv, rr = rng(seed);
  const bend = [(rr() - 0.5) * wobble, (rr() - 0.5) * wobble];
  for (let i = 0; i < p.count; i++) {
    const y = p.getY(i), t = y / L + 0.5;
    const rad = r0 + (r1 - r0) * t, bow = Math.sin(t * Math.PI);
    const ang = Math.atan2(p.getZ(i), p.getX(i)), lump = 1 + Math.sin(ang * 3 + seed) * 0.05 + Math.sin(t * 9 + seed) * 0.03;
    p.setXYZ(i, p.getX(i) * rad * lump + bend[0] * bow, y, p.getZ(i) * rad * lump * flat + bend[1] * bow);
    uv.setXY(i, (ang / (Math.PI * 2)) * (2 * Math.PI * rad) / 0.12, y / 0.45);
  }
  g.computeVertexNormals();
  const m = new THREE.Mesh(g, mat); m.position.copy(A).addScaledVector(d, 0.5);
  m.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), d.normalize()); parent.add(m); return m;
}

// The Arles rush-seated chair, proportioned from the painted chair beside the
// washstand: stout legs about 5 cm thick; back posts that are flattened and a
// little wider than the legs, rising to a level top; a broad top rail with a
// gently arched upper edge, flush with the post tops, and two more broad slats
// below it, each about as tall as the gap between them; a thick, plump straw
// cushion that overhangs the frame with a visible rolled front edge; and
// stretchers set low between the legs. Each chair is slightly irregular.
// The chair faces local +z.
function buildChair(x, z, yaw, scale = 1, seed = 1) {
  const g = group(x, 0, z, yaw); g.scale.setScalar(scale);
  const r = rng(seed * 97 + 13), j = (a) => (r() - 0.5) * a;
  const wood = M.chairWood, sh = 0.42;                          // top of the seat rails
  const fw = 0.44, bw = 0.38, sd = 0.38;
  const FL = [-fw / 2 + j(0.008), sd / 2], FR = [fw / 2 + j(0.008), sd / 2];
  const BL = [-bw / 2 + j(0.008), -sd / 2], BR = [bw / 2 + j(0.008), -sd / 2];
  const lr = 0.031;   // the painted legs are stout: about a seventh of the seat width
  // front legs, slightly splayed, a touch thicker at the foot
  limb([FL[0] - 0.01, 0, FL[1] + 0.01], [FL[0], sh, FL[1]], lr * 1.08, lr * 0.95, wood, g, seed + 1, 0.006);
  limb([FR[0] + 0.01, 0, FR[1] + 0.01], [FR[0], sh, FR[1]], lr * 1.08, lr * 0.95, wood, g, seed + 2, 0.006);
  // back legs rise straight into flattened posts that rake back above the seat
  const top = 0.96 + j(0.015), rake = 0.08;
  const postTop = [];
  for (const [P, sgn, k] of [[BL, -1, 3], [BR, 1, 4]]) {
    limb([P[0] + sgn * 0.006, 0, P[1] + 0.008], [P[0], sh, P[1]], lr * 1.1, lr, wood, g, seed + k, 0.006, 0.8);
    const tp = [P[0] + sgn * 0.004 + j(0.006), top, P[1] - rake];
    limb([P[0], sh, P[1]], tp, lr * 1.02, lr * 0.95, wood, g, seed + k + 10, 0.012, 0.7);
    postTop.push(tp);
    // the post top: cut level and slightly rounded
    const cap = new THREE.Mesh(new THREE.SphereGeometry(lr * 0.95, 12, 8, 0, Math.PI * 2, 0, Math.PI / 2), wood);
    cap.position.set(tp[0], top - 0.002, tp[2]); cap.scale.set(1, 0.35, 0.7); g.add(cap);
  }
  // three broad slats; the top rail is flush with the post tops and arched along its upper edge
  const slat = (yTop, hgt, arch, k) => {
    const f = (yTop - sh) / (top - sh), zz = -sd / 2 - rake * f;
    const w = bw + 0.004 + j(0.006), bow = 0.028, th = 0.02;
    // a subdivided board, bent toward the back so the curve is smooth across its face;
    // the top rail's upper edge rises into a shallow arch
    const sg = new THREE.BoxGeometry(w, hgt, th, 16, 2, 1); const p = sg.attributes.position;
    for (let i = 0; i < p.count; i++) {
      const u = p.getX(i) / (w / 2), yv = p.getY(i);
      const lift = yv > 0 ? arch * (1 - u * u) : 0;
      p.setXYZ(i, p.getX(i), yv - hgt / 2 + lift, p.getZ(i) - bow * (1 - u * u));
    }
    sg.computeVertexNormals(); worldUV(sg, 0.45, true);
    const m = new THREE.Mesh(sg, M.chairWoodH); m.position.set(j(0.004), yTop, zz); m.rotation.set(-0.06, 0, j(0.03)); g.add(m);
  };
  const sh2 = 0.068;
  slat(top - 0.004, sh2 + 0.008, 0.012, 0);
  slat(top - 0.004 - sh2 - 0.065, sh2, 0.0, 1);
  slat(top - 0.004 - 2 * (sh2 + 0.065), sh2, 0.0, 2);
  // stretchers: two across the front, two along each side, one across the back
  const st = 0.016;
  const at = (P, y, out) => [P[0] + Math.sign(P[0]) * out, y, P[1] + (P[1] > 0 ? out : -out * 0.3)];
  for (const y of [0.15, 0.27]) limb(at(FL, y + j(0.012), 0.009), at(FR, y + j(0.012), 0.009), st, st, wood, g, seed + 20 + y * 100, 0.003);
  limb(at(BL, 0.2, 0.008), at(BR, 0.2, 0.008), st, st, wood, g, seed + 30, 0.003);
  for (const [P, Q] of [[FL, BL], [FR, BR]]) for (const y of [0.19, 0.31]) limb(at(P, y + j(0.012), 0.009), at(Q, y + 0.012 + j(0.012), 0.008), st, st, wood, g, seed + 40 + y * 100, 0.003);
  // seat rails, mostly covered by the rush
  limb([FL[0], sh, FL[1]], [FR[0], sh, FR[1]], 0.018, 0.018, wood, g, seed + 50, 0.002);
  limb([BL[0], sh, BL[1]], [BR[0], sh, BR[1]], 0.018, 0.018, wood, g, seed + 51, 0.002);
  limb([FL[0], sh, FL[1]], [BL[0], sh, BL[1]], 0.018, 0.018, wood, g, seed + 52, 0.002);
  limb([FR[0], sh, FR[1]], [BR[0], sh, BR[1]], 0.018, 0.018, wood, g, seed + 53, 0.002);
  // the straw cushion: a closed, plump trapezoid that overhangs the rails, with
  // rounded corners, a thick rolled edge and a shallow hollow in the middle
  const OV = 0.035, H0 = -0.03, H1 = 0.075;
  const cg = new THREE.SphereGeometry(1, 40, 24); const cp = cg.attributes.position;
  for (let i = 0; i < cp.count; i++) {
    let u = cp.getX(i), y = cp.getY(i), v = cp.getZ(i);
    const sq = q => Math.sign(q) * Math.pow(Math.abs(q), 0.45);   // squarish plan, rounded corners
    u = sq(u); v = sq(v);
    const tz = (v + 1) / 2, wHere = bw + (fw - bw) * tz + 2 * OV;
    const top = y > 0;
    const crown = H1 * (1 - 0.35 * (u * u + v * v)) - 0.022 * Math.exp(-(u * u + v * v) * 6);
    const yy = top ? H0 + (crown - H0) * Math.pow(y, 0.35) : H0 + y * 0.012;
    cp.setXYZ(i, u * wHere / 2 + j(0.002), sh + 0.01 + yy, v * (sd + 2 * OV) / 2);
  }
  cg.computeVertexNormals();
  // straw texture: top face projected from above, sides wrap round
  const uvs = cg.attributes.uv; for (let i = 0; i < uvs.count; i++) { const X = cp.getX(i), Z = cp.getZ(i); uvs.setXY(i, X / (fw + 2 * OV) + 0.5, Z / (sd + 2 * OV) + 0.5); }
  const seat = new THREE.Mesh(cg, M.rush); seat.rotation.y = j(0.04); g.add(seat);
  return g;
}
