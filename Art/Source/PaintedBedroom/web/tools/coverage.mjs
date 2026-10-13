// Check that every object in an export belongs to exactly one group (one js/ file).
//
//   node coverage.mjs [Data folder]      (default: ../../InnerFrameBedroomWeb/Data)
//
// Loads index.html?only=<group> for each group, lists the Unity objects it draws, and
// compares with the export's objects. Run it after adding or renaming an object.
// Same setup as export_unity.mjs. Exit code 1 if an object is missing or in two groups.
import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';
import { fileURLToPath, pathToFileURL } from 'url';

const here = path.dirname(fileURLToPath(import.meta.url));
const data = process.argv[2] || path.join(here, '..', '..', 'InnerFrameBedroomWeb', 'Data');
const all = JSON.parse(fs.readFileSync(path.join(data, 'room.json'), 'utf8')).objects.map(o => o.name);
const GROUPS = ['room', 'window', 'doors', 'bed', 'washstand', 'washstand_items', 'chairs', 'pictures', 'hangings'];   // as in js/app.js

const b = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const seen = {}, errors = [];
for (const g of GROUPS) {
  const p = await b.newPage({ viewport: { width: 400, height: 300 } });
  p.on('pageerror', e => errors.push(`${g}: ${e.message}`));
  if (process.env.THREE_LOCAL) {
    await p.route('https://cdn.jsdelivr.net/npm/three@0.170.0/**', r => {
      const rel = r.request().url().split('three@0.170.0/')[1];
      r.fulfill({ path: path.join(process.env.THREE_LOCAL, rel), contentType: 'application/javascript' });
    });
  }
  await p.route('https://fonts.**', r => r.abort());
  await p.goto(pathToFileURL(path.join(here, '..', 'index.html')).href + '?only=' + g, { waitUntil: 'commit', timeout: 120000 });
  await p.waitForFunction(() => window.__innerframe, null, { timeout: 600000 });
  await p.evaluate(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))));
  const names = await p.evaluate(() => { const s = new Set(); world.traverse(o => { if (o.isMesh && o.visible) s.add(exportName(o)); }); return [...s].sort(); });
  for (const n of names) (seen[n] ||= []).push(g);
  console.log(g.padEnd(16), names.join(', '));
  await p.close();
}
await b.close();
const missing = all.filter(n => !seen[n]);
const extra = Object.keys(seen).filter(n => !all.includes(n));
const twice = Object.entries(seen).filter(([, g]) => g.length > 1).map(([n, g]) => `${n} (${g.join(', ')})`);
console.log(`\n${all.length} objects in the export, ${all.length - missing.length} covered`);
if (missing.length) console.log('in no group:', missing.join(', '));
if (extra.length) console.log('drawn but not in this export (re-export?):', extra.join(', '));
if (twice.length) console.log('in two groups:', twice.join('; '));
if (errors.length) console.log('page errors:\n' + errors.join('\n'));
const ok = !missing.length && !twice.length && !errors.length;
console.log(ok ? 'OK: every object belongs to exactly one group' : 'NOT OK');
process.exit(ok ? 0 : 1);
