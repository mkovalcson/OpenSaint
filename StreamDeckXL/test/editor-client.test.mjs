import test from 'node:test';
import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { actionRequest, EditorClient, requestPipe, selectEditor } from '../src/editor-client.mjs';
const base = 'com.johnny5.animationeditor';
const state = { methods: ['play_library_sequence'], capabilities: ['editor_configured_library_outputs'], libraryToken: 'library-1', transportToken: 'transport-1' };

test('Stream Deck heartbeats and commands share their session and report live availability', async () => {
  const sent = []; let connected = true;
  const client = new EditorClient({ discover: async () => [41], streamDeckSession: 'deck-session', streamDeckConnected: () => connected,
    request: async (id, request) => { sent.push(request); return { ok: true, state, status: 'completed' }; } });
  await client.execute(base + '.apply-pose', { name: 'Pose' });
  assert.deepEqual(sent[0], { method: 'stream_deck_status', streamDeckSession: 'deck-session', enabled: true });
  assert.equal(sent[1].streamDeckSession, 'deck-session');
  connected = false; await client.status(41); assert.equal(sent.at(-1).enabled, false);
});

test('button actions preserve exact paths, loop only sequences, and use editor output settings', () => {
  assert.deepEqual(actionRequest(base + '.apply-pose', { name: 'Folder/Greeting.json', loop: true }),
    { method: 'apply_library_pose', name: 'Folder/Greeting.json', loop: false, useEditorOutputs: true });
  assert.equal(actionRequest(base + '.play-sequence', { name: 'Wave.json', loop: true }).loop, true);
  const picker = actionRequest(base + '.choose-library', { kind: 'pose' });
  assert.equal(picker.method, 'apply_library_pose'); assert.equal('name' in picker, false);
  assert.deepEqual(actionRequest(base + '.stop', {}), { method: 'stop' });
  assert.throws(() => actionRequest(base + '.play-sequence', {}), /Choose a Library item/);
  assert.deepEqual(actionRequest(base + '.apply-pose', { name: 'Pose', driveUrdf: false, drivePhysical: false }),
    { method: 'apply_library_pose', name: 'Pose', loop: false, useEditorOutputs: true });
  assert.equal(picker.useEditorOutputs, true);
});
test('an older editor cannot silently ignore automatic output routing; Stop still works', async () => {
  let mutations = 0;
  const client = new EditorClient({ discover: async () => [41], request: async (id, body) => {
    if (body.method === 'get_status') return { ok: true, state: { ...state, capabilities: [] } };
    mutations++; return { ok: true, status: 'completed' };
  } });
  await assert.rejects(client.execute(base + '.apply-pose', { name: 'Pose' }), /Update Animation Editor/);
  assert.equal(mutations, 0);
  await client.execute(base + '.stop', {}); assert.equal(mutations, 1);
});
test('never silently choose among multiple editors or replace a closed selected instance', () => {
  assert.equal(selectEditor([23]), 23); assert.equal(selectEditor([23, 24], '24'), 24);
  assert.throws(() => selectEditor([]), /Open Animation Editor/);
  assert.throws(() => selectEditor([23, 24]), /Several editors/);
  assert.throws(() => selectEditor([24], '23'), /Selected editor is closed/);
});
test('uncertain mutation retries the identical request and instance only', async () => {
  const sent = []; let attempts = 0;
  const client = new EditorClient({ discover: async () => [41], request: async (id, body) => {
    if (body.method === 'get_status') return { ok: true, state };
    sent.push({ id, body: JSON.stringify(body) });
    if (++attempts === 1) throw new Error('reply lost');
    return { ok: true, status: 'completed' };
  } });
  await client.execute(base + '.play-sequence', { name: 'Wave.json' });
  assert.equal(sent.length, 2); assert.deepEqual(sent[0], sent[1]);
  assert.equal(JSON.parse(sent[0].body).expectedToken, 'library-1');
});
test('API rejection is shown without retrying a rejected mutation', async () => {
  let mutations = 0;
  const client = new EditorClient({ discover: async () => [41], request: async (id, body) => {
    if (body.method === 'get_status') return { ok: true, state };
    mutations++; return { ok: false, error: 'Editor context changed.' };
  } });
  await assert.rejects(client.execute(base + '.apply-pose', { name: 'Pose' }), /context changed/);
  assert.equal(mutations, 1);
});
test('simultaneous polling and rapid Start/Stop requests use one connection at a time', async () => {
  let connections = 0, maximum = 0; const order = [];
  const client = new EditorClient({ discover: async () => [41], request: async (id, body) => {
    connections++; maximum = Math.max(maximum, connections);
    await new Promise(resolve => setTimeout(resolve, 2)); connections--;
    if (body.method === 'get_status') return { ok: true, state };
    order.push(body); return { ok: true, status: 'completed' };
  } });
  await Promise.all([client.status(41), client.execute(base + '.play-sequence', { name: 'Wave' }), client.execute(base + '.stop', {}), client.status(41)]);
  assert.equal(maximum, 1); assert.deepEqual(order.map(x => x.method), ['play_library_sequence', 'stop']);
  assert.equal(order[1].expectedToken, 'transport-1');
  assert.notEqual(order[0].requestId, order[1].requestId);
});
test('named pipe JSON framing handles chunk boundaries and rejects malformed replies', async () => {
  function connectWith(chunks) {
    return () => {
      const socket = new EventEmitter(); socket.setEncoding = () => {}; socket.destroy = () => {};
      socket.write = data => { assert.equal(data, '{"method":"get_status"}\n'); for (const chunk of chunks) socket.emit('data', chunk); };
      queueMicrotask(() => socket.emit('connect')); return socket;
    };
  }
  assert.deepEqual(await requestPipe(12, { method: 'get_status' }, { connect: connectWith(['{"ok":', 'true}\n']) }), { ok: true });
  await assert.rejects(requestPipe(12, { method: 'get_status' }, { connect: connectWith(['bad\n']) }), /invalid JSON/);
  await assert.rejects(requestPipe(-1, {}), /valid editor/);
});
test('a busy pipe retries before sending, and a timeout cancels pending connection retries', async () => {
  let attempts = 0, writes = 0;
  const connect = () => {
    const socket = new EventEmitter(); socket.setEncoding = () => {}; socket.destroy = () => {};
    socket.write = () => { writes++; socket.emit('data', '{"ok":true}\n'); };
    const attempt = ++attempts;
    queueMicrotask(() => attempt === 1 ? socket.emit('error', Object.assign(new Error('busy'), { code: 'EBUSY' })) : socket.emit('connect'));
    return socket;
  };
  assert.deepEqual(await requestPipe(12, { method: 'stop' }, { connect }), { ok: true });
  assert.equal(attempts, 2); assert.equal(writes, 1);
  attempts = 0; writes = 0;
  await assert.rejects(requestPipe(12, { method: 'stop' }, { connect, timeout: 10 }), /in time/);
  await new Promise(resolve => setTimeout(resolve, 60));
  assert.equal(attempts, 1); assert.equal(writes, 0);
});
