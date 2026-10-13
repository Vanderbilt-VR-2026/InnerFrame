// Painted Bedroom: The washstand itself (top, drawer, legs). What stands on it is in washstand_items.js.
// Unity objects: Washstand.
// Loaded as a plain script after core.js; all files share one scope (see index.html).
"use strict";

// This object's materials, added to the shared palette M by buildMaterials() in core.js.
function washstandMaterials() {
  M.table = paintMat({ seed: 9, base: '#a8582a', cols: ['#b66630', '#8e4622', '#c27438', '#9c5026'], strokes: 1800, angle: Math.PI / 2, spread: 0.08, len: [30, 80], wid: [3, 7], vary: 0.07 });
  M.tableTop = paintMat({ seed: 29, base: '#bb6c34', cols: ['#c97c3e', '#a85a2a', '#d88e4c', '#b26432', '#e2a060'], strokes: 1800, angle: 0, spread: 0.1, len: [30, 80], wid: [4, 8], vary: 0.07 });
}

// Colour grading measured against the painting for these materials (see GRADE in core.js).
Object.assign(GRADE, {
  table: { dL: -4, kC: 0.552, dh: 20 },
  tableTop: { dL: -4, kC: 0.552, dh: 20 },
});

// Fit to the painting: offset, scale and extra height for this object (see DISTORT in core.js).
DISTORT.table = { dx: 0.156, dz: -0.191, s: 1.073, sy: 1.101 };

// ---------------------------------------------------------------------------
// The washstand and everything on it. Local +z points to the wall.
const TABLE = { x: -1.455, z: 3.575, yaw: -0.56, w: 0.8, d: 0.62, h: 0.92 };  // yaw from the fit to the painted table's corners and feet
function buildTable() {
  const g = group(TABLE.x, 0, TABLE.z, TABLE.yaw); OBJ.table = g;
  const { w, d, h } = TABLE;
  // a thick top that overhangs, its front edge catching the light
  box(w + 0.06, 0.045, d + 0.06, M.tableTop, 0, h - 0.0225, 0, g, 0.6);
  box(w - 0.08, 0.14, d - 0.08, M.table, 0, h - 0.11, 0, g, 0.6);
  // drawer with a dark seam and a small knob
  box(0.52, 0.095, 0.012, M.tableTop, 0.0, h - 0.11, -d / 2 + 0.035, g, 0.4);
  const knob = new THREE.Mesh(new THREE.SphereGeometry(0.016, 10, 8), M.peg); knob.position.set(0.0, h - 0.11, -d / 2 + 0.025); g.add(knob);
  for (const [sx, sz] of [[-1, -1], [1, -1], [-1, 1], [1, 1]]) {
    const leg = cyl(0.04, 0.026, h - 0.04, M.table, sx * (w / 2 - 0.055), (h - 0.04) / 2, sz * (d / 2 - 0.055), g, 4);
    leg.rotation.y = Math.PI / 4;
  }
  buildWashstandItems(g, h);   // the things standing on it: washstand_items.js
}
