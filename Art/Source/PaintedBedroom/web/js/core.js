// Painted Bedroom: Shared by every object: room layout, seeded randomness, brushstroke textures, colour grading (GRADE),
// the material palette (M), geometry helpers, the scene and per-object fit to the painting (DISTORT).
// Loaded as a plain script after core.js; all files share one scope (see index.html).
"use strict";

// ---------------------------------------------------------------------------
// Layout. Coordinates are metres in a left-handed frame like Unity's: +x to the
// painter's right, +z toward the window wall, floor at y = 0. The painter stands
// at the origin with his eye at 1.6 m. Every position below was solved by
// fitting a camera to landmarks in the painting (wall corners, table legs, bed
// posts) and projecting each object back onto the walls and floor.
//
// The room is a trapezoid, as the real room in the Yellow House was: the bed
// wall on the right runs at an angle, so the bed is turned toward the painter.
const ROOM = { xL: -2.39, zF: -0.7, zB: 4.01, h: 3.3, rB: { x: 1.55, z: 4.01 }, rF: { x: 2.30, z: -0.7 }, hR: 3.14 };
const RD = (() => { const dx = ROOM.rF.x - ROOM.rB.x, dz = ROOM.rF.z - ROOM.rB.z, L = Math.hypot(dx, dz); return { x: dx / L, z: dz / L, L }; })();
const R_IN = { x: RD.z, z: -RD.x };                                        // inward normal of the bed wall
const R_ANG = Math.atan2(-RD.z, RD.x);                                       // yaw that lays local +x along the bed wall
const faceYaw = (nx, nz) => Math.atan2(nx, nz);                              // yaw that turns local +z to face (nx, nz)
const rightAt = z => ROOM.rB.x + (z - ROOM.rB.z) * RD.x / RD.z;              // x of the bed wall at depth z
const rightU = z => (z - ROOM.rB.z) / RD.z;                                  // distance along the bed wall from the back corner
const REDUCED = matchMedia('(prefers-reduced-motion: reduce)').matches;

// ---------------------------------------------------------------------------
// Seeded randomness so the room paints the same way every load.
function rng(seed) {
  let a = seed >>> 0;
  return () => {
    a |= 0; a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
const hexToRgb = h => { const n = parseInt(h.slice(1), 16); return [(n >> 16) & 255, (n >> 8) & 255, n & 255]; };
function jitter(hex, r, amt) {
  const [R, G, B] = hexToRgb(hex); const k = 1 + (r() - 0.5) * amt;
  const c = v => Math.max(0, Math.min(255, Math.round(v * k + (r() - 0.5) * 12)));
  return `rgb(${c(R)},${c(G)},${c(B)})`;
}

// ---------------------------------------------------------------------------
// Brushstroke textures: a base tint covered in short, thick, directional
// strokes, with a matching height canvas for impasto relief. Strokes crossing
// an edge are repeated on the far side so each texture tiles.
function strokeCanvas(o) {
  const S = o.size || 512, r = rng(o.seed || 1);
  const col = document.createElement('canvas'); col.width = col.height = S;
  const hgt = document.createElement('canvas'); hgt.width = hgt.height = S;
  const c = col.getContext('2d'), h = hgt.getContext('2d');
  c.fillStyle = o.base; c.fillRect(0, 0, S, S);
  h.fillStyle = '#808080'; h.fillRect(0, 0, S, S);
  c.lineCap = h.lineCap = 'round';
  const n = o.strokes ?? 1600;
  const [l0, l1] = o.len || [14, 46], [w0, w1] = o.wid || [4, 10];
  for (let i = 0; i < n; i++) {
    const x = r() * S, y = r() * S;
    let a = (o.angle || 0) + (r() - 0.5) * (o.spread ?? 0.4);
    if (o.circular) a = Math.atan2(y - S / 2, x - S / 2) + Math.PI / 2 + (r() - 0.5) * 0.3;
    if (o.wave) a += Math.sin(y / S * Math.PI * 2 * o.wave + x / S * 3) * 0.35;
    const L = l0 + r() * (l1 - l0), W = w0 + r() * (w1 - w0);
    const dx = Math.cos(a) * L / 2, dy = Math.sin(a) * L / 2, bend = (r() - 0.5) * L * 0.25;
    const pick = o.pick ? o.pick(x / S, y / S, r) : o.cols[(r() * o.cols.length) | 0];
    const colr = jitter(pick, r, o.vary ?? 0.14);
    const g = 90 + r() * 120 | 0;
    for (const ox of (o.tile === false ? [0] : [-S, 0, S])) for (const oy of (o.tile === false ? [0] : [-S, 0, S])) {
      if (ox && (x + ox < -L || x + ox > S + L)) continue;
      if (oy && (y + oy < -L || y + oy > S + L)) continue;
      const px = x + ox, py = y + oy;
      for (const [ctx, style] of [[c, colr], [h, `rgb(${g},${g},${g})`]]) {
        ctx.strokeStyle = style; ctx.lineWidth = W; ctx.beginPath();
        ctx.moveTo(px - dx, py - dy);
        ctx.quadraticCurveTo(px + bend * Math.sin(a), py + bend * Math.cos(a), px + dx, py + dy);
        ctx.stroke();
      }
      c.strokeStyle = 'rgba(255,255,240,0.10)'; c.lineWidth = Math.max(1, W * 0.25);
      c.beginPath(); c.moveTo(px - dx, py - dy - W * 0.2); c.lineTo(px + dx, py + dy - W * 0.2); c.stroke();
    }
  }
  if (o.after) o.after(c, h, S, r);
  return { col, hgt };
}
function toTextures({ col, hgt }, repeat = true) {
  const map = new THREE.CanvasTexture(col); map.colorSpace = THREE.SRGBColorSpace;
  const bump = new THREE.CanvasTexture(hgt);
  for (const t of [map, bump]) { if (repeat) t.wrapS = t.wrapT = THREE.RepeatWrapping; t.anisotropy = 8; }
  return { map, bump };
}
function paintMat(o, extra = {}) {
  const t = toTextures(strokeCanvas(o), o.tile !== false);
  return new THREE.MeshStandardMaterial({ map: t.map, bumpMap: t.bump, bumpScale: o.bumpScale ?? 2.2, roughness: o.rough ?? 0.82, metalness: 0, emissive: new THREE.Color('#ffffff'), emissiveMap: t.map, emissiveIntensity: o.flat ?? 0.28, ...extra });
}

// Box-projected UVs in metres, so brush size stays constant across objects.
function worldUV(geo, tile = 1, swap = false) {
  const p = geo.attributes.position, n = geo.attributes.normal, uv = new Float32Array(p.count * 2);
  for (let i = 0; i < p.count; i++) {
    const ax = Math.abs(n.getX(i)), ay = Math.abs(n.getY(i)), az = Math.abs(n.getZ(i));
    let u, v;
    if (ax >= ay && ax >= az) { u = p.getZ(i); v = p.getY(i); }
    else if (ay >= az) { u = p.getX(i); v = p.getZ(i); }
    else { u = p.getX(i); v = p.getY(i); }
    if (swap) [u, v] = [v, u];
    uv[i * 2] = u / tile; uv[i * 2 + 1] = v / tile;
  }
  geo.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
  return geo;
}

// Colour grading measured against the painting: for each material, a lightness
// shift (dL), a chroma scale (kC) and a hue rotation (dh, degrees) in CIELAB,
// applied to its painted canvas. Values come from comparing average colours of
// each region in the painter's view with the same region of the painting.
const GRADE = {};   // filled by each object's file (room.js, bed.js, chairs.js, washstand.js, wall.js)
function srgbToLinear(c) { c /= 255; return c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4); }
function linearToSrgb(c) { c = c <= 0.0031308 ? 12.92 * c : 1.055 * Math.pow(c, 1 / 2.4) - 0.055; return Math.max(0, Math.min(255, Math.round(c * 255))); }
function gradeCanvas(cv, g) {
  if (!g || !cv || !cv.getContext) return;
  const ctx = cv.getContext('2d'), im = ctx.getImageData(0, 0, cv.width, cv.height), d = im.data;
  const ch = Math.cos(g.dh * Math.PI / 180), sh = Math.sin(g.dh * Math.PI / 180);
  const fz = t => t > 0.008856 ? Math.cbrt(t) : 7.787 * t + 16 / 116, fi = t => { const t3 = t * t * t; return t3 > 0.008856 ? t3 : (t - 16 / 116) / 7.787; };
  for (let i = 0; i < d.length; i += 4) {
    const r = srgbToLinear(d[i]), gg = srgbToLinear(d[i + 1]), b = srgbToLinear(d[i + 2]);
    const X = (0.4124 * r + 0.3576 * gg + 0.1805 * b) / 0.95047, Y = 0.2126 * r + 0.7152 * gg + 0.0722 * b, Z = (0.0193 * r + 0.1192 * gg + 0.9505 * b) / 1.08883;
    const fx = fz(X), fy = fz(Y), fzz = fz(Z);
    let L = 116 * fy - 16, A = 500 * (fx - fy), B = 200 * (fy - fzz);
    L = Math.max(0, Math.min(100, L + g.dL));
    const A2 = (A * ch - B * sh) * g.kC, B2 = (A * sh + B * ch) * g.kC;
    const ny = (L + 16) / 116, nx = ny + A2 / 500, nz = ny - B2 / 200;
    const X2 = fi(nx) * 0.95047, Y2 = fi(ny), Z2 = fi(nz) * 1.08883;
    d[i] = linearToSrgb(3.2406 * X2 - 1.5372 * Y2 - 0.4986 * Z2);
    d[i + 1] = linearToSrgb(-0.9689 * X2 + 1.8758 * Y2 + 0.0415 * Z2);
    d[i + 2] = linearToSrgb(0.0557 * X2 - 0.2040 * Y2 + 1.0570 * Z2);
  }
  ctx.putImageData(im, 0, 0);
}
function gradeHex(hex, g) {
  if (!g) return hex;
  const cv = document.createElement('canvas'); cv.width = cv.height = 1; const c = cv.getContext('2d');
  c.fillStyle = hex; c.fillRect(0, 0, 1, 1); gradeCanvas(cv, g);
  const d = c.getImageData(0, 0, 1, 1).data; return '#' + [d[0], d[1], d[2]].map(v => v.toString(16).padStart(2, '0')).join('');
}
function gradeMaterials() {
  for (const [k, m] of Object.entries(M)) {
    const g = GRADE[k]; if (!g || !m.map || m.userData.pregraded) continue;
    gradeCanvas(m.map.image, g); m.map.needsUpdate = true;
  }
}

// ---------------------------------------------------------------------------
// Palette, sampled from the painting.
const M = {};
function buildMaterials() {
  // Each object file adds its own materials to M (roomMaterials() in room.js, bedMaterials() in bed.js...).
  roomMaterials(); windowMaterials(); doorsMaterials(); bedMaterials(); chairsMaterials(); washstandMaterials(); washstandItemsMaterials(); picturesMaterials(); hangingsMaterials();
  // shared by several objects (the cord of the pictures; peg wood, also the door and drawer knobs)
  M.string = new THREE.MeshBasicMaterial({ color: '#1f2a40' });
  M.peg = paintMat({ seed: 21, base: '#b4702a', cols: ['#c4823a', '#965a20'], strokes: 400 });
  gradeMaterials();
  for (const [k, m] of Object.entries(M)) m.userData.cat = k;
}

// ---------------------------------------------------------------------------
// Geometry helpers. Everything is built inside a group mirrored on x, which
// turns the left-handed layout numbers into three.js's right-handed space.
const scene = new THREE.Scene();
const world = new THREE.Group(); world.scale.x = -1; scene.add(world);
function mesh(geo, mat, x = 0, y = 0, z = 0, parent = world) { const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z); parent.add(m); return m; }
function box(w, h, d, mat, x, y, z, parent = world, tile = 0.9) { return mesh(worldUV(new THREE.BoxGeometry(w, h, d), tile), mat, x, y, z, parent); }
function rbox(w, h, d, rad, mat, x, y, z, parent = world, tile = 0.9) { return mesh(worldUV(new RoundedBoxGeometry(w, h, d, 3, rad), tile), mat, x, y, z, parent); }
function cyl(r0, r1, h, mat, x, y, z, parent = world, seg = 14) { return mesh(new THREE.CylinderGeometry(r0, r1, h, seg, 1), mat, x, y, z, parent); }
function lathe(profile, mat, x, y, z, parent = world, seg = 28) {
  const m = mesh(new THREE.LatheGeometry(profile.map(([r, h]) => new THREE.Vector2(r, h)), seg), mat, x, y, z, parent);
  m.material = m.material.clone(); m.material.side = THREE.DoubleSide; return m;
}
function group(x, y, z, yaw = 0, parent = world) { const g = new THREE.Group(); g.position.set(x, y, z); g.rotation.y = yaw; parent.add(g); return g; }
// a thin cord between two points (picture strings)
function cord(a, b, parent = world, r = 0.004) {
  const A = new THREE.Vector3(...a), B = new THREE.Vector3(...b), d = B.clone().sub(A);
  const m = mesh(new THREE.CylinderGeometry(r, r, d.length(), 5), M.string, 0, 0, 0, parent);
  m.position.copy(A).addScaledVector(d, 0.5); m.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), d.normalize()); return m;
}
// a wall slab with rectangular openings, extruded outward
function wallSlab(width, height, holes, mat, thick = 0.14) {
  const s = new THREE.Shape(); s.moveTo(0, 0); s.lineTo(width, 0); s.lineTo(width, height); s.lineTo(0, height); s.closePath();
  for (const [u0, v0, u1, v1] of holes) { const p = new THREE.Path(); p.moveTo(u0, v0); p.lineTo(u0, v1); p.lineTo(u1, v1); p.lineTo(u1, v0); p.closePath(); s.holes.push(p); }
  const g = new THREE.ExtrudeGeometry(s, { depth: thick, bevelEnabled: false }); g.computeVertexNormals();
  return new THREE.Mesh(worldUV(g, 1.3), mat);
}
// a board with an arched top; 'shoulder' dips the curve near the posts like the painted bed
function archedBoard(w, h, rise, thick, mat, shoulder = 0) {
  const s = new THREE.Shape(); s.moveTo(-w / 2, 0); s.lineTo(w / 2, 0); s.lineTo(w / 2, h - rise);
  s.bezierCurveTo(w * 0.32, h - rise - shoulder, w * 0.22, h + rise * 0.9, 0, h + rise * 0.9);
  s.bezierCurveTo(-w * 0.22, h + rise * 0.9, -w * 0.32, h - rise - shoulder, -w / 2, h - rise);
  s.closePath();
  const g = new THREE.ExtrudeGeometry(s, { depth: thick, bevelEnabled: true, bevelThickness: 0.012, bevelSize: 0.012, bevelSegments: 2, curveSegments: 24 });
  g.translate(0, 0, -thick / 2);
  return new THREE.Mesh(worldUV(g, 0.9), mat);
}
// hanging cloth: a subdivided plane pulled into soft folds, optionally flared toward the hem
function cloth(w, h, mat, folds = 5, depth = 0.025, flare = 0, seed = 1) {
  const g = new THREE.PlaneGeometry(w, h, 24, 30); const p = g.attributes.position; const r = rng(seed);
  const ph = r() * 6;
  for (let i = 0; i < p.count; i++) {
    const x = p.getX(i), y = p.getY(i), t = 0.5 - y / h;
    p.setX(i, x * (1 + flare * t));
    p.setZ(i, Math.sin((x / w) * Math.PI * folds + ph + t * 1.5) * depth * (0.3 + t) + t * t * 0.04);
  }
  g.computeVertexNormals(); g.translate(0, -h / 2, 0); // origin at the top edge
  const m = new THREE.Mesh(worldUV(g, 0.6), mat.clone()); m.material.side = THREE.DoubleSide; return m;
}

// A soft cushion: a rounded box whose thickness falls away toward its edges.
function cushion(w, h, d, mat, rad = 0.05) {
  const g = new RoundedBoxGeometry(w, h, d, 5, rad); const p = g.attributes.position;
  for (let i = 0; i < p.count; i++) {
    const u = p.getX(i) / (w / 2), v = p.getZ(i) / (d / 2);
    const k = 0.35 + 0.65 * (1 - u * u * 0.8) * (1 - v * v * 0.8);
    p.setY(i, p.getY(i) * k);
  }
  g.computeVertexNormals();
  return new THREE.Mesh(worldUV(g, 0.7), mat);
}

// Van Gogh did not draw the furniture in one consistent perspective: the table,
// the near chair and the bed are each larger, taller or closer than the room's
// geometry allows. To match the painting from the painter's viewpoint, each piece
// gets its own offset (dx, dz in metres), uniform scale s and extra height sy,
// solved by comparing its outline against the canvas.
const OBJ = {};
const DISTORT = {};   // filled by each object's file (room.js, bed.js, chairs.js, washstand.js, wall.js)
function applyDistortion() {
  for (const [k, d] of Object.entries(DISTORT)) {
    const g = OBJ[k]; if (!g) continue;
    g.userData.obj = k;
    g.position.x += d.dx; g.position.z += d.dz;
    g.scale.set(g.scale.x * d.s, g.scale.y * d.s * d.sy, g.scale.z * d.s);
  }
}
