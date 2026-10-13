// Render the painter's view of the web scene, next to the painting, without opening a browser.
//
//   node snapshot.mjs <out.png> [groups]
//
// groups: a comma list of room, bed, washstand, chairs, wall (default: everything), so you can
// look at the object you changed on its own:  node snapshot.mjs /tmp/chairs.png chairs
// Writes <out.png> (render | painting, same height) and <out>_render.png (render only).
// Same setup as export_unity.mjs (npm install playwright && npx playwright install chromium;
// THREE_LOCAL for offline use). Takes about 20 s.
import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';
import { fileURLToPath, pathToFileURL } from 'url';

const here = path.dirname(fileURLToPath(import.meta.url));
const [OUT, GROUPS] = process.argv.slice(2);
if (!OUT) { console.error('usage: node snapshot.mjs <out.png> [room,bed,washstand,chairs,wall]'); process.exit(2); }
const W = 1241, H = 1000;   // the page frames the painting at 700:564 (PAINT_ASPECT in js/app.js)

const b = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const p = await b.newPage({ viewport: { width: W, height: H } });
const errors = [];
p.on('pageerror', e => errors.push(e.message));
if (process.env.THREE_LOCAL) {
  await p.route('https://cdn.jsdelivr.net/npm/three@0.170.0/**', r => {
    const rel = r.request().url().split('three@0.170.0/')[1];
    r.fulfill({ path: path.join(process.env.THREE_LOCAL, rel), contentType: 'application/javascript' });
  });
}
await p.route('https://fonts.**', r => r.abort());
const url = pathToFileURL(path.join(here, '..', 'index.html')).href + (GROUPS ? '?only=' + encodeURIComponent(GROUPS) : '');
await p.goto(url, { waitUntil: 'commit', timeout: 120000 });
await p.waitForFunction(() => window.__innerframe, null, { timeout: 600000 });
await p.addStyleTag({ content: '.card, .bar, .hint, #joy, #loading, .compare { display: none !important; }' });
await p.evaluate(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(() => requestAnimationFrame(r)))));
const render = await p.locator('#stage').screenshot();
const renderPath = OUT.replace(/\.png$/i, '') + '_render.png';
fs.writeFileSync(renderPath, render);

// side by side with the painting, at the same height
const repoRoot = path.join(here, '..', '..', '..', '..', '..');
const painting = fs.readFileSync(path.join(repoRoot, 'Assets', 'Art', 'Painting', 'The_Bedroom_Van_Gogh.jpg'));
const q = await b.newPage({ viewport: { width: W * 2 + 16, height: H } });
await q.setContent(`<body style="margin:0;background:#fff;display:flex;gap:16px">
  <img style="height:${H}px" src="data:image/png;base64,${render.toString('base64')}">
  <img style="height:${H}px" src="data:image/jpeg;base64,${painting.toString('base64')}"></body>`);
await q.waitForFunction(() => [...document.images].every(i => i.complete));
const width = await q.evaluate(() => Math.ceil(document.body.scrollWidth));
await q.setViewportSize({ width, height: H });
await q.screenshot({ path: OUT });
await b.close();
if (errors.length) { console.error('page errors:\n' + errors.join('\n')); process.exit(1); }
console.log(`wrote ${OUT} and ${renderPath}${GROUPS ? ' (only ' + GROUPS + ')' : ''}`);
