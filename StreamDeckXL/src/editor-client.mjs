import net from 'node:net';
import fs from 'node:fs/promises';
import { randomUUID } from 'node:crypto';

export const PIPE_ROOT = '\\\\.\\pipe\\';
export async function discoverEditors() {
  const names = await fs.readdir(PIPE_ROOT);
  return names.map(name => /^Johnny5\.AnimationEditor\.(\d+)$/.exec(name))
    .filter(Boolean).map(match => Number(match[1])).sort((a, b) => a - b);
}

export function requestPipe(processId, request, { timeout = 2500, connect = net.createConnection } = {}) {
  if (!Number.isSafeInteger(processId) || processId <= 0) return Promise.reject(new Error('Select a valid editor instance.'));
  return new Promise((resolve, reject) => {
    let socket, retryTimer, text = '', finished = false;
    const done = (error, result) => {
      if (finished) return;
      finished = true; clearTimeout(timer); clearTimeout(retryTimer); socket?.destroy();
      if (error) reject(error); else resolve(result);
    };
    const timer = setTimeout(() => done(new Error('Editor did not respond in time.')), timeout);
    const open = () => {
      if (finished) return;
      let connected = false;
      const current = connect(PIPE_ROOT + 'Johnny5.AnimationEditor.' + processId); socket = current;
      current.setEncoding('utf8');
      current.on('connect', () => { connected = true; if (!finished) current.write(JSON.stringify(request) + '\n'); });
      current.on('data', chunk => {
        text += chunk;
        if (Buffer.byteLength(text) > 8 * 1024 * 1024) return done(new Error('Editor response is too large.'));
        const end = text.indexOf('\n');
        if (end < 0) return;
        try { done(null, JSON.parse(text.slice(0, end))); }
        catch { done(new Error('Editor returned invalid JSON.')); }
      });
      current.on('error', error => {
        // Another same-user client may briefly own the server's single pipe.
        // Only retry connection acquisition here, before any command was sent.
        if (!finished && !connected && ['EBUSY', 'ENOENT'].includes(error.code)) {
          current.destroy(); retryTimer = setTimeout(open, 40); return;
        }
        done(new Error('Editor connection unavailable: ' + error.message));
      });
      current.on('end', () => { if (!finished && connected) done(new Error('Editor closed the connection without a reply.')); });
    };
    open();
  });
}

export function requireOk(reply) {
  if (reply?.ok !== true) throw new Error(reply?.error || 'Editor rejected the request.');
  return reply;
}

export function selectEditor(ids, selected = '') {
  if (selected !== '' && selected != null) {
    const id = Number(selected);
    if (!ids.includes(id)) throw new Error('Selected editor is closed. Select its replacement in button settings.');
    return id;
  }
  if (ids.length === 0) throw new Error('Open Animation Editor and enable File → Enable Codex Connection.');
  if (ids.length !== 1) throw new Error('Several editors are open. Select one in button settings.');
  return ids[0];
}

export function actionRequest(action, settings = {}) {
  if (action.endsWith('.stop')) return { method: 'stop' };
  const kind = action.endsWith('.apply-pose') ? 'pose' : action.endsWith('.play-sequence') ? 'sequence' : settings.kind === 'pose' ? 'pose' : 'sequence';
  const picker = action.endsWith('.choose-library');
  if (!picker && !settings.name) throw new Error('Choose a Library item in button settings.');
  return { method: kind === 'pose' ? 'apply_library_pose' : 'play_library_sequence',
    ...(picker ? {} : { name: settings.name }), loop: kind === 'sequence' && settings.loop === true,
    useEditorOutputs: true };
}

export class EditorClient {
  constructor({ discover = discoverEditors, request = requestPipe, streamDeckSession = null, streamDeckConnected = () => false } = {}) {
    this.streamDeckSession = streamDeckSession; this.streamDeckConnected = streamDeckConnected;
    this.discover = discover; this.connections = new Map();
    this.request = (id, body) => {
      const result = (this.connections.get(id) || Promise.resolve()).then(() => request(id, body));
      const settled = result.catch(() => {});
      this.connections.set(id, settled);
      settled.then(() => { if (this.connections.get(id) === settled) this.connections.delete(id); });
      return result;
    };
    this.queue = Promise.resolve();
  }
  async status(processId) {
    const response = await this.request(processId, this.streamDeckSession
      ? { method: 'stream_deck_status', streamDeckSession: this.streamDeckSession, enabled: this.streamDeckConnected() }
      : { method: 'get_status' });
    if (this.streamDeckSession && response?.ok === false && /Unknown API method/i.test(response.error || ''))
      throw new Error('Update Animation Editor to use the Stream Deck connection button.');
    const reply = requireOk(response);
    if (!reply.state?.methods?.includes('play_library_sequence')) throw new Error('Update Animation Editor to the Stream Deck API build.');
    return reply.state;
  }
  async library(processId, kind) {
    return requireOk(await this.request(processId, { method: 'list_library', libraryKind: kind })).items.filter(item => item.valid);
  }
  execute(action, settings) {
    // Preserve physical button order, including Stop following a start. A timeout
    // retries the identical ID against the identical process, never a new start.
    const execute = async () => {
      const processId = selectEditor(await this.discover(), settings.processId);
      const command = actionRequest(action, settings);
      const state = await this.status(processId);
      if (command.useEditorOutputs && !state.capabilities?.includes('editor_configured_library_outputs'))
        throw new Error('Update Animation Editor to use its Focus Control output settings with Stream Deck.');
      const request = { ...command, requestId: randomUUID(),
        ...(this.streamDeckSession ? { streamDeckSession: this.streamDeckSession } : {}),
        expectedToken: command.method === 'stop' ? state.transportToken : state.libraryToken };
      let result;
      try { result = await this.request(processId, request); }
      catch { result = await this.request(processId, request); }
      return { processId, requestId: request.requestId, result: requireOk(result) };
    };
    const result = this.queue.then(execute);
    this.queue = result.catch(() => {});
    return result;
  }
}
