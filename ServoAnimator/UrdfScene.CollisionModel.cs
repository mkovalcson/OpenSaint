using System.Windows.Media.Media3D;

namespace ServoAnimator;

internal sealed partial class UrdfScene
{
    private sealed record CollisionShape(CollisionProxy Proxy, Rect3D Bounds, CollisionKind Kind,
        CollisionEyeSide EyeSide = CollisionEyeSide.None);

    private CollisionShape[] _collisionShapes;
    private CollisionModel _collisionModel;
    private CollisionSession _warningCollisionSession;

    // Only this bridge reads WPF objects. The cached model and its per-request
    // sessions contain numeric values, strings and arrays, never visual objects.
    internal CollisionSession CaptureCollisionSession()
    {
        var session = new CollisionSession(this);
        CaptureCollisionPose(session);
        return session;
    }

    private void CaptureCollisionPose(CollisionSession session)
    {
        foreach (var link in _links)
            session.SetLinkTransform(link.Key, link.Value.Transform?.Value ?? Matrix3D.Identity);
        foreach (var joint in _joints)
            session.SetJoint(joint.Key, joint.Value.Position);
    }

    private CollisionModel GetCollisionModel()
    {
        if (_collisionModel != null) return _collisionModel;
        _collisionShapes ??= BuildCollisionShapes().ToArray();
        var nodes = new List<CollisionNode>();
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        int AddLink(string name)
        {
            if (indices.TryGetValue(name, out int existing)) return existing;
            _parentJointByChild.TryGetValue(name, out var joint);
            int parent = joint == null ? -1 : AddLink(joint.ParentLink);
            int index = nodes.Count;
            nodes.Add(new(name, parent, joint?.Name, joint?.JointType, joint?.Axis ?? default,
                joint?.OriginMatrix ?? Matrix3D.Identity));
            indices.Add(name, index);
            return index;
        }
        foreach (string name in _links.Keys) AddLink(name);
        var shapes = _collisionShapes.Select(s => new NumericShape(indices[s.Proxy.LinkName],
            s.Bounds, s.Proxy.LocalTransform?.Value ?? Matrix3D.Identity)).ToArray();
        var candidates = _collisionShapes.Select(s => new CollisionCandidate(s.Proxy, null, s.Kind, s.EyeSide)).ToArray();
        var pairs = new List<CollisionPair>();
        for (int i = 0; i < candidates.Length; i++)
        for (int j = i + 1; j < candidates.Length; j++)
        {
            var a = candidates[i]; var b = candidates[j];
            if (a.Proxy.LinkName == b.Proxy.LinkName || !ShouldCheckCollisionPair(a, b, true, true)) continue;
            string key = PairKey($"{a.Proxy.Id}#{a.Kind}", $"{b.Proxy.Id}#{b.Kind}");
            int mask = 0;
            for (int state = 0; state < 4; state++)
                if (ShouldCheckCollisionPair(a, b, (state & 1) != 0, (state & 2) != 0)) mask |= 1 << state;
            pairs.Add(new(i, j, mask, key, _baselineCollisionPairs.Contains(key)));
        }
        return _collisionModel = new(nodes.ToArray(), shapes, pairs.ToArray());
    }

    private List<CollisionHit> DetectCollisionPairs(bool ignoreBaseline, bool leftEyePoppedOut, bool rightEyePoppedOut)
    {
        _warningCollisionSession ??= new CollisionSession(this);
        CaptureCollisionPose(_warningCollisionSession);
        var hits = new List<CollisionHit>();
        _warningCollisionSession.VisitCollisions(ignoreBaseline, leftEyePoppedOut, rightEyePoppedOut, pair =>
        {
            hits.Add(new(_collisionShapes[pair.A].Proxy, _collisionShapes[pair.B].Proxy, pair.Key));
        });
        return hits;
    }

    private sealed record CollisionNode(string Name, int Parent, string Joint, string Type, Vector3D Axis, Matrix3D Origin);
    private readonly record struct NumericShape(int Link, Rect3D Bounds, Matrix3D Local);
    internal readonly record struct CollisionPair(int A, int B, int EyeMask, string Key, bool Baseline);
    private sealed record CollisionModel(CollisionNode[] Nodes, NumericShape[] Shapes, CollisionPair[] Pairs);

    // A session owns all mutable state. Trial motion never calls SetControl,
    // changes a WPF transform, updates a servo target, or restores the display.
    internal sealed class CollisionSession
    {
        private readonly CollisionModel _model;
        private readonly Dictionary<string, int> _links = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _joints = new(StringComparer.Ordinal);
        private readonly Matrix3D[] _linkLocal, _jointLocal, _world;
        private readonly double[] _positions;
        private readonly bool[] _dirty, _changed;
        private readonly OrientedBox[] _boxes;

        internal CollisionSession(UrdfScene scene) : this(scene.GetCollisionModel()) { }

        private CollisionSession(CollisionModel model)
        {
            _model = model;
            int count = model.Nodes.Length;
            _linkLocal = new Matrix3D[count]; _jointLocal = new Matrix3D[count]; _world = new Matrix3D[count];
            _positions = new double[count]; _dirty = new bool[count]; _changed = new bool[count];
            for (int i = 0; i < count; i++)
            {
                var node = model.Nodes[i];
                _links.Add(node.Name, i);
                if (node.Joint != null) _joints.Add(node.Joint, i);
                _linkLocal[i] = Matrix3D.Identity; _jointLocal[i] = node.Origin;
                _positions[i] = double.NaN; _dirty[i] = true;
            }
            _boxes = model.Shapes.Select(_ => new OrientedBox(default, new Vector3D[3], new double[3], default)).ToArray();
        }

        internal bool SetJoint(string name, double position)
        {
            if (!double.IsFinite(position) || !_joints.TryGetValue(name, out int i)) return false;
            if (_positions[i] == position) return true;
            _positions[i] = position;
            var node = _model.Nodes[i];
            var matrix = Matrix3D.Identity;
            if (node.Type is "revolute" or "continuous") matrix.Rotate(new Quaternion(node.Axis, position / Deg));
            else if (node.Type == "prismatic") matrix.Translate(node.Axis * position);
            matrix.Append(node.Origin);
            _jointLocal[i] = matrix; _dirty[i] = true;
            return true;
        }

        internal void SetLinkTransform(string name, Matrix3D matrix)
        {
            int i = _links[name];
            if (_linkLocal[i] == matrix) return;
            _linkLocal[i] = matrix; _dirty[i] = true;
        }

        private void UpdateBoxes()
        {
            // Parent-before-child traversal computes each link transform once.
            // Unchanged branches retain their world transforms and boxes.
            for (int i = 0; i < _model.Nodes.Length; i++)
            {
                int parent = _model.Nodes[i].Parent;
                _changed[i] = _dirty[i] || (parent >= 0 && _changed[parent]);
                if (!_changed[i]) continue;
                var world = _linkLocal[i]; world.Append(_jointLocal[i]);
                if (parent >= 0) world.Append(_world[parent]);
                _world[i] = world; _dirty[i] = false;
            }
            for (int i = 0; i < _boxes.Length; i++)
            {
                var shape = _model.Shapes[i];
                if (!_changed[shape.Link]) continue;
                var matrix = shape.Local; matrix.Append(_world[shape.Link]);
                UpdateBox(_boxes[i], shape.Bounds, matrix);
            }
        }

        internal bool HasCollision(bool left, bool right) => VisitCollisions(true, left, right, null);

        internal bool VisitCollisions(bool ignoreBaseline, bool left, bool right, Action<CollisionPair> visit)
        {
            UpdateBoxes();
            int mask = 1 << ((left ? 1 : 0) | (right ? 2 : 0));
            bool hit = false;
            foreach (var pair in _model.Pairs)
            {
                if ((pair.EyeMask & mask) == 0 || (ignoreBaseline && pair.Baseline)) continue;
                var a = _boxes[pair.A]; var b = _boxes[pair.B];
                if (!AabbIntersects(a.Aabb, b.Aabb) || !OrientedBoxesIntersect(a, b)) continue;
                if (visit == null) return true; // Guard needs only the first hit.
                visit(pair); hit = true;
            }
            return hit;
        }

        private static void UpdateBox(OrientedBox box, Rect3D bounds, Matrix3D matrix)
        {
            var local = new Point3D(bounds.X + bounds.SizeX / 2, bounds.Y + bounds.SizeY / 2, bounds.Z + bounds.SizeZ / 2);
            box.Center = matrix.Transform(local);
            for (int i = 0; i < 3; i++)
            {
                var point = local;
                if (i == 0) point.X++; else if (i == 1) point.Y++; else point.Z++;
                var axis = matrix.Transform(point) - box.Center;
                double scale = axis.Length;
                if (scale < 1e-12) { axis = i == 0 ? new(1, 0, 0) : i == 1 ? new(0, 1, 0) : new(0, 0, 1); scale = 1; }
                else axis.Normalize();
                box.Axis[i] = axis;
                box.Half[i] = (i == 0 ? bounds.SizeX : i == 1 ? bounds.SizeY : bounds.SizeZ) / 2 * scale;
            }
            var aabb = Rect3D.Empty;
            for (int corner = 0; corner < 8; corner++)
                aabb.Union(matrix.Transform(new Point3D(bounds.X + ((corner & 1) != 0 ? bounds.SizeX : 0),
                    bounds.Y + ((corner & 2) != 0 ? bounds.SizeY : 0), bounds.Z + ((corner & 4) != 0 ? bounds.SizeZ : 0))));
            box.Aabb = aabb;
        }
    }
}
