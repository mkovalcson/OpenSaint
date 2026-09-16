import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { WebSocketServer } from 'ws';
import { EditorClient } from '../src/editor-client.mjs';
const processId = Number(process.argv[2]);
const client = new EditorClient();
const settings = { processId: String(processId), name: 'DirectWave.json', loop: true };
assert((await client.discover()).includes(processId));
await client.execute('com.johnny5.animationeditor.play-sequence', settings);
assert((await client.status(processId)).library.running);
await client.execute('com.johnny5.animationeditor.apply-pose', { ...settings, name: 'DirectPose.json' });
assert(!(await client.status(processId)).library.running);
await client.execute('com.johnny5.animationeditor.stop', settings);

// Simulate the Stream Deck host's registration and physical key events against
// the packaged entrypoint, while its API connection reaches the real WPF editor.
const server = new WebSocketServer({ host: '127.0.0.1', port: 0 });
await new Promise(resolve => server.once('listening', resolve));
const messages = []; let pluginSocket;
const connected = new Promise(resolve => server.once('connection', socket => {
  pluginSocket = socket; socket.on('message', data => messages.push(JSON.parse(data.toString()))); resolve();
}));
const plugin = spawn(process.execPath, [fileURLToPath(new URL('../com.johnny5.animationeditor.sdPlugin/bin/plugin.js', import.meta.url)),
  '-port', String(server.address().port), '-pluginUUID', 'test-plugin', '-registerEvent', 'registerPlugin', '-info', JSON.stringify({ devices: [{ id: 'offline-xl', type: 2 }] })],
  { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
let errors = ''; plugin.stderr.on('data', data => errors += data);
async function waitFor(predicate) {
  const start = Date.now();
  while (Date.now() - start < 8000) {
    const found = messages.find(predicate); if (found) return found;
    if (plugin.exitCode != null) throw new Error('Plugin exited: ' + errors);
    await new Promise(resolve => setTimeout(resolve, 20));
  }
  throw new Error('Plugin event timeout: ' + errors + JSON.stringify(messages));
}
function event(name, action, context, settings) {
  pluginSocket.send(JSON.stringify({ event: name, action: 'com.johnny5.animationeditor.' + action, context,
    payload: { settings, controller: 'Keypad', coordinates: { row: 0, column: 0 } }, device: 'test-xl' }));
}
try {
  await connected;
  await waitFor(message => message.event === 'registerPlugin' && message.uuid === 'test-plugin');
  event('willAppear', 'play-sequence', 'wave', settings);
  await waitFor(message => message.event === 'setImage' && message.context === 'wave');
  assert.equal((await client.status(processId)).streamDeck.connected, false, 'Saved offline devices must not count as connected.');
  pluginSocket.send(JSON.stringify({ event: 'deviceDidConnect', device: 'test-xl', deviceInfo: { type: 2, name: 'Test XL', size: { columns: 8, rows: 4 } } }));
  event('willAppear', 'play-sequence', 'wave', settings);
  event('propertyInspectorDidAppear', 'play-sequence', 'wave', settings);
  await waitFor(message => message.event === 'sendToPropertyInspector' && message.payload.type === 'library' && message.payload.items.some(item => item.path === 'DirectWave.json'));
  event('keyDown', 'play-sequence', 'wave', settings);
  await waitFor(message => message.event === 'showOk' && message.context === 'wave');
  event('keyDown', 'apply-pose', 'pose', { ...settings, name: 'DirectPose.json' });
  await waitFor(message => message.event === 'showOk' && message.context === 'pose');
  await waitFor(message => message.event === 'setImage' && message.context === 'pose' && message.payload.image.startsWith('data:image/png;base64,'));
  event('keyDown', 'stop', 'stop', { processId: String(processId) });
  await waitFor(message => message.event === 'showOk' && message.context === 'stop');
  assert(messages.some(message => message.event === 'setImage' && message.payload.image.startsWith('data:image/svg+xml;base64,')));
  assert(!messages.some(message => message.event === 'showAlert'), JSON.stringify(messages));
  for (const [action, context] of [['play-sequence', 'wave'], ['apply-pose', 'pose'], ['stop', 'stop']]) event('willDisappear', action, context, {});
  pluginSocket.send(JSON.stringify({ event: 'deviceDidDisconnect', device: 'test-xl' }));
  const start = Date.now();
  while ((await client.status(processId)).streamDeck.connected && Date.now() - start < 8000) await new Promise(resolve => setTimeout(resolve, 100));
  assert.equal((await client.status(processId)).streamDeck.connected, false, 'Disconnect must be reported even with no visible plugin keys.');
  console.log('PASS: real pipe discovery, named playback, pose replacement, Stop, packaged plugin registration, XL key events, Library selector, and button feedback.');
} finally {
  pluginSocket?.close(); server.close(); plugin.kill();
}
