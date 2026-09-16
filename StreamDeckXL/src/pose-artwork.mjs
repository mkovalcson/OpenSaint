// Share artwork across buttons and refresh changed Library images without per-frame decoding.
export class PoseArtwork {
  constructor(load, now = Date.now) { this.load = load; this.now = now; this.cache = new Map(); }
  async get(processId, name, refresh = false) {
    const key = JSON.stringify([processId, name]);
    let entry = this.cache.get(key);
    if (!entry || (refresh || entry.expires <= this.now()) && !entry.pending) {
      if (this.cache.size >= 128) this.cache.clear();
      entry = { expires: this.now() + 30000, pending: true };
      entry.value = Promise.resolve().then(() => this.load(processId, name)).catch(() => null)
        .finally(() => { entry.pending = false; });
      this.cache.set(key, entry);
    }
    return entry.value;
  }
}

export function buttonTitle(label) {
  const clean = String(label).replace(/\s+/g, ' ').trim();
  if (clean.length <= 12) return clean;
  const boundary = clean.lastIndexOf(' ', 12);
  const split = boundary >= 5 ? boundary : 12;
  const first = clean.slice(0, split), rest = clean.slice(split).trim();
  return first + '\n' + (rest.length > 12 ? rest.slice(0, 11) + '…' : rest);
}

export function poseImage(images, state, selected) {
  if (!images) return null;
  if (!state) return images.disconnected;
  const active = state.library?.running && selected && state.library.name?.replaceAll('\\', '/') === selected.replaceAll('\\', '/');
  return active ? images.active : state.library?.running ? images.running : images.connected;
}
