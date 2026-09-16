import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdir } from 'node:fs/promises';
import { chromium } from 'playwright';
import { WebSocketServer } from 'ws';

test('Stream Deck settings configure playback without per-button output selectors', async () => {
  const server = new WebSocketServer({ host: '127.0.0.1', port: 0 });
  await new Promise(resolve => server.once('listening', resolve));
  const saved = [];
  server.on('connection', socket => socket.on('message', data => {
    const message = JSON.parse(data.toString());
    if (message.event === 'setSettings') saved.push(message.payload);
    if (message.event !== 'sendToPlugin') return;
    for (const payload of [
      { type: 'editors', editors: [123] },
      { type: 'status', state: { processId: 123, version: '1.24.1', library: { running: false } } },
      { type: 'library', items: [{ path: 'Gestures/DirectWave.json', description: 'A friendly greeting.' }, { path: 'Rest.json', description: 'Rest pose.' }] }
    ]) socket.send(JSON.stringify({ event: 'sendToPropertyInspector', payload }));
  }));
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 360, height: 1020 }, deviceScaleFactor: 1 });
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    async function open(action, settings = {}) {
      await page.goto(new URL('../com.johnny5.animationeditor.sdPlugin/ui/index.html', import.meta.url).href);
      await page.evaluate(({ port, action, settings }) => window.connectElgatoStreamDeckSocket(port, 'button-1', 'registerPropertyInspector', '{}',
        JSON.stringify({ action: 'com.johnny5.animationeditor.' + action, payload: { settings } })), { port: server.address().port, action, settings });
      await page.getByText('Connected · Editor 123', { exact: false }).waitFor();
    }
    await open('play-sequence', { driveUrdf: false, drivePhysical: false });
    assert.equal(await page.locator('#driveUrdf, #drivePhysical').count(), 0);
    assert.equal(await page.getByText('Output when pressed').count(), 0);
    assert(await page.getByText('Background permissions are set', { exact: false }).isVisible());
    await page.selectOption('#name', 'Gestures/DirectWave.json');
    await page.locator('#loop').check();
    await page.fill('#label', 'Greeting'); await page.locator('#label').blur();
    await page.waitForTimeout(30);
    assert.equal(saved.at(-1).name, 'Gestures/DirectWave.json'); assert.equal(saved.at(-1).loop, true);
    assert.equal(saved.at(-1).label, 'Greeting');
    assert.equal('driveUrdf' in saved.at(-1), false); assert.equal('drivePhysical' in saved.at(-1), false);
    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    await mkdir('previews', { recursive: true });
    await page.screenshot({ path: 'previews/StreamDeckSequenceSettings.png', fullPage: true });
    await open('apply-pose', { name: 'Rest.json' });
    assert(!(await page.locator('#loopRow').isVisible()));
    await open('choose-library', { kind: 'sequence' });
    assert(!(await page.locator('#librarySection').isVisible())); assert(await page.locator('#chooseSection').isVisible());
    await page.selectOption('#kind', 'pose'); assert(!(await page.locator('#loopRow').isVisible()));
    await open('stop');
    assert(!(await page.locator('#playbackSection').isVisible())); assert(await page.locator('#stopHelp').isVisible());
    assert.deepEqual(errors, []);
  } finally { await browser.close(); for (const socket of server.clients) socket.terminate(); await new Promise(resolve => server.close(resolve)); }
});
