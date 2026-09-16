let socket, context, action, settings = {}, items = [];
const el = id => document.getElementById(id);
function send(event, payload) {
  if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify({ event, action, context, payload }));
}
function save() {
  settings = { ...settings, processId: el('processId').value, kind: el('kind').value,
    name: el('name').value, label: el('label').value, loop: el('loop').checked };
  delete settings.driveUrdf; delete settings.drivePhysical;
  send('setSettings', settings); layout();
}
function layout() {
  const pose = action?.endsWith('.apply-pose'), stop = action?.endsWith('.stop'), choose = action?.endsWith('.choose-library');
  el('heading').textContent = stop ? 'Stop Playback' : pose ? 'Apply Library Pose' : choose ? 'Choose Pose / Sequence' : 'Play Library Sequence';
  el('chooseSection').hidden = !choose; el('librarySection').hidden = stop || choose;
  el('playbackSection').hidden = stop; el('stopHelp').hidden = !stop;
  el('loopRow').hidden = pose || (choose && settings.kind === 'pose');
  el('libraryLabel').textContent = pose ? 'Library pose' : 'Library sequence';
}
function drawItems() {
  const selected = settings.name || '', filter = el('filter').value.toLowerCase();
  el('name').replaceChildren(new Option('Choose an item…', ''));
  const visible = items.filter(item => item.path.toLowerCase().includes(filter) || item.path === selected);
  for (const item of visible) el('name').add(new Option(item.path.replace(/\.json$/i, ''), item.path));
  if (selected && !items.some(item => item.path === selected)) el('name').add(new Option(selected + ' · unavailable', selected));
  el('name').value = selected;
  el('description').textContent = items.find(item => item.path === selected)?.description || (items.length ? `${items.length} Library items available` : 'Refresh after connecting to the editor.');
}
function applySettings(next) {
  settings = next || {};
  const processId = String(settings.processId || '');
  if (![...el('processId').options].some(o => o.value === processId)) el('processId').add(new Option('Editor ' + processId, processId));
  el('processId').value = processId; el('kind').value = settings.kind === 'pose' ? 'pose' : 'sequence';
  el('label').value = settings.label || ''; el('loop').checked = settings.loop === true;
  layout(); drawItems();
}
function receive(payload) {
  if (payload.type === 'editors') {
    const selected = String(settings.processId || '');
    el('processId').replaceChildren(new Option('Automatic · only open editor', ''));
    for (const id of payload.editors) el('processId').add(new Option('Editor ' + id, String(id)));
    if (selected && !payload.editors.includes(Number(selected))) el('processId').add(new Option('Editor ' + selected + ' · closed', selected));
    el('processId').value = selected;
  } else if (payload.type === 'library') { items = payload.items || []; drawItems(); }
  else if (payload.type === 'status') {
    const state = payload.state;
    el('connection').className = 'status connected';
    el('connectionText').textContent = `Connected · Editor ${state.processId} · v${state.version}`;
    el('activity').textContent = state.streamDeck && !state.streamDeck.enabled ? 'Stream Deck control is disabled or disconnected. Check its button in the editor’s Hardware section.'
      : state.library?.running ? `Playing ${state.library.name}${state.library.loop ? ' · looping' : ''} · ${Math.floor(state.library.positionSeconds)} s`
      : state.library?.selecting ? 'Choose an item in the editor’s Library window.' : 'Ready. Press the physical Stream Deck key to run this action.';
  } else if (payload.type === 'error') {
    el('connection').className = 'status error'; el('connectionText').textContent = payload.message;
  } else if (payload.type === 'receipt') {
    el('activity').textContent = payload.status === 'awaiting_selection' ? 'Choose an item in the editor’s Library window.' : payload.status === 'cancelled' ? 'Selection cancelled.' : 'Command completed.';
  }
}
window.connectElgatoStreamDeckSocket = function(port, uuid, registerEvent, info, actionInfo) {
  const instance = JSON.parse(actionInfo); context = uuid; action = instance.action;
  applySettings(instance.payload?.settings);
  socket = new WebSocket('ws://127.0.0.1:' + port);
  socket.onopen = () => { socket.send(JSON.stringify({ event: registerEvent, uuid })); send('sendToPlugin', { type: 'refresh' }); };
  socket.onmessage = event => {
    const message = JSON.parse(event.data);
    if (message.event === 'sendToPropertyInspector') receive(message.payload);
    if (message.event === 'didReceiveSettings') applySettings(message.payload.settings);
  };
  socket.onclose = () => receive({ type: 'error', message: 'Stream Deck connection closed.' });
};
for (const id of ['processId', 'kind', 'name', 'label', 'loop']) el(id).addEventListener('change', () => {
  if (id === 'kind' || id === 'processId') { settings.name = ''; items = []; drawItems(); }
  save();
  if (id === 'kind' || id === 'processId') send('sendToPlugin', { type: 'refresh' });
});
el('refresh').addEventListener('click', () => send('sendToPlugin', { type: 'refresh' }));
el('filter').addEventListener('input', drawItems);
