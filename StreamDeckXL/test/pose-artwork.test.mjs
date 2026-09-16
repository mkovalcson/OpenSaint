import test from 'node:test';
import assert from 'node:assert/strict';
import { PoseArtwork, buttonTitle, poseImage } from '../src/pose-artwork.mjs';

test('pose artwork is shared, refreshed on expiry or demand, and tolerates missing images', async () => {
  let now = 0, calls = 0;
  const cache = new PoseArtwork(async (id, name) => { calls++; if (name === 'bad') throw Error('missing'); return id + name + calls; }, () => now);
  const [a, b] = await Promise.all([cache.get(10, 'wave'), cache.get(10, 'wave')]);
  assert.equal(a, b); assert.equal(calls, 1);
  assert.equal(await cache.get(10, 'wave'), a);
  now = 30001; assert.notEqual(await cache.get(10, 'wave'), a);
  await cache.get(10, 'wave', true); assert.equal(calls, 3);
  await cache.get(11, 'wave'); assert.equal(calls, 4);
  assert.equal(await cache.get(10, 'bad'), null);
  assert.equal(await cache.get(10, 'bad'), null); assert.equal(calls, 5);
});

test('button captions wrap at words into at most two short lines', () => {
  assert.equal(buttonTitle('Wave'), 'Wave');
  assert.equal(buttonTitle('Friendly greeting'), 'Friendly\ngreeting');
  assert.equal(buttonTitle('abcdefghijklmnopqrstuvwxy'), 'abcdefghijkl\nmnopqrstuvw…');
  assert.equal(buttonTitle(' Hello\nworld '), 'Hello world');
  for (const label of ['Long descriptive pose name', 'abcdefghijklmnopqrstuvwxy']) {
    const lines = buttonTitle(label).split('\n');
    assert(lines.length <= 2); assert(lines.every(line => line.length <= 12));
  }
});

test('thumbnail retains connection and playback feedback with generic fallback', () => {
  const images = { connected: 'green', disconnected: 'red', running: 'amber', active: 'border' };
  assert.equal(poseImage(null, {}, 'pose'), null);
  assert.equal(poseImage(images, null, 'pose'), 'red');
  assert.equal(poseImage(images, {}, 'pose'), 'green');
  assert.equal(poseImage(images, { library: { running: true, name: 'other' } }, 'pose'), 'amber');
  assert.equal(poseImage(images, { library: { running: true, name: 'folder\\pose' } }, 'folder/pose'), 'border');
});
