// Export the web scene (../index.html) to the Unity package's data format.
//
//   npm install playwright && npx playwright install chromium
//   node export_unity.mjs ../../InnerFrameBedroomWeb/Data
//
// Writes room.json, room.bytes and Textures/tex_###.png into the given folder.
// Optional: THREE_LOCAL=/path/to/three.js-r170 serves three.js from a local
// checkout instead of the jsdelivr CDN (for offline or restricted networks).
import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';
import { fileURLToPath, pathToFileURL } from 'url';

const here = path.dirname(fileURLToPath(import.meta.url));
const OUT = process.argv[2];
if (!OUT) { console.error('usage: node export_unity.mjs <output folder>'); process.exit(2); }
fs.mkdirSync(path.join(OUT, 'Textures'), { recursive: true });

const b = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const p = await b.newPage({ viewport: { width: 400, height: 300 } });
p.on('pageerror', e => console.log('page error: ' + e.message));
if (process.env.THREE_LOCAL) {
  await p.route('https://cdn.jsdelivr.net/npm/three@0.170.0/**', r => {
    const rel = r.request().url().split('three@0.170.0/')[1];
    r.fulfill({ path: path.join(process.env.THREE_LOCAL, rel), contentType: 'application/javascript' });
  });
}
await p.route('https://fonts.**', r => r.abort());   // fonts only style the page's labels
await p.goto(pathToFileURL(path.join(here, '..', 'index.html')).href, { waitUntil: 'commit', timeout: 120000 });
// the scene paints its textures on load; on a software renderer this can take minutes
await p.waitForFunction(() => window.__innerframe, null, { timeout: 600000 });

const info = await p.evaluate(() => { const r = window.__innerframe.exportUnity(); window.__u = r; return { json: r.json, bytes: r.bin.length, tex: r.textures.length }; });
fs.writeFileSync(path.join(OUT, 'room.json'), info.json);
const chunks = [], CH = 4 * 1024 * 1024;
for (let o = 0; o < info.bytes; o += CH) {
  const b64 = await p.evaluate(([o, CH]) => { const a = window.__u.bin.subarray(o, o + CH); let s = ''; for (let i = 0; i < a.length; i += 32768) s += String.fromCharCode.apply(null, a.subarray(i, i + 32768)); return btoa(s); }, [o, CH]);
  chunks.push(Buffer.from(b64, 'base64'));
}
fs.writeFileSync(path.join(OUT, 'room.bytes'), Buffer.concat(chunks));
for (let i = 0; i < info.tex; i++) {
  const t = await p.evaluate(i => window.__u.textures[i], i);
  fs.writeFileSync(path.join(OUT, 'Textures', t.name + '.png'), Buffer.from(t.data.split(',')[1], 'base64'));
}
const h = JSON.parse(info.json);
console.log(`exported ${h.objects.length} objects, ${h.materials.length} materials, ${info.tex} textures, ${info.bytes} bytes of mesh data to ${OUT}`);
await b.close();
