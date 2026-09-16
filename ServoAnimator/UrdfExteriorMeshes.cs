using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ServoAnimator;

/// <summary>Validated offline masks omit only back faces of closed opaque solids.
/// Front geometry, normals, joint transforms and collision geometry stay original.</summary>
internal sealed class UrdfExteriorMeshes
{
    internal sealed class Entry
    {
        public string Source { get; set; }
        public string Mesh { get; set; }
        public string Sha256 { get; set; }
        public int Before { get; set; }
        public int After { get; set; }
    }
    private sealed class Manifest
    {
        public string Algorithm { get; set; }
        public string UrdfSha256 { get; set; }
        public Dictionary<string, string> Sources { get; set; }
        public Dictionary<string, Entry> Visuals { get; set; }
    }
    private Dictionary<string, Entry> _entries = new();
    private string _root;
    public int Count => _entries.Count;
    public static UrdfExteriorMeshes Load(string urdf)
    {
        var result = new UrdfExteriorMeshes { _root = Path.GetDirectoryName(Path.GetFullPath(urdf)) };
        try
        {
            string path = Path.Combine(result._root, "Meshes", "ExteriorOptimized", "manifest.json");
            if (!File.Exists(path)) return result;
            var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (manifest?.Algorithm != "closed-opaque-backfaces-v1" || !Matches(urdf, manifest.UrdfSha256) || manifest.Sources == null || manifest.Visuals == null) return result;
            // Validate the complete input snapshot, not just a target mesh.
            foreach (var source in manifest.Sources)
                if (!Matches(result.Resolve(source.Key), source.Value)) return result;
            foreach (var entry in manifest.Visuals.Values)
                if (!Matches(result.Resolve(entry.Mesh), entry.Sha256)) return result;
            result._entries = manifest.Visuals;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("URDF exterior optimization skipped: " + ex.Message); }
        return result;
    }
    private string Resolve(string relative)
    {
        string full = Path.GetFullPath(Path.Combine(_root, relative));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Exterior mesh path escapes the model folder.");
        return full;
    }
    private static bool Matches(string path, string expected)
    {
        using var file = File.OpenRead(path);
        return string.Equals(Convert.ToHexString(SHA256.HashData(file)), expected, StringComparison.OrdinalIgnoreCase);
    }
    public bool TryBackFaces(string key, string source, MeshGeometry3D original, out MeshGeometry3D back)
    {
        back = null;
        if (!_entries.TryGetValue(key, out var entry) || entry.Source != source || entry.Before != original.TriangleIndices.Count / 3) return false;
        try
        {
            byte[] bytes = File.ReadAllBytes(Resolve(entry.Mesh));
            if (bytes.Length != entry.After * 12L) return false;
            if (entry.After == 0) return true;
            var indices = new Int32Collection(bytes.Length / 4);
            for (int i = 0; i < bytes.Length; i += 4)
            {
                int index = BitConverter.ToInt32(bytes, i);
                if (index < 0 || index >= original.Positions.Count) return false;
                indices.Add(index);
            }
            // Share the frozen vertex/normal buffers; only the back-face index list differs.
            back = new MeshGeometry3D { Positions = original.Positions, Normals = original.Normals,
                TextureCoordinates = original.TextureCoordinates, TriangleIndices = indices };
            back.Freeze(); return true;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("URDF back-face mask skipped: " + ex.Message); return false; }
    }
}
