// Render the Unity data (room.json + room.bytes + Textures) the way the Unity importer
// builds it, with lighting close to the Unity scene: a quick check of an export
// without opening Unity.
//
//   node preview_unity_data.mjs <data folder> '<views JSON>' <output folder>
//
// Views: [{"painter":true}] for the painter's framed view, or
// {"p":[x,y,z],"t":[x,y,z]} for a camera at p looking at t. These are three.js
// coordinates, so negate x from Unity coordinates. Optional "move":{"Carafe":[dx,dy,dz]}
// shifts objects by Unity-space deltas; it accumulates, so give it once.
// THREE_LOCAL=/path/to/three.js-r170 serves three.js locally instead of the CDN.
import { chromium } from 'playwright';
import fs from 'fs'; import path from 'path';
const DIR = process.argv[2]; const views = JSON.parse(process.argv[3]); const OUTDIR = process.argv[4] || '.';
if (!DIR || !process.argv[3]) { console.error("usage: node preview_unity_data.mjs <data folder> '<views JSON>' [output folder]"); process.exit(2); }
fs.mkdirSync(OUTDIR, { recursive: true });
const html = `<!doctype html><html><head><meta charset="utf-8"><style>html,body{margin:0;height:100%}</style>
<script type="importmap">{"imports":{"three":"https://cdn.jsdelivr.net/npm/three@0.170.0/build/three.module.js"}}</script></head><body>
<script type="module">
import * as THREE from 'three';
const r = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true }); r.setSize(innerWidth, innerHeight); r.outputColorSpace = THREE.SRGBColorSpace; document.body.appendChild(r.domElement);
const sc = new THREE.Scene(); sc.background = new THREE.Color('#142347');
r.shadowMap.enabled = true; r.shadowMap.type = THREE.PCFSoftShadowMap;
sc.add(new THREE.HemisphereLight('#e8eef0', '#7a7060', 1.6));
const d = new THREE.DirectionalLight('#fff3dc', 2.6); d.position.set(0.28 * -6 + 0, 0.57 * 6 + 1.6, 0.77 * 6 + 2); d.target.position.set(0, 0.6, 2); sc.add(d); sc.add(d.target);
d.castShadow = true; d.shadow.mapSize.set(2048, 2048); d.shadow.camera.left = -4; d.shadow.camera.right = 4; d.shadow.camera.top = 4; d.shadow.camera.bottom = -4; d.shadow.camera.far = 20; d.shadow.bias = -0.0005; d.shadow.radius = 3;
const cam = new THREE.PerspectiveCamera(70, innerWidth / innerHeight, 0.03, 40);
const H = await (await fetch('/d/room.json')).json(); const B = await (await fetch('/d/room.bytes')).arrayBuffer();
const F = new Float32Array(B, 0, H.floatCount), I = new Uint32Array(B, H.floatCount * 4, H.intCount);
const tl = new THREE.TextureLoader(); const tex = H.textures.map(n => { const t = tl.load('/d/Textures/' + n + '.png'); t.colorSpace = THREE.SRGBColorSpace; t.wrapS = t.wrapT = THREE.RepeatWrapping; return t; });
// mimic the Unity importer: a left-handed world shown in three.js through one mirror
const world = new THREE.Group(); world.scale.x = -1; sc.add(world);
const mats = H.materials.map(m => { const t = m.texture >= 0 ? tex[m.texture] : null; const col = new THREE.Color().setRGB(...m.color, THREE.SRGBColorSpace);
  if (m.outline) return new THREE.MeshBasicMaterial({ color: col, side: THREE.FrontSide });
  if (m.unlit) return new THREE.MeshBasicMaterial({ map: t, color: t ? 0xffffff : col, side: THREE.DoubleSide });
  const op = m.opacity ?? 1; return new THREE.MeshStandardMaterial({ map: t, color: t ? 0xffffff : col, roughness: 1, emissive: 0xffffff, emissiveMap: t, emissiveIntensity: t ? 0.11 : 0, side: THREE.DoubleSide, transparent: op < 1, opacity: op, depthWrite: op >= 1 }); });
let tris = 0;
for (const o of H.objects) { const g = new THREE.Group(); g.name = o.name; g.position.set(...o.position); world.add(g);
  for (const m of o.meshes) { const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.BufferAttribute(F.slice(m.positions, m.positions + m.vertexCount * 3), 3));
    geo.setAttribute('normal', new THREE.BufferAttribute(F.slice(m.normals, m.normals + m.vertexCount * 3), 3));
    geo.setAttribute('uv', new THREE.BufferAttribute(F.slice(m.uvs, m.uvs + m.vertexCount * 2), 2));
    geo.setIndex(new THREE.BufferAttribute(I.slice(m.indices, m.indices + m.indexCount), 1)); tris += m.indexCount / 3;
    { const me = new THREE.Mesh(geo, mats[m.material]); const ink = H.materials[m.material].outline, outside = o.name === 'Window_View'; me.castShadow = !['Room_Walls','Room_Floor','Room_Ceiling','Room_Skirting','Door_Frames'].includes(o.name) && !ink && !outside && !(H.materials[m.material].opacity < 1); me.receiveShadow = !ink; g.add(me); } } }
const mk = Object.fromEntries(H.markers.map(m => [m.name, new THREE.Vector3(-m.position[0], m.position[1], m.position[2])]));
window.__view = (v) => { if (v.move) for (const [n, d] of Object.entries(v.move)) { const g = world.getObjectByName(n); if (g) { g.position.x += d[0]; g.position.y += d[1]; g.position.z += d[2]; } } if (v.painter) { const e = mk.Marker_PainterView, t = mk.Marker_PainterView_Target; cam.position.copy(e); cam.lookAt(t);
    const n = cam.near, hv = Math.tan(H.painterFov * Math.PI / 360), hh = hv * innerWidth / innerHeight, sh = H.painterLensShiftY * 2 * hv;
    cam.updateMatrixWorld(); cam.projectionMatrix.makePerspective(-hh * n, hh * n, (sh + hv) * n, (sh - hv) * n, n, 40); cam.projectionMatrixInverse.copy(cam.projectionMatrix).invert(); }
  else { cam.fov = 70; cam.updateProjectionMatrix(); cam.position.set(...v.p); cam.lookAt(...v.t); }
  r.render(sc, cam); };
window.__info = { tris }; await new Promise(res => setTimeout(res, 1500)); window.__ready = true;
</script></body></html>`;
const b = await chromium.launch({ args: ['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader','--ignore-gpu-blocklist'] });
const p = await b.newPage({ viewport: { width: 700, height: 564 } });
p.on('pageerror', e => console.log('ERR '+e.message));
if (process.env.THREE_LOCAL) await p.route('https://cdn.jsdelivr.net/npm/three@0.170.0/**', r => { const rel = r.request().url().split('three@0.170.0/')[1]; r.fulfill({ path: path.join(process.env.THREE_LOCAL, rel), contentType: 'application/javascript' }); });
await p.route('http://local/d/**', r => r.fulfill({ path: path.join(DIR, decodeURIComponent(new URL(r.request().url()).pathname.slice(3))) }));
await p.route('http://local/', r => r.fulfill({ body: html, contentType: 'text/html' }));
await p.goto('http://local/', { waitUntil: 'commit' });
await p.waitForFunction(() => window.__ready, null, { timeout: 600000 });
console.log(JSON.stringify(await p.evaluate(() => window.__info)));
let i = 0; for (const v of views) { await p.evaluate(v => window.__view(v), v); await p.waitForTimeout(400); await p.screenshot({ path: path.join(OUTDIR, `view${i++}.png`) }); console.log(path.join(OUTDIR, `view${i - 1}.png`)); }
await b.close();
