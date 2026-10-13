// Painted Bedroom: The things on the washstand, standing on its top (they move and scale with it).
// Unity objects: Carafe, Basin, Pitcher, Tumbler, SoapDish, ClothesBrush, Bottle_Tall, Bottle_Short, BrushBox.
// Loaded as a plain script after core.js; all files share one scope (see index.html).
"use strict";

// This object's materials, added to the shared palette M by buildMaterials() in core.js.
function washstandItemsMaterials() {
  M.carafe = paintMat({ seed: 14, base: '#a6d2c6', cols: ['#bfe2d8', '#8cc2b6', '#d8f0e8', '#9ccac0'], strokes: 1200, angle: Math.PI / 2, spread: 0.5, wid: [4, 8] });
  M.blueware = paintMat({ seed: 15, base: '#4f90d4', cols: ['#68a6e0', '#3f7cc4', '#8cbeea', '#4a88d0'], strokes: 1200, angle: Math.PI / 2, spread: 0.6, wid: [4, 8] });
  M.glassClear = new THREE.MeshStandardMaterial({ color: '#d2e8e4', roughness: 0.3, emissive: new THREE.Color('#9fc0c0'), emissiveIntensity: 0.25 });
  M.soap = new THREE.MeshStandardMaterial({ color: '#ecc85a', roughness: 0.7 });
  M.cork = new THREE.MeshStandardMaterial({ color: '#4a3a2a', roughness: 0.9 });
  M.bristle = new THREE.MeshStandardMaterial({ color: '#4a3418', roughness: 0.95 });
  M.box = paintMat({ seed: 27, base: '#ece2a8', cols: ['#f4eabc', '#dccf8e', '#f8f0d0'], strokes: 500 });
}

// Colour grading measured against the painting for these materials (see GRADE in core.js).
Object.assign(GRADE, {
  carafe: { dL: -4.07, kC: 0.492, dh: -37.9 },
  blueware: { dL: -4.07, kC: 0.492, dh: -37.9 },
});

function buildWashstandItems(g, h) {
  const gi = group(0, h + 0.002, 0, 0, g); gi.scale.setScalar(1.12);
  const item = (name) => { const t = new THREE.Group(); t.userData.exportName = name; gi.add(t); return t; };   // each thing on the table is its own movable object
  const carafe = item('Carafe'), basin = item('Basin'), pitcher = item('Pitcher'), tumbler = item('Tumbler'), soapDish = item('SoapDish'), cbrush = item('ClothesBrush');
  // pale green carafe with a handle, back left
  lathe([[0, 0], [0.06, 0], [0.078, 0.04], [0.084, 0.12], [0.068, 0.19], [0.036, 0.24], [0.03, 0.29], [0.042, 0.31], [0.038, 0.315]], M.carafe, -0.3, 0, 0.12, carafe);
  const ch = new THREE.Mesh(new THREE.TorusGeometry(0.055, 0.011, 8, 14, Math.PI), M.carafe); ch.position.set(-0.375, 0.18, 0.12); ch.rotation.z = Math.PI / 2; carafe.add(ch);
  // wide blue basin with the blue pitcher standing in it, its handle and spout
  lathe([[0, 0], [0.09, 0], [0.11, 0.015], [0.18, 0.05], [0.22, 0.072], [0.226, 0.08]], M.blueware, -0.05, 0, 0.04, basin);
  lathe([[0, 0], [0.06, 0], [0.074, 0.02], [0.088, 0.08], [0.082, 0.14], [0.058, 0.19], [0.05, 0.21], [0.068, 0.245], [0.064, 0.25]], M.blueware, -0.05, 0.012, 0.06, pitcher);
  const ph = new THREE.Mesh(new THREE.TorusGeometry(0.062, 0.013, 8, 16, Math.PI), M.blueware); ph.position.set(-0.125, 0.15, 0.06); ph.rotation.z = Math.PI / 2; pitcher.add(ph);
  const spout = cyl(0.008, 0.032, 0.07, M.blueware, 0.02, 0.24, 0.06, pitcher, 10); spout.rotation.z = -1.0;
  // tumbler and the soap dish at the front left
  lathe([[0, 0], [0.028, 0], [0.034, 0.1], [0.032, 0.102]], M.glassClear, -0.34, 0, -0.15, tumbler, 16);
  lathe([[0, 0], [0.07, 0], [0.085, 0.012], [0.088, 0.016]], M.blueware, -0.17, 0, -0.17, soapDish, 20);
  const soap = new THREE.Mesh(new THREE.SphereGeometry(0.05, 14, 8), M.soap); soap.scale.set(1, 0.4, 0.75); soap.position.set(-0.17, 0.025, -0.17); soapDish.add(soap);
  // two stoppered bottles at the back right
  for (const [bx, bz, hh, bn] of [[0.15, 0.15, 0.17, 'Bottle_Tall'], [0.23, 0.11, 0.15, 'Bottle_Short']]) {
    const bottle = item(bn);
    lathe([[0, 0], [0.026, 0], [0.028, hh * 0.65], [0.01, hh * 0.85], [0.01, hh], [0, hh]], M.glassClear, bx, 0, bz, bottle, 16);
    const st = new THREE.Mesh(new THREE.SphereGeometry(0.016, 10, 8), M.cork); st.position.set(bx, hh + 0.01, bz); bottle.add(st);
  }
  // a clothes brush lying bristles-up on a small box, front right
  const cbox = item('BrushBox');
  box(0.25, 0.05, 0.14, M.box, 0.22, 0.025, -0.1, cbox, 0.3);
  box(0.2, 0.03, 0.07, M.box, 0.22, 0.065, -0.1, cbrush, 0.3);
  for (let i = 0; i < 6; i++) box(0.022, 0.03, 0.055, M.bristle, 0.135 + i * 0.034, 0.093, -0.1, cbrush, 0.3);
}


