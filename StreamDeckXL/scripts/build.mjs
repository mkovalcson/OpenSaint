import { build } from 'esbuild';
import { mkdir, writeFile, copyFile } from 'node:fs/promises';
import sharp from 'sharp';
const root = 'com.johnny5.animationeditor.sdPlugin';
await mkdir(root + '/bin', { recursive: true }); await mkdir(root + '/images', { recursive: true });
await build({ entryPoints: ['src/plugin.mjs'], outfile: root + '/bin/plugin.js', bundle: true, platform: 'node', target: 'node20', format: 'cjs', minify: false });
const shapes = {
  plugin: '<rect x="28" y="32" width="88" height="64" rx="18"/><circle cx="51" cy="58" r="10" fill="#18232e"/><circle cx="93" cy="58" r="10" fill="#18232e"/><path d="M55 81H89" stroke="#18232e" stroke-width="7"/>',
  pose: '<circle cx="72" cy="42" r="15"/><path d="M42 92V69Q72 52 102 69V92Z"/>',
  sequence: '<path d="M50 27L104 62L50 96Z"/>',
  choose: '<path d="M28 36H65L78 48H116V94H28Z"/>',
  stop: '<rect x="43" y="33" width="58" height="58" rx="6"/>'
};
for (const [name, shape] of Object.entries(shapes)) await writeFile(`${root}/images/${name}.svg`, `<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144"><rect width="144" height="144" rx="18" fill="#18232e"/><g fill="${name === 'stop' ? '#ea8888' : '#bdd9e9'}">${shape}</g></svg>`);
// Elgato requires a PNG for the plugin's installation icon.
await sharp(`${root}/images/plugin.svg`).resize(256, 256).png().toFile(`${root}/images/plugin.png`);
await sharp(`${root}/images/plugin.svg`).resize(512, 512).png().toFile(`${root}/images/plugin@2x.png`);
await copyFile(`${root}/images/plugin.svg`, `${root}/images/category.svg`);
await copyFile('node_modules/ws/LICENSE', `${root}/THIRD_PARTY_NOTICES.txt`);
console.log('Built Animation Editor Stream Deck plugin.');
