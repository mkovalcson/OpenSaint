import WebSocket from 'ws';
import { randomUUID } from 'node:crypto';
import { EditorClient, selectEditor, requireOk } from './editor-client.mjs';
import { PoseArtwork, buttonTitle, poseImage } from './pose-artwork.mjs';

const args = Object.fromEntries(Array.from({ length: Math.floor((process.argv.length - 2) / 2) }, (_, i) => process.argv.slice(2 + i * 2, 4 + i * 2)));
if (!/^\d+$/.test(args['-port'] || '') || !args['-pluginUUID'] || !args['-registerEvent']) throw new Error('Start this plugin through Stream Deck.');
const socket = new WebSocket('ws://127.0.0.1:' + args['-port']);
// Registration info also lists offline devices. Only live connection events count.
const connectedDevices = new Set();
const client = new EditorClient({ streamDeckSession: randomUUID(), streamDeckConnected: () => connectedDevices.size > 0 });
const artwork = new PoseArtwork(async (id, name) => requireOk(await client.request(id,
  { method: 'get_library_thumbnail', libraryKind: 'pose', name })).images);
const artSettingsKey = action => JSON.stringify([action.settings.processId, action.settings.name]);
async function loadArtwork(action, id, state, refresh = false) {
  if (!action.action.endsWith('.apply-pose') || !action.settings.name || !state.methods?.includes('get_library_thumbnail')) {
    action.artwork = null; return;
  }
  action.artwork = await artwork.get(id, action.settings.name, refresh);
  action.artSettingsKey = artSettingsKey(action);
}
const actions = new Map(), pending = new Map();
let polling = false, inspector = null;
const send = (event, context, payload) => {
  if (socket.readyState === WebSocket.OPEN) socket.send(JSON.stringify({ event, context, ...(payload === undefined ? {} : { payload }) }));
};
const shortName = name => (name || '').split(/[\\/]/).pop().replace(/\.json$/i, '');
const svg = (action, state, selected) => {
  const stopped = action.endsWith('.stop');
  const color = !state ? '#dc6666' : state.library?.running ? '#f1bd61' : '#56d19c';
  const icon = stopped ? '<rect x="45" y="31" width="54" height="54" rx="7"/>'
    : action.endsWith('.apply-pose') ? '<circle cx="72" cy="40" r="13"/><path d="M45 87V66Q72 52 99 66V87Z"/>'
    : action.endsWith('.choose-library') ? '<path d="M31 39H66L77 50H113V88H31Z"/>'
    : '<path d="M52 29L100 58L52 88Z"/>';
  const active = state?.library?.running && selected && state.library.name?.replaceAll('\\', '/') === selected.replaceAll('\\', '/');
  return 'data:image/svg+xml;base64,' + Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144"><rect width="144" height="144" rx="18" fill="#18232e"/><rect x="3" y="3" width="138" height="138" rx="16" fill="none" stroke="${active ? '#f1bd61' : '#3e5368'}" stroke-width="${active ? 6 : 2}"/><circle cx="124" cy="20" r="7" fill="${color}"/><g fill="${stopped ? '#ea8888' : '#bdd9e9'}">${icon}</g></svg>`).toString('base64');
};
function updateKey(context, action, state) {
  if (actions.get(context) !== action) return; // A changed selection may supersede an in-flight image request.
  const label = action.settings.label || shortName(action.settings.name) || (action.action.endsWith('.stop') ? 'Stop' : action.action.endsWith('.choose-library') ? 'Choose…' : action.action.endsWith('.apply-pose') ? 'Apply Pose' : 'Play Sequence');
  const title = buttonTitle(label);
  const image = poseImage(action.artSettingsKey === artSettingsKey(action) ? action.artwork : null, state, action.settings.name)
    || svg(action.action, state, action.settings.name);
  if (action.lastImage !== image) { send('setImage', context, { image, target: 0 }); action.lastImage = image; }
  if (action.lastTitle !== title) { send('setTitle', context, { title, target: 0 }); action.lastTitle = title; }
}
function tell(context, payload) {
  const action = actions.get(context);
  if (action) send('sendToPropertyInspector', context, { ...payload });
}
async function inspect(context, refreshLibrary = true) {
  const action = actions.get(context); if (!action) return;
  try {
    const ids = await client.discover();
    tell(context, { type: 'editors', editors: ids });
    const id = selectEditor(ids, action.settings.processId);
    const state = await client.status(id);
    await loadArtwork(action, id, state, refreshLibrary); updateKey(context, action, state);
    tell(context, { type: 'status', state });
    if (refreshLibrary && !action.action.endsWith('.stop')) {
      const kind = action.action.endsWith('.apply-pose') ? 'pose' : action.action.endsWith('.play-sequence') ? 'sequence' : action.settings.kind === 'pose' ? 'pose' : 'sequence';
      tell(context, { type: 'library', items: await client.library(id, kind) });
    }
  } catch (error) { updateKey(context, action, null); tell(context, { type: 'error', message: error.message }); }
}
async function poll() {
  if (polling) return;
  polling = true;
  try {
    const ids = await client.discover(), states = new Map(ids.map(id => [id, client.status(id)]));
    await Promise.allSettled(states.values()); // Presence stays live even when another Stream Deck profile is visible.
    for (const [context, action] of actions) {
      try {
        const id = selectEditor(ids, action.settings.processId);
        if (!states.has(id)) states.set(id, client.status(id));
        const state = await states.get(id);
        await loadArtwork(action, id, state); updateKey(context, action, state);
        if (inspector === context) tell(context, { type: 'status', state });
      } catch (error) { updateKey(context, action, null); if (inspector === context) tell(context, { type: 'error', message: error.message }); }
    }
    for (const [context, receipt] of pending) {
      try {
        const reply = requireOk(await client.request(receipt.processId, { method: 'get_request', requestId: receipt.requestId }));
        if (reply.status !== 'awaiting_selection') {
          pending.delete(context); tell(context, { type: 'receipt', status: reply.status });
          send(reply.status === 'completed' ? 'showOk' : 'showAlert', context);
        }
      } catch (error) { pending.delete(context); send('showAlert', context); tell(context, { type: 'error', message: error.message }); }
    }
  } catch (error) {
    for (const [context, action] of actions) { updateKey(context, action, null); if (inspector === context) tell(context, { type: 'error', message: error.message }); }
  } finally { polling = false; }
}
socket.on('open', () => socket.send(JSON.stringify({ event: args['-registerEvent'], uuid: args['-pluginUUID'] })));
socket.on('message', async data => {
  let event; try { event = JSON.parse(data.toString()); } catch { return; }
  const { context, payload = {} } = event;
  if (event.event === 'deviceDidConnect' || event.event === 'deviceDidDisconnect') {
    if (event.event === 'deviceDidConnect') connectedDevices.add(event.device); else connectedDevices.delete(event.device);
    await poll(); return;
  }
  if (event.event === 'willAppear' || event.event === 'didReceiveSettings' || event.event === 'keyDown') {
    const existing = actions.get(context) || {};
    actions.set(context, { ...existing, action: event.action, settings: { ...payload.settings } });
  }
  if (event.event === 'willAppear') { await poll(); return; }
  if (event.event === 'willDisappear') { actions.delete(context); return; }
  if (event.event === 'propertyInspectorDidAppear') { inspector = context; await inspect(context); return; }
  if (event.event === 'propertyInspectorDidDisappear') { if (inspector === context) inspector = null; return; }
  if (event.event === 'didReceiveSettings') { if (inspector === context) await inspect(context); else await poll(); return; }
  if (event.event === 'sendToPlugin' && payload.type === 'refresh') { await inspect(context); return; }
  if (event.event !== 'keyDown') return;
  try {
    const action = actions.get(context);
    const result = await client.execute(action.action, { ...action.settings });
    if (result.result.status === 'awaiting_selection') pending.set(context, result);
    else { pending.delete(context); send('showOk', context); }
    tell(context, { type: 'receipt', status: result.result.status || 'completed' });
    await poll();
  } catch (error) { send('showAlert', context); tell(context, { type: 'error', message: error.message }); }
});
const timer = setInterval(poll, 1000);
socket.on('close', () => { clearInterval(timer); process.exit(0); });
socket.on('error', error => { console.error(error.message); });
