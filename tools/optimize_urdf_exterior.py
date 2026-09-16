"""Prepare back-side render meshes, excluding verified closed opaque solids.

Original front surfaces and collision meshes are never changed. Closed components
must have exact welded vertices, two oppositely directed faces per edge, and
positive signed volume. Open/inward/non-manifold geometry keeps its back faces.
Requires numpy; run from any folder. Outputs are validated against source hashes.
"""
import os
os.environ.setdefault('OPENBLAS_NUM_THREADS', '1')
import hashlib, json, pathlib, struct, xml.etree.ElementTree as ET
import numpy as np

ROOT = pathlib.Path(__file__).resolve().parents[1] / 'ServoAnimator' / 'Models'
DT = np.dtype([('normal', '<f4', (3,)), ('points', '<f4', (3, 3)), ('attribute', '<u2')])

def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def read_stl(path):
    data = path.read_bytes()
    n = struct.unpack('<I', data[80:84])[0] if len(data) >= 84 else -1
    if 84 + n * 50 == len(data): return np.frombuffer(data, DT, offset=84).copy()
    points = [list(map(float, line.split()[1:])) for line in data.decode().splitlines() if line.strip().startswith('vertex ')]
    points = np.asarray(points, dtype=np.float32).reshape(-1, 3, 3)
    result = np.zeros(len(points), DT); result['points'] = points
    normals = np.cross(points[:, 1] - points[:, 0], points[:, 2] - points[:, 0])
    lengths = np.linalg.norm(normals, axis=1); normals /= np.maximum(lengths[:, None], 1e-30)
    result['normal'] = normals
    return result

def components(points):
    # Exact vertex welding: cracks are not guessed shut to manufacture occluders.
    vertices, index = np.unique(points.reshape(-1, 3), axis=0, return_inverse=True)
    faces = index.reshape(-1, 3); parents = list(range(len(faces)))
    def find(a):
        while parents[a] != a: parents[a] = parents[parents[a]]; a = parents[a]
        return a
    # Components share edges, not merely a vertex: two touching solids may have
    # independent winding and must have their signed volumes checked separately.
    edges = np.sort(np.concatenate([faces[:, [0, 1]], faces[:, [1, 2]], faces[:, [2, 0]]]), axis=1)
    keys = edges[:, 0].astype(np.int64) * len(vertices) + edges[:, 1]
    order = np.argsort(keys); owners = np.tile(np.arange(len(faces)), 3)
    same = np.flatnonzero(keys[order][1:] == keys[order][:-1])
    for k in same:
        a, b = find(owners[order[k]]), find(owners[order[k + 1]])
        parents[b] = a
    roots = np.array([find(a) for a in range(len(faces))])
    order = np.argsort(roots); starts = np.r_[0, np.flatnonzero(np.diff(roots[order])) + 1, len(order)]
    for begin, end in zip(starts[:-1], starts[1:]):
        selected = order[begin:end]
        yield selected, faces[selected], vertices

def hidden_back_faces(points):
    hidden = np.zeros(len(points), dtype=bool)
    for ids, faces, all_vertices in components(points):
        if len(ids) < 4: continue
        directed = np.concatenate([faces[:, [0, 1]], faces[:, [1, 2]], faces[:, [2, 0]]])
        edges, inverse, counts = np.unique(np.sort(directed, axis=1), axis=0, return_inverse=True, return_counts=True)
        if np.any(counts != 2) or np.any(edges[:, 0] == edges[:, 1]): continue
        direction = np.where(directed[:, 0] < directed[:, 1], 1, -1)
        if np.any(np.bincount(inverse, weights=direction) != 0): continue
        triangles = points[ids]
        centered = triangles - triangles.mean(axis=(0, 1))
        volume = np.sum(centered[:, 0] * np.cross(centered[:, 1], centered[:, 2])) / 6
        if volume <= 1e-18: continue
        hidden[ids] = True
    return hidden

def transform(visual, mesh, points):
    origin = visual.find('origin')
    xyz = np.fromstring(origin.get('xyz', '0 0 0') if origin is not None else '0 0 0', sep=' ')
    r, p, y = np.fromstring(origin.get('rpy', '0 0 0') if origin is not None else '0 0 0', sep=' ')
    cr, sr, cp, sp, cy, sy = np.cos(r), np.sin(r), np.cos(p), np.sin(p), np.cos(y), np.sin(y)
    rotation = np.array([[cy*cp, cy*sp*sr-sy*cr, cy*sp*cr+sy*sr], [sy*cp, sy*sp*sr+cy*cr, sy*sp*cr-cy*sr], [-sp, cp*sr, cp*cr]])
    scale = np.fromstring(mesh.get('scale', '1 1 1'), sep=' ')
    return (points.astype(float) * scale) @ rotation.T + xyz

def main():
    urdf = ROOT / 'johnny5_head.urdf'; robot = ET.parse(urdf).getroot()
    opacity = {m.get('name'): float(m.find('color').get('rgba').split()[-1]) for m in robot.findall('material') if m.find('color') is not None}
    output = ROOT / 'Meshes' / 'ExteriorOptimized'; output.mkdir(exist_ok=True)
    manifest = {'urdfSha256': digest(urdf), 'sources': {}, 'visuals': {}, 'statistics': {}}
    cache = {}; original = removed = 0
    manifest['algorithm'] = 'closed-opaque-backfaces-v1'
    for link in robot.findall('link'):
        count = 0
        for visual in link.findall('visual'):
            mesh = visual.find('geometry/mesh'); material = visual.find('material')
            if mesh is None: continue
            source = mesh.get('filename'); path = ROOT / source
            if path.suffix.lower() != '.stl': continue
            if source not in cache: cache[source] = read_stl(path); manifest['sources'][source] = digest(path)
            data = cache[source]; original += len(data)
            name = material.get('name', '') if material is not None else ''
            if opacity.get(name, 0) < 1 or 'dynamic' in name: continue
            points = transform(visual, mesh, data['points'])
            hidden = hidden_back_faces(points); dropped = int(hidden.sum())
            if not dropped: continue
            key = link.get('name')+'/'+visual.get('name', '')
            kept = data[~hidden]; filename = hashlib.sha256(key.encode()).hexdigest()[:20]+'.backfaces.bin'
            target = output / filename
            indices = (np.flatnonzero(~hidden)[:, None] * 3 + np.arange(3)).astype('<u4')
            target.write_bytes(indices.tobytes())
            old = output / (hashlib.sha256(key.encode()).hexdigest()[:20]+'.stl')
            if old.exists() and old.resolve().parent == output.resolve(): old.unlink()
            manifest['visuals'][key] = {'source': source, 'mesh': target.relative_to(ROOT).as_posix(), 'sha256': digest(target), 'before': len(data), 'after': len(kept)}
            removed += dropped; count += dropped
        if count: print(f"{link.get('name')}: {count:,} hidden back-side triangles omitted", flush=True)
    manifest['statistics'] = dict(originalStlTriangles=original, omittedBackTriangles=removed,
        originalTwoSidedTriangles=original*2, optimizedTwoSidedTriangles=original*2-removed)
    (output/'manifest.json').write_text(json.dumps(manifest, indent=2)+'\n')
    print(json.dumps(manifest['statistics']), flush=True)

if __name__ == '__main__': main()
