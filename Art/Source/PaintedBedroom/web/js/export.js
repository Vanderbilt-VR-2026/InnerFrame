// Painted Bedroom: Export to Blender (GLB) and to the Unity package (room.json, room.bytes, textures).
// Loaded as a plain script after core.js; all files share one scope (see index.html).
"use strict";

// Export for Blender / Unity. Every mesh is gathered into a named object
// (Bed, Washstand, Chair_Front, Walls, Window, ...), the layout's mirror is
// baked into the geometry (no negative scales), texture flips are baked into
// the UVs, and empty markers record the painter's viewpoint and a spawn point.
// Coordinates are the scene's own (right-handed, metres, Y up), so Unity's
// importers bring the room in with the same numbers as the InnerFrame graybox.
const OBJ_NAMES = { bed: 'Bed', table: 'Washstand', chairA: 'Chair_Front', chairB: 'Chair_Back' };
const CAT_NAMES = { wallBack: 'Room_Walls', wallLeft: 'Room_Walls', wallRight: 'Room_Walls', floor: 'Room_Floor', ceiling: 'Room_Ceiling',
  base: 'Room_Skirting', outside: 'Window_View', windowFrame: 'Window', glass: 'Window', sill: 'Window',
  smock: 'Clothes', clothDark: 'Clothes', peg: 'PegRail' };
function exportName(mesh) {
  for (let a = mesh; a && a !== world; a = a.parent) {
    if (a.userData.exportName) return a.userData.exportName;
    for (const [k, n] of Object.entries(OBJ_NAMES)) if (OBJ[k] === a) return n;
  }
  const cat = mesh.material.userData.cat;
  if (['door', 'doorTrim', 'casing', 'casingPale'].includes(cat)) {
    return 'Door_Frames';   // casings and jambs stay with the walls; the door leaves are tagged on their groups
  }
  return CAT_NAMES[cat] || 'Room_Details';
}
function buildExportRoot() {
  scene.updateMatrixWorld(true);
  const buckets = new Map();
  world.traverse(o => { if (o.isMesh && o.visible) { const n = exportName(o); if (!buckets.has(n)) buckets.set(n, []); buckets.get(n).push(o); } });
  const matCache = new Map();
  const exportMat = (m) => {
    if (matCache.has(m)) return matCache.get(m);
    let out = m;
    if (m.map && (m.map.repeat.x !== 1 || m.map.repeat.y !== 1 || m.map.offset.x || m.map.offset.y || m.map.rotation)) {
      out = m.clone(); const t = m.map.clone(); t.repeat.set(1, 1); t.offset.set(0, 0); t.rotation = 0; t.needsUpdate = true;
      out.map = t; if (m.emissiveMap === m.map) out.emissiveMap = t;
    }
    if (!out.name) out.name = m.userData.cat ? m.userData.cat : (m.isMeshBasicMaterial ? 'Ink_' + m.color.getHexString() : 'Paint_' + (m.color ? m.color.getHexString() : 'ffffff'));
    matCache.set(m, out); return out;
  };
  const root = new THREE.Group(); root.name = 'InnerFrame_Bedroom';
  const box = new THREE.Box3(), tmp = new THREE.Box3();
  for (const [name, meshes] of buckets) {
    box.makeEmpty(); for (const m of meshes) { m.geometry.computeBoundingBox(); tmp.copy(m.geometry.boundingBox).applyMatrix4(m.matrixWorld); box.union(tmp); }
    const pivot = new THREE.Vector3((box.min.x + box.max.x) / 2, box.min.y, (box.min.z + box.max.z) / 2);
    const g = new THREE.Group(); g.name = name; g.position.copy(pivot); root.add(g);
    const toLocal = new THREE.Matrix4().makeTranslation(-pivot.x, -pivot.y, -pivot.z);
    meshes.forEach((m, i) => {
      let geo = m.geometry.clone();
      const mat4 = new THREE.Matrix4().multiplyMatrices(toLocal, m.matrixWorld);
      geo.applyMatrix4(mat4);
      if (mat4.determinant() < 0) {                        // a mirrored transform reverses winding: put it back
        if (geo.index) { const ix = geo.index.array; for (let k = 0; k < ix.length; k += 3) { const t = ix[k + 1]; ix[k + 1] = ix[k + 2]; ix[k + 2] = t; } geo.index.needsUpdate = true; }
        else { for (const att of Object.values(geo.attributes)) { const a = att.array, s = att.itemSize; for (let k = 0; k < att.count; k += 3) for (let c = 0; c < s; c++) { const t = a[(k + 1) * s + c]; a[(k + 1) * s + c] = a[(k + 2) * s + c]; a[(k + 2) * s + c] = t; } att.needsUpdate = true; } }
      }
      const map = m.material.map;
      if (map && geo.attributes.uv && (map.repeat.x !== 1 || map.repeat.y !== 1 || map.offset.x || map.offset.y || map.rotation)) {
        map.updateMatrix(); const uv = geo.attributes.uv, v = new THREE.Vector2();
        for (let k = 0; k < uv.count; k++) { v.fromBufferAttribute(uv, k).applyMatrix3(map.matrix); uv.setXY(k, v.x, v.y); }
      }
      const mesh = new THREE.Mesh(geo, exportMat(m.material)); mesh.name = `${name}_${String(i).padStart(2, '0')}`; g.add(mesh);
    });
  }
  // markers: the painter's eye, a point he looks at, and a spawn point on the floor
  const eye = PAINT_VIEW.pos.clone(), dir = new THREE.Vector3(-Math.sin(PAINT_VIEW.yaw), 0, -Math.cos(PAINT_VIEW.yaw));
  for (const [n, p] of [['Marker_PainterView', eye], ['Marker_PainterView_Target', eye.clone().addScaledVector(dir, 2)], ['Marker_Spawn', new THREE.Vector3(eye.x, 0, eye.z).addScaledVector(dir, 0.6)]]) {
    const e = new THREE.Object3D(); e.name = n; e.position.copy(p); root.add(e);
  }
  return { root, buckets };
}
async function exportGLB() {
  const { GLTFExporter } = await import('three/addons/exporters/GLTFExporter.js');
  const { root, buckets } = buildExportRoot();
  const out = new THREE.Scene(); out.add(root);
  const glb = await new GLTFExporter().parseAsync(out, { binary: true, onlyVisible: true, maxTextureSize: 4096 });
  return { glb, objects: [...buckets.keys()], meshes: [...buckets.values()].reduce((a, b) => a + b.length, 0) };
}

// Export for Unity without Blender: a JSON description, one binary buffer and
// PNG textures, read by the InnerFrame editor importer. Converted to Unity's
// left-handed space here (x negated, triangle winding reversed), so the
// importer copies numbers straight into Mesh objects. Furniture gets an
// inside-out "ink" shell (inverted hull) for painted outlines that work in VR.
const OUTLINE_GROUPS = new Set(['Bed', 'Pillow_Back', 'Pillow_Front', 'Blanket', 'Washstand', 'Carafe', 'Basin', 'Pitcher', 'Tumbler', 'SoapDish', 'Bottle_Tall', 'Bottle_Short', 'BrushBox', 'ClothesBrush', 'Chair_Front', 'Chair_Back', 'Hat', 'PegRail', 'Window', 'Mirror', 'Picture_Landscape', 'Picture_PortraitMan', 'Picture_PortraitWoman']);
function exportUnity() {
  const { root } = buildExportRoot();
  const floats = [], ints = [];
  const pushF = arr => { const off = floats.length; for (const v of arr) floats.push(v); return off; };
  const pushI = arr => { const off = ints.length; for (const v of arr) ints.push(v); return off; };
  const images = new Map(), textures = [], materials = [], matIndex = new Map();
  // Stable names: a material is named after what it is (its palette entry, e.g. 'blanket'), or after
  // the object that first uses it when it has no palette entry. A number is added only when two would
  // otherwise share a name ('blanket', 'blanket_2'). Texture files are named after their material.
  // So adding or removing a material renames nothing else: diffs of Data/ and Unity's generated .mat
  // files only change for what actually changed. (Before, every name ended with its position in the
  // list, so one new material early on renamed every later material and texture.)
  const used = new Set(['Ink_Outline']);
  const unique = (base) => { if (!used.has(base)) { used.add(base); return base; } let k = 2; while (used.has(base + '_' + k)) k++; used.add(base + '_' + k); return base + '_' + k; };
  const texOf = (t, matName) => {
    if (!t || !t.image) return -1;
    if (!images.has(t.image)) { images.set(t.image, textures.length); textures.push({ name: unique('tex_' + matName), data: t.image.toDataURL('image/png') }); }
    return images.get(t.image);
  };
  const matOf = (m, objName) => {
    if (matIndex.has(m)) return matIndex.get(m);
    let base = (m.name || 'Paint').replace(/[^A-Za-z0-9_-]/g, '_');
    if (/^Paint(_|$)/.test(base)) base = objName + '_paint';   // no palette entry: name it after its object
    const name = unique(base);
    const tex = texOf(m.map, name);
    const c = m.color ? m.color.clone().convertLinearToSRGB() : new THREE.Color(1, 1, 1);
    materials.push({ name, texture: tex,
      color: [c.r, c.g, c.b], unlit: !!m.isMeshBasicMaterial, emission: m.emissiveMap ? (m.emissiveIntensity ?? 0.3) : 0, doubleSided: m.side === THREE.DoubleSide, outline: false,
      // window panes and the glassware are see-through in 3D (the painting shows them as flat colour)
      opacity: m.userData.cat === 'glass' ? 0.8 : m.userData.cat === 'glassClear' ? 0.6 : 1 });
    matIndex.set(m, materials.length - 1); return materials.length - 1;
  };
  const ink = { name: 'Ink_Outline', texture: -1, color: [0.06, 0.07, 0.06], unlit: true, emission: 0, doubleSided: false, outline: true, opacity: 1 };
  materials.push(ink); const inkIndex = materials.length - 1;
  const meshOut = (name, geo, material) => {
    const g = geo.index ? geo : (() => { const n = geo.attributes.position.count; const ix = new Uint32Array(n); for (let i = 0; i < n; i++) ix[i] = i; const c = geo.clone(); c.setIndex(new THREE.BufferAttribute(ix, 1)); return c; })();
    if (!g.attributes.normal) g.computeVertexNormals();
    const P = g.attributes.position, N = g.attributes.normal, U = g.attributes.uv, n = P.count;
    const pos = new Array(n * 3), nor = new Array(n * 3), uv = new Array(n * 2);
    for (let i = 0; i < n; i++) {
      pos[i * 3] = -P.getX(i); pos[i * 3 + 1] = P.getY(i); pos[i * 3 + 2] = P.getZ(i);
      nor[i * 3] = -N.getX(i); nor[i * 3 + 1] = N.getY(i); nor[i * 3 + 2] = N.getZ(i);
      uv[i * 2] = U ? U.getX(i) : 0; uv[i * 2 + 1] = U ? U.getY(i) : 0;
    }
    const ix = g.index.array, tri = new Array(ix.length);
    for (let k = 0; k < ix.length; k += 3) { tri[k] = ix[k]; tri[k + 1] = ix[k + 2]; tri[k + 2] = ix[k + 1]; }
    return { name, material, vertexCount: n, positions: pushF(pos), normals: pushF(nor), uvs: pushF(uv), indexCount: tri.length, indices: pushI(tri) };
  };
  // inside-out shell: vertices pushed out along normals averaged over shared positions, winding reversed
  const hullOf = (geo, t) => {
    const g = geo.index ? geo.clone() : geo.clone(); if (!g.attributes.normal) g.computeVertexNormals();
    const P = g.attributes.position, N = g.attributes.normal, key = i => `${Math.round(P.getX(i) * 1e4)},${Math.round(P.getY(i) * 1e4)},${Math.round(P.getZ(i) * 1e4)}`;
    const acc = new Map();
    for (let i = 0; i < P.count; i++) { const k = key(i); const a = acc.get(k) || [0, 0, 0]; a[0] += N.getX(i); a[1] += N.getY(i); a[2] += N.getZ(i); acc.set(k, a); }
    const out = new Float32Array(P.count * 3);
    for (let i = 0; i < P.count; i++) { const a = acc.get(key(i)), l = Math.hypot(a[0], a[1], a[2]) || 1; out[i * 3] = P.getX(i) + a[0] / l * t; out[i * 3 + 1] = P.getY(i) + a[1] / l * t; out[i * 3 + 2] = P.getZ(i) + a[2] / l * t; }
    const h = new THREE.BufferGeometry(); h.setAttribute('position', new THREE.BufferAttribute(out, 3)); h.setAttribute('normal', N.clone()); if (g.attributes.uv) h.setAttribute('uv', g.attributes.uv.clone());
    if (g.index) { const ix = g.index.array.slice(); for (let k = 0; k < ix.length; k += 3) { const tmp = ix[k + 1]; ix[k + 1] = ix[k + 2]; ix[k + 2] = tmp; } h.setIndex(new THREE.BufferAttribute(ix, 1)); }
    else { const n = P.count, ix = new Uint32Array(n); for (let k = 0; k < n; k += 3) { ix[k] = k; ix[k + 1] = k + 2; ix[k + 2] = k + 1; } h.setIndex(new THREE.BufferAttribute(ix, 1)); }
    return h;
  };
  // cloth gets a real thickness: the sheet is doubled along its normals and the open
  // edges are closed with a rim, so a towel or blanket picked up and turned is a solid thing
  const CLOTH = { Towel: 0.004, Smock: 0.006, Clothes_Dark: 0.008, Cloth_Pale: 0.006, Blanket: 0.014 };
  const thicken = (geo, t) => {
    const g = geo.index ? geo : (() => { const c = geo.clone(); const n = c.attributes.position.count, ix = []; for (let i = 0; i < n; i++) ix.push(i); c.setIndex(ix); return c; })();
    if (!g.attributes.normal) g.computeVertexNormals();
    const P = g.attributes.position, N = g.attributes.normal, U = g.attributes.uv, n = P.count, ix = g.index.array;
    const pos = new Float32Array(n * 6), nor = new Float32Array(n * 6), uv = new Float32Array(n * 4);
    for (let i = 0; i < n; i++) {
      const nx = N.getX(i), ny = N.getY(i), nz = N.getZ(i);
      pos.set([P.getX(i), P.getY(i), P.getZ(i)], i * 3); nor.set([nx, ny, nz], i * 3);
      pos.set([P.getX(i) - nx * t, P.getY(i) - ny * t, P.getZ(i) - nz * t], (n + i) * 3); nor.set([-nx, -ny, -nz], (n + i) * 3);
      const u = U ? U.getX(i) : 0, v = U ? U.getY(i) : 0; uv.set([u, v], i * 2); uv.set([u, v], (n + i) * 2);
    }
    const out = Array.from(ix);
    for (let k = 0; k < ix.length; k += 3) out.push(n + ix[k], n + ix[k + 2], n + ix[k + 1]);
    const edges = new Map();
    for (let k = 0; k < ix.length; k += 3) for (let e = 0; e < 3; e++) {
      const a = ix[k + e], b = ix[k + (e + 1) % 3], key = a < b ? a + '_' + b : b + '_' + a;
      const rec = edges.get(key); if (rec) rec.count++; else edges.set(key, { a, b, count: 1 });
    }
    for (const { a, b, count } of edges.values()) if (count === 1) out.push(a, n + b, b, a, n + a, n + b);
    const h = new THREE.BufferGeometry();
    h.setAttribute('position', new THREE.BufferAttribute(pos, 3)); h.setAttribute('normal', new THREE.BufferAttribute(nor, 3)); h.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
    h.setIndex(new THREE.BufferAttribute(new Uint32Array(out), 1));
    return h;
  };
  const objects = [], markers = [];
  for (const g of root.children) {
    if (!g.isGroup && !g.children.length && !g.isMesh) { markers.push({ name: g.name, position: [-g.position.x, g.position.y, g.position.z] }); continue; }
    const o = { name: g.name, position: [-g.position.x, g.position.y, g.position.z], meshes: [] };
    for (const m of g.children) {
      if (!m.isMesh) continue;
      const solid = CLOTH[g.name] && m.material.side === THREE.DoubleSide ? thicken(m.geometry, CLOTH[g.name]) : m.geometry;
      o.meshes.push(meshOut(m.name, solid, matOf(m.material, g.name)));
      if (OUTLINE_GROUPS.has(g.name)) {
        m.geometry.computeBoundingBox(); const s = new THREE.Vector3(); m.geometry.boundingBox.getSize(s);
        const mid = [s.x, s.y, s.z].sort((a, b) => a - b)[1];
        const t = Math.max(0.002, Math.min(0.007, 0.06 * (mid || 0.06)));
        o.meshes.push(meshOut(m.name + '_ink', hullOf(m.geometry, t), inkIndex));
      }
    }
    objects.push(o);
  }
  const header = { version: 1, objects, markers, materials, textures: textures.map(t => t.name), floatCount: floats.length, intCount: ints.length,
    painterFov: 2 * Math.atan(PAINT_VIEW.half) * 180 / Math.PI, painterLensShiftY: PAINT_VIEW.shift / (2 * PAINT_VIEW.half) };
  const bin = new ArrayBuffer(floats.length * 4 + ints.length * 4);
  new Float32Array(bin, 0, floats.length).set(floats); new Uint32Array(bin, floats.length * 4, ints.length).set(ints);
  return { json: JSON.stringify(header), bin: new Uint8Array(bin), textures };
}
window.__innerframe = { setMode, view, camera, PAINT_VIEW, idPass, project, BED, OBJ, exportGLB, exportUnity };
