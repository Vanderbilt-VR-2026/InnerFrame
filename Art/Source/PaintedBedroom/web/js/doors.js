// Painted Bedroom: The two doors, their frames and casings, and the landing behind each door.
// Unity objects: Door_Left, Door_Right, Door_Frames. Their openings in the walls (DOOR_L, DOOR_R) are in room.js.
// Loaded as a plain script after core.js; all files share one scope (see index.html).
"use strict";

// This object's materials, added to the shared palette M by buildMaterials() in core.js.
function doorsMaterials() {
  // doors: light blue with paler, greenish mottling
  M.door = paintMat({ seed: 10, base: '#62a2e2', cols: ['#72aee6', '#4f90d8', '#8cc0ec', '#a9cfee', '#9cc4cc', '#5a9ade'], strokes: 2200, angle: Math.PI / 2, spread: 0.3, len: [12, 40], wid: [5, 11], vary: 0.06 });
  M.doorTrim = paintMat({ seed: 11, base: '#3f78c0', cols: ['#4a84c8', '#336cb4', '#5890d0'], strokes: 900, angle: Math.PI / 2, spread: 0.2 });
  M.casing = paintMat({ seed: 26, base: '#244e36', cols: ['#2f5e42', '#1c402c', '#3a6a4c'], strokes: 900, angle: Math.PI / 2, spread: 0.2 });
  M.casingPale = paintMat({ seed: 30, base: '#cfe0ee', cols: ['#e2ecf4', '#bcd2e6', '#f0f4f6'], strokes: 900, angle: Math.PI / 2, spread: 0.1 });
  // the landing behind each door: a dim, cool hallway
  M.recess = paintMat({ seed: 42, base: '#3c5a74', cols: ['#4a6a86', '#2f4a62', '#56789a', '#34506a'], strokes: 900, angle: Math.PI / 2, spread: 0.3 });
}

// Colour grading measured against the painting for these materials (see GRADE in core.js).
Object.assign(GRADE, {
  door: { dL: -0.47, kC: 0.63, dh: -28.1 },
  doorTrim: { dL: -0.47, kC: 0.63, dh: -28.1 },
  casing: { dL: -0.47, kC: 0.63, dh: -28.1 },
  casingPale: { dL: -0.47, kC: 0.63, dh: -28.1 },
});

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
