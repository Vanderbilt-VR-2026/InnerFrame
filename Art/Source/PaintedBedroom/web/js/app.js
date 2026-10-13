// Painted Bedroom: Renderer, ink-outline pass, cameras (painter's view and walk), controls, and the build: which objects are made, in which order.
// Loaded as a plain script after core.js; all files share one scope (see index.html).
"use strict";

// ---------------------------------------------------------------------------
// Renderer, flat lighting and the ink-outline pass.
const stage = document.getElementById('stage');
const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.toneMapping = THREE.NoToneMapping;
stage.appendChild(renderer.domElement);
scene.background = new THREE.Color('#142347');
const camera = new THREE.PerspectiveCamera(60, 1, 0.03, 40);

// Mostly ambient, as in the painting, with a warm light from the window so the
// impasto relief catches something.
scene.add(new THREE.HemisphereLight('#ffffff', '#d4e0ea', 2.2));
const sun = new THREE.DirectionalLight('#fff6e4', 0.45); sun.position.set(1.2, 3.0, 5.5); sun.target.position.set(-0.5, 0, 0); scene.add(sun, sun.target);
const fill = new THREE.DirectionalLight('#dfe8ff', 0.25); fill.position.set(-1.5, 2.5, -3); scene.add(fill);

// Normal-depth pass for the outlines. The normal is packed into two channels so
// the third can carry a brush-variation value computed from the surface's own
// position in the room: the hand-drawn unevenness of each line travels with the
// object instead of sitting on the screen like a smudge on a lens.
const ndMat = new THREE.ShaderMaterial({
  vertexShader: `varying vec3 vN; varying float vD; varying vec3 vW;
    void main(){ vec4 mv = modelViewMatrix * vec4(position,1.0); vW = (modelMatrix * vec4(position,1.0)).xyz;
      vN = normalize(normalMatrix * normal); vD = -mv.z; gl_Position = projectionMatrix * mv; }`,
  fragmentShader: `varying vec3 vN; varying float vD; varying vec3 vW;
    float h3(vec3 p){ return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }
    float n3(vec3 p){ vec3 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f);
      return mix(mix(mix(h3(i), h3(i+vec3(1,0,0)), f.x), mix(h3(i+vec3(0,1,0)), h3(i+vec3(1,1,0)), f.x), f.y),
                 mix(mix(h3(i+vec3(0,0,1)), h3(i+vec3(1,0,1)), f.x), mix(h3(i+vec3(0,1,1)), h3(i+vec3(1,1,1)), f.x), f.y), f.z); }
    vec2 octEncode(vec3 n){ n /= (abs(n.x) + abs(n.y) + abs(n.z));
      vec2 e = n.z >= 0.0 ? n.xy : (1.0 - abs(n.yx)) * vec2(n.x >= 0.0 ? 1.0 : -1.0, n.y >= 0.0 ? 1.0 : -1.0); return e * 0.5 + 0.5; }
    void main(){ vec3 n = normalize(vN) * (gl_FrontFacing ? 1.0 : -1.0);
      gl_FragColor = vec4(octEncode(n), n3(vW * 9.0), vD); }`,
  side: THREE.DoubleSide,
});
let rtColor, rtND;
const post = new THREE.ShaderMaterial({
  uniforms: { tColor: { value: null }, tND: { value: null }, uPx: { value: new THREE.Vector2() }, uTime: { value: 0 }, uInk: { value: 1 }, uDpr: { value: 1 } },
  vertexShader: `varying vec2 vUv; void main(){ vUv = uv; gl_Position = vec4(position.xy, 0.0, 1.0); }`,
  fragmentShader: `
    uniform sampler2D tColor, tND; uniform vec2 uPx; uniform float uTime, uInk, uDpr; varying vec2 vUv;
    float hash(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
    float noise(vec2 p){ vec2 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f);
      return mix(mix(hash(i), hash(i+vec2(1,0)), f.x), mix(hash(i+vec2(0,1)), hash(i+vec2(1,1)), f.x), f.y); }
    vec3 octDecode(vec2 f){ f = f * 2.0 - 1.0; vec3 n = vec3(f, 1.0 - abs(f.x) - abs(f.y)); float t = max(-n.z, 0.0);
      n.x += n.x >= 0.0 ? -t : t; n.y += n.y >= 0.0 ? -t : t; return normalize(n); }
    vec3 toSRGB(vec3 c){ return mix(c * 12.92, 1.055 * pow(c, vec3(1.0/2.4)) - 0.055, step(0.0031308, c)); }
    void main(){
      // sample the image exactly: no per-frame displacement, which smeared the picture like a smudged lens
      vec2 uv = vUv;
      vec3 col = texture2D(tColor, uv).rgb;
      // edges are found at four sub-pixel positions and averaged, so thin parts
      // (chair legs, bottle necks, the peg rail) don't flicker as the view moves
      float edge = 0.0;
      for (int k = 0; k < 4; k++) {
        vec2 j = vec2(k == 1 || k == 3 ? 0.5 : -0.5, k >= 2 ? 0.5 : -0.5) * uPx;
        vec4 c = texture2D(tND, uv + j);
        vec3 n0 = octDecode(c.rg); float d0 = c.a;
        float th = (0.8 + c.b * 0.6) * uDpr * mix(1.0, 0.75, smoothstep(2.5, 4.5, d0));           // line width varies along the object, not across the screen
        float eN = 0.0, eD = 0.0;
        for (int i = 0; i < 8; i++) {
          float a = float(i) * 0.7853982;
          vec4 sN = texture2D(tND, uv + j + vec2(cos(a), sin(a)) * uPx * th);
          eN = max(eN, 1.0 - dot(n0, octDecode(sN.rg)));
          eD = max(eD, max(sN.a - d0, 0.0) / max(d0, 0.05));   // ink only on the near side: no halo on the wall behind
        }
        float e = max(smoothstep(0.28, 0.4, eN), smoothstep(0.035, 0.05, eD));
        float far = smoothstep(2.5, 4.5, d0);
        edge += e * mix(smoothstep(0.15, 0.6, c.b) * 0.65 + 0.35, 0.85, far) * 0.25;   // uneven, broken contour that moves with the surface
      }
      vec3 ink = mix(col * 0.28, vec3(0.13, 0.11, 0.09), 0.5);   // dark, but tinted by what it outlines
      col = mix(col, ink, edge * uInk);
      float l = dot(col, vec3(0.299, 0.587, 0.114));
      col = mix(vec3(l), col, 1.12);
      gl_FragColor = vec4(toSRGB(max(col, 0.0)), 1.0);
    }`,
  depthTest: false, depthWrite: false,
});
const postScene = new THREE.Scene();
const postCam = new THREE.OrthographicCamera(-1, 1, 1, -1, 0, 1);
postScene.add(new THREE.Mesh(new THREE.PlaneGeometry(2, 2), post));

let viewW = 1, viewH = 1;
function resize() {
  viewW = stage.clientWidth; viewH = stage.clientHeight; const dpr = renderer.getPixelRatio();
  renderer.setSize(viewW, viewH, false);
  const W = Math.floor(viewW * dpr), H = Math.floor(viewH * dpr);
  rtColor?.dispose(); rtND?.dispose();
  rtColor = new THREE.WebGLRenderTarget(W, H, { samples: 4 });
  rtND = new THREE.WebGLRenderTarget(W, H, { type: THREE.HalfFloatType, minFilter: THREE.NearestFilter, magFilter: THREE.NearestFilter });
  post.uniforms.tColor.value = rtColor.texture; post.uniforms.tND.value = rtND.texture;
  post.uniforms.uPx.value.set(1 / W, 1 / H); post.uniforms.uDpr.value = Math.max(1, H / 700);
}

// ---------------------------------------------------------------------------
// Camera. The painting is drawn with straight verticals while looking down at
// the floor, which is a shifted lens, not a tilted one: the painter's view keeps
// pitch at zero and moves the frame down instead. 'half' is half the frame
// height and 'shift' the frame centre, both in tangent units.
const PAINT_ASPECT = 700 / 564;
const PAINT_VIEW = { pos: new THREE.Vector3(0, 1.6, 0), yaw: Math.PI - 0.069, pitch: 0, half: 0.783, shift: -0.4 };
const WALK_LENS = { half: 0.62, shift: 0 };
const view = { pos: PAINT_VIEW.pos.clone(), yaw: PAINT_VIEW.yaw, pitch: 0, half: PAINT_VIEW.half, shift: PAINT_VIEW.shift };
let mode = 'painting', tween = null;
const EYE = 1.6, RADIUS = 0.22;

function applyView() {
  camera.position.copy(view.pos);
  camera.rotation.set(view.pitch, view.yaw, 0, 'YXZ');
  // keep the whole painting in frame on any screen shape
  const aspect = viewW / viewH, minAspect = PAINT_ASPECT;
  let hv = view.half, hh = hv * aspect;
  if (aspect < minAspect) { hh = view.half * minAspect; hv = hh / aspect; }
  const n = camera.near;
  camera.projectionMatrix.makePerspective(-hh * n, hh * n, (view.shift + hv) * n, (view.shift - hv) * n, n, camera.far);
  camera.projectionMatrixInverse.copy(camera.projectionMatrix).invert();
}
function goTo(target, ms = 1100) {
  const from = { pos: view.pos.clone(), yaw: view.yaw, pitch: view.pitch, half: view.half, shift: view.shift };
  let dy = target.yaw - from.yaw; dy = Math.atan2(Math.sin(dy), Math.cos(dy));
  tween = { from, to: { pos: target.pos.clone(), yaw: from.yaw + dy, pitch: target.pitch, half: target.half, shift: target.shift }, t0: performance.now(), ms: REDUCED ? 1 : ms };
}

// Walking limits, worked out in layout coordinates.
const DB = DISTORT.bed, DT = DISTORT.table;
const obbs = [
  { x: BED.x + DB.dx + Math.sin(BED.yaw) * BED.l * DB.s / 2, z: BED.z + DB.dz + Math.cos(BED.yaw) * BED.l * DB.s / 2, hw: BED.w * DB.s / 2 + 0.05, hd: BED.l * DB.s / 2 + 0.06, yaw: BED.yaw },
  { x: TABLE.x + DT.dx, z: TABLE.z + DT.dz, hw: TABLE.w * DT.s / 2 + 0.03, hd: TABLE.d * DT.s / 2 + 0.03, yaw: TABLE.yaw },
];
const chairsXZ = [];
function collide(q) {
  const p = { x: -q.x, z: q.z };
  p.x = Math.max(ROOM.xL + RADIUS, p.x);
  p.z = Math.min(ROOM.zB - RADIUS, Math.max(ROOM.zF + RADIUS, p.z));
  const dist = (p.x - ROOM.rB.x) * R_IN.x + (p.z - ROOM.rB.z) * R_IN.z;
  if (dist < RADIUS) { p.x += R_IN.x * (RADIUS - dist); p.z += R_IN.z * (RADIUS - dist); }
  for (const o of obbs) {
    const c = Math.cos(o.yaw), s = Math.sin(o.yaw), X = p.x - o.x, Z = p.z - o.z;
    const lx = c * X - s * Z, lz = s * X + c * Z; // world -> box frame
    const cx = Math.max(-o.hw, Math.min(o.hw, lx)), cz = Math.max(-o.hd, Math.min(o.hd, lz));
    let dx = lx - cx, dz = lz - cz, d = Math.hypot(dx, dz), nx, nz;
    if (d >= RADIUS) continue;
    if (d > 1e-5) { nx = cx + dx / d * RADIUS; nz = cz + dz / d * RADIUS; }
    else {
      const opts = [[lx + o.hw, -1, 0], [o.hw - lx, 1, 0], [lz + o.hd, 0, -1], [o.hd - lz, 0, 1]].sort((a, b) => a[0] - b[0])[0];
      nx = opts[1] ? Math.sign(opts[1]) * (o.hw + RADIUS) : lx; nz = opts[2] ? Math.sign(opts[2]) * (o.hd + RADIUS) : lz;
    }
    p.x = o.x + c * nx + s * nz; p.z = o.z - s * nx + c * nz; // box frame -> world
  }
  for (const [cx, cz, r] of chairsXZ) {
    const dx = p.x - cx, dz = p.z - cz, d = Math.hypot(dx, dz), m = r + RADIUS;
    if (d < m && d > 1e-5) { p.x = cx + dx / d * m; p.z = cz + dz / d * m; }
  }
  q.x = -p.x; q.z = p.z;
}

const keys = new Set();
addEventListener('keydown', e => {
  if (e.target.closest && e.target.closest('button') && (e.key === ' ' || e.key === 'Enter')) return;
  const k = e.key.toLowerCase(); keys.add(k);
  if (['w', 'a', 's', 'd', 'arrowup', 'arrowdown', 'arrowleft', 'arrowright'].includes(k)) { if (mode !== 'walk') setMode('walk', false); e.preventDefault(); }
  if (k === 'p') setMode('painting');
  if (k === 'o') toggleInk();
  if (k === 'c') toggleCompare();
});
addEventListener('keyup', e => keys.delete(e.key.toLowerCase()));
addEventListener('blur', () => keys.clear());

const canvas = renderer.domElement;
let look = null, stick = null; const joy = document.getElementById('joy'), joyKnob = joy.querySelector('i');
const moveVec = { x: 0, y: 0 };
canvas.addEventListener('pointerdown', e => {
  canvas.setPointerCapture(e.pointerId);
  const isStick = e.pointerType === 'touch' && e.clientX < innerWidth * 0.42 && e.clientY > innerHeight * 0.45;
  if (isStick) { stick = { id: e.pointerId, x: e.clientX, y: e.clientY }; joy.hidden = false; joy.style.left = (e.clientX - 46) + 'px'; joy.style.top = (e.clientY - 46) + 'px'; joy.style.bottom = 'auto'; if (mode !== 'walk') setMode('walk', false); }
  else look = { id: e.pointerId, x: e.clientX, y: e.clientY };
});
canvas.addEventListener('pointermove', e => {
  if (look && e.pointerId === look.id) {
    const s = e.pointerType === 'touch' ? 0.0055 : 0.0038;
    view.yaw -= (e.clientX - look.x) * s; view.pitch -= (e.clientY - look.y) * s;
    view.pitch = Math.max(-1.35, Math.min(1.35, view.pitch));
    look.x = e.clientX; look.y = e.clientY; tween = null;
    if (mode === 'painting' && Math.abs(view.yaw - PAINT_VIEW.yaw) + Math.abs(view.pitch - PAINT_VIEW.pitch) > 0.08) setMode('walk', false);
  }
  if (stick && e.pointerId === stick.id) {
    let dx = e.clientX - stick.x, dy = e.clientY - stick.y; const d = Math.hypot(dx, dy), m = 40;
    if (d > m) { dx *= m / d; dy *= m / d; }
    moveVec.x = dx / m; moveVec.y = -dy / m;
    joyKnob.style.transform = `translate(${dx}px, ${dy}px)`;
  }
});
const endPointer = e => {
  if (look && e.pointerId === look.id) look = null;
  if (stick && e.pointerId === stick.id) { stick = null; moveVec.x = moveVec.y = 0; joy.hidden = true; joyKnob.style.transform = ''; }
};
canvas.addEventListener('pointerup', endPointer); canvas.addEventListener('pointercancel', endPointer);

const btn = { painting: document.getElementById('btnPainting'), walk: document.getElementById('btnWalk'), ink: document.getElementById('btnOutline'), compare: document.getElementById('btnCompare') };
const hint = document.getElementById('hint');
const coarse = matchMedia('(pointer: coarse)').matches;
const walkHint = coarse ? 'Drag to look · hold the lower left to walk' : 'Drag to look · W A S D or arrow keys to walk · Shift to hurry';
function setMode(m, animate = true) {
  const was = mode; mode = m;
  btn.painting.setAttribute('aria-pressed', String(m === 'painting'));
  btn.walk.setAttribute('aria-pressed', String(m === 'walk'));
  if (m === 'painting') { goTo(PAINT_VIEW); hint.textContent = 'The painter\'s viewpoint · drag to look around'; }
  else {
    // leaving the painting: level the lens, keep looking where the painting looked
    const tgt = animate ? { pos: new THREE.Vector3(-0.1, EYE, 0.6), yaw: Math.PI - 0.1, pitch: -0.22 } : { pos: view.pos.clone(), yaw: view.yaw, pitch: was === 'painting' ? -0.3 : view.pitch };
    goTo({ ...tgt, ...WALK_LENS }, animate ? 900 : 450);
    hint.textContent = walkHint;
  }
}
let ink = true;
function toggleInk() { ink = !ink; btn.ink.setAttribute('aria-pressed', String(ink)); }
function toggleCompare() { const el = document.getElementById('compare'); el.hidden = !el.hidden; btn.compare.setAttribute('aria-pressed', String(!el.hidden)); }
btn.painting.onclick = () => setMode('painting');
btn.walk.onclick = () => setMode('walk');
btn.ink.onclick = toggleInk;
btn.compare.onclick = toggleCompare;

// ---------------------------------------------------------------------------
// Build and run.
// ?only=chairs (or a comma list of room, window, doors, bed, washstand, washstand_items, chairs, pictures, hangings)
// builds just those groups, to look at one object on its own; tools/snapshot.mjs renders it.
// Without ?only everything is built.
const ONLY = new URLSearchParams(location.search).get('only');
const want = group => !ONLY || ONLY.split(',').includes(group);
buildMaterials();
if (want('room')) buildRoom();
if (want('window')) { buildOutside(); buildWindow(); }               // the window and the world beyond it
if (want('doors')) buildDoors();
if (want('bed')) buildBed();
if (want('washstand') || want('washstand_items')) buildTable();     // the items stand on its top
if (want('chairs')) {
  OBJ.chairB = buildChair(-0.36, 3.45, 2.25, 1.12, 2);     // by the washstand, turned toward the bed
  OBJ.chairA = buildChair(-1.55, 2.15, 1.95, 1.0, 1);   // in the foreground; angle solved by projecting its back posts and seat front onto the canvas
}
applyDistortion();
for (const k of ['chairA', 'chairB']) { const g = OBJ[k]; if (g) chairsXZ.push([g.position.x, g.position.z, 0.28 * DISTORT[k].s]); }
if (want('pictures')) buildPictures();
if (want('hangings')) buildHangings();
// asked for only one of a pair: hide the other, once every file has loaded (exportName() is in export.js)
if (ONLY) addEventListener('DOMContentLoaded', () => {
  const ITEMS = ['Carafe', 'Basin', 'Pitcher', 'Tumbler', 'SoapDish', 'ClothesBrush', 'Bottle_Tall', 'Bottle_Short', 'BrushBox'];
  world.traverse(o => {
    if (!o.isMesh) return;
    const n = exportName(o);
    if ((n === 'Washstand' && !want('washstand')) || (ITEMS.includes(n) && !want('washstand_items'))) o.visible = false;
  });
});
addEventListener('resize', resize); resize();
applyView();
hint.textContent = coarse ? 'Drag to look · hold the lower left to walk' : 'Drag to look · W A S D or arrow keys to walk';

const clock = new THREE.Clock();
const fwd = new THREE.Vector3(), side = new THREE.Vector3();
function frame() {
  const dt = Math.min(clock.getDelta(), 0.05);
  if (tween) {
    const k = Math.min(1, (performance.now() - tween.t0) / tween.ms), e = k < 0.5 ? 4 * k * k * k : 1 - Math.pow(-2 * k + 2, 3) / 2;
    view.pos.lerpVectors(tween.from.pos, tween.to.pos, e);
    for (const key of ['yaw', 'pitch', 'half', 'shift']) view[key] = tween.from[key] + (tween.to[key] - tween.from[key]) * e;
    if (k >= 1) tween = null;
  }
  if (mode === 'walk') {
    let mx = moveVec.x, my = moveVec.y;
    if (keys.has('w') || keys.has('arrowup')) my += 1;
    if (keys.has('s') || keys.has('arrowdown')) my -= 1;
    if (keys.has('a')) mx -= 1;
    if (keys.has('d')) mx += 1;
    if (keys.has('arrowleft')) view.yaw += 1.8 * dt;
    if (keys.has('arrowright')) view.yaw -= 1.8 * dt;
    if (mx || my) {
      if (tween && tween.to.half === WALK_LENS.half) { view.half = WALK_LENS.half; view.shift = 0; }
      tween = null;
      const speed = (keys.has('shift') ? 2.2 : 1.25) * dt;
      fwd.set(-Math.sin(view.yaw), 0, -Math.cos(view.yaw));
      side.set(Math.cos(view.yaw), 0, -Math.sin(view.yaw));
      view.pos.addScaledVector(fwd, my * speed).addScaledVector(side, mx * speed);
      view.pos.y += (EYE - view.pos.y) * Math.min(1, dt * 4);
    }
    collide(view.pos);
  }
  applyView();
  post.uniforms.uTime.value = REDUCED ? 0 : clock.elapsedTime;
  post.uniforms.uInk.value = ink ? 1 : 0;

  renderer.setRenderTarget(rtColor); renderer.render(scene, camera);
  const bg = scene.background; scene.background = null; scene.overrideMaterial = ndMat;
  renderer.setClearColor(new THREE.Color(0.5, 0.5, 0.5), 1.0);
  renderer.setRenderTarget(rtND); renderer.clear(); renderer.render(scene, camera);
  scene.overrideMaterial = null; scene.background = bg; renderer.setClearColor(0x000000, 1);
  renderer.setRenderTarget(null); renderer.render(postScene, postCam);
  requestAnimationFrame(frame);
}
requestAnimationFrame(() => { document.getElementById('loading').hidden = true; frame(); });
// Comparison tool: render each surface category as a flat ID colour from the current camera.
function idPass(byObject = false) {
  const cats = [], idOf = {}, mats = new Map();
  const objOf = o => { for (let a = o; a; a = a.parent) if (a.userData.obj) return a.userData.obj; return null; };
  scene.traverse(o => { if (!o.isMesh) return; const c = (byObject && objOf(o)) || o.material.userData.cat || 'other';
    if (!(c in idOf)) { idOf[c] = cats.length + 1; cats.push(c); }
    const id = idOf[c]; mats.set(o, o.material); o.material = new THREE.MeshBasicMaterial({ color: new THREE.Color((id & 255) / 255, 0, 0), side: THREE.DoubleSide }); });
  const w = viewW, h = viewH, rt = new THREE.WebGLRenderTarget(w, h);
  const bg = scene.background; scene.background = new THREE.Color(0, 0, 0);
  renderer.setRenderTarget(rt); renderer.render(scene, camera); renderer.setRenderTarget(null);
  scene.background = bg;
  const buf = new Uint8Array(w * h * 4); renderer.readRenderTargetPixels(rt, 0, 0, w, h, buf);
  for (const [o, m] of mats) { o.material.dispose(); o.material = m; }
  const ids = new Array(w * h); for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) ids[(h - 1 - y) * w + x] = buf[(y * w + x) * 4];
  return { w, h, cats, ids: ids.join(',') };
}
// project layout coordinates (or a named object's local point) to screen pixels
function project(x, y, z, objName) {
  const v = new THREE.Vector3(x, y, z);
  if (objName) OBJ[objName].localToWorld(v); else world.localToWorld(v);
  v.project(camera); return [(v.x + 1) / 2 * viewW, (1 - v.y) / 2 * viewH];
}

// ---------------------------------------------------------------------------
