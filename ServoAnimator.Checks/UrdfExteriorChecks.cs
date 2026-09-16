using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Xml.Linq;
using ServoAnimator;

internal static partial class Program
{
    private static void UrdfExteriorChecks(bool render = true)
    {
        ExteriorSnapshotChecks();
        string urdf = Path.Combine(AppContext.BaseDirectory, "Models", "johnny5_head.urdf");
        Check(UrdfExteriorMeshes.Load(urdf).Count > 0, "Bundled masks must validate against the original URDF and meshes.");
        var original = UrdfScene.Load(urdf, optimizeExterior: false);
        var optimized = UrdfScene.Load(urdf);
        long Triangles(Model3D model) => model is Model3DGroup group ? group.Children.Sum(Triangles) : model is GeometryModel3D { Geometry: MeshGeometry3D mesh } g
            ? mesh.TriangleIndices.Count / 3L * ((g.Material != null ? 1 : 0) + (g.BackMaterial != null ? 1 : 0)) : 0;
        long before = Triangles(original.RootModel), after = Triangles(optimized.RootModel);
        Check(before - after == 836176, "Only the verified hidden back faces should be omitted.");
        Console.WriteLine($"Rendered triangle sides: {before:N0} → {after:N0}");
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var aProxies = (System.Collections.ICollection)typeof(UrdfScene).GetField("_collisionProxies", flags).GetValue(original);
        var bProxies = (System.Collections.ICollection)typeof(UrdfScene).GetField("_collisionProxies", flags).GetValue(optimized);
        Check(aProxies.Count == bProxies.Count, "Collision proxies retain original geometry.");
        string ProxyShape(object proxy) => proxy.GetType().GetProperty("Id").GetValue(proxy) + "|" + proxy.GetType().GetProperty("LocalBounds").GetValue(proxy) + "|" + ((Transform3D)proxy.GetType().GetProperty("LocalTransform").GetValue(proxy)).Value;
        Check(aProxies.Cast<object>().Select(ProxyShape).SequenceEqual(bProxies.Cast<object>().Select(ProxyShape)), "Every collision bound and local transform must match the full-detail scene.");
        if (!render) return;
        const int size = 192;
        string folder = Path.Combine(Environment.CurrentDirectory, "urdf-exterior-previews"); Directory.CreateDirectory(folder);
        var joints = XDocument.Load(urdf).Root.Elements("joint").Where(j => j.Attribute("type")?.Value != "fixed" && j.Element("limit") != null).ToArray();
        var bounds = original.RootModel.Bounds;
        var center = new Point3D(bounds.X + bounds.SizeX / 2, bounds.Y + bounds.SizeY / 2, bounds.Z + bounds.SizeZ / 2);
        double distance = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ)) * 2.7;
        var directions = new[] { new Vector3D(1, 0, 0), new Vector3D(-1, 0, 0), new Vector3D(0, 1, 0), new Vector3D(0, -1, 0),
            new Vector3D(1, 1, 0.7), new Vector3D(-1, -1, 0.7), new Vector3D(0.1, 0, 1), new Vector3D(0.1, 0, -1) };
        for (int pose = 0; pose < 3; pose++)
        {
            foreach (var joint in joints)
            {
                double value = pose == 0 ? 0 : double.Parse(joint.Element("limit").Attribute(pose == 1 ? "lower" : "upper")?.Value ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                original.SetJoint(joint.Attribute("name").Value, value); optimized.SetJoint(joint.Attribute("name").Value, value);
            }
            for (int view = 0; view < directions.Length; view++)
            {
                if (pose != view % 3) continue; // eight directions distributed across neutral/lower/upper poses
                var direction = directions[view]; direction.Normalize();
                var full = Render(original, direction); var reduced = Render(optimized, direction);
                byte[] fullPixels = new byte[size * size * 4], reducedPixels = new byte[fullPixels.Length];
                full.CopyPixels(fullPixels, size * 4, 0); reduced.CopyPixels(reducedPixels, size * 4, 0);
                int changed = 0, max = 0;
                for (int i = 0; i < fullPixels.Length; i++) { int delta = Math.Abs(fullPixels[i] - reducedPixels[i]); if (delta > 2) changed++; max = Math.Max(max, delta); }
                Console.WriteLine($"Pose {pose}, view {view}: {changed} changed channels; max difference {max}");
                Save(full, $"pose{pose}-view{view}-original.png"); Save(reduced, $"pose{pose}-view{view}-optimized.png");
                Check(changed == 0, "Exterior rendering must match the original at all tested viewing angles and joint extremes.");
            }
        }
        RenderTargetBitmap Render(UrdfScene scene, Vector3D direction)
        {
            var viewport = new Viewport3D { Camera = new PerspectiveCamera(center + direction * distance, -direction, new Vector3D(0, 0, 1), 35) { NearPlaneDistance = 0.001, FarPlaneDistance = 100 } };
            viewport.Children.Add(new ModelVisual3D { Content = scene.RootModel });
            var lights = new Model3DGroup(); lights.Children.Add(new AmbientLight(Color.FromRgb(120, 120, 120))); lights.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -1, -2)));
            viewport.Children.Add(new ModelVisual3D { Content = lights });
            var grid = new Grid { Background = new SolidColorBrush(Color.FromRgb(25, 30, 39)) }; grid.Children.Add(viewport);
            grid.Measure(new Size(size, size)); grid.Arrange(new Rect(0, 0, size, size)); grid.UpdateLayout();
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32); bitmap.Render(grid); return bitmap;
        }
        void Save(BitmapSource bitmap, string name) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(folder, name)); encoder.Save(file); }
    }

    private static void ExteriorSnapshotChecks()
    {
        string root = Path.Combine(Path.GetTempPath(), "j5-exterior-checks-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "Meshes", "ExteriorOptimized"); Directory.CreateDirectory(folder);
        string urdf = Path.Combine(root, "model.urdf"), source = Path.Combine(root, "source.stl"), mask = Path.Combine(folder, "back.bin");
        try
        {
            File.WriteAllText(urdf, "<robot/>"); File.WriteAllText(source, "source"); File.WriteAllBytes(mask, Array.Empty<byte>());
            string Hash(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
            File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonSerializer.Serialize(new {
                algorithm = "closed-opaque-backfaces-v1", urdfSha256 = Hash(urdf), sources = new Dictionary<string, string> { ["source.stl"] = Hash(source) },
                visuals = new Dictionary<string, object> { ["link/visual"] = new { source = "source.stl", mesh = "Meshes/ExteriorOptimized/back.bin", sha256 = Hash(mask), before = 1, after = 0 } } }));
            Check(UrdfExteriorMeshes.Load(urdf).Count == 1, "Unchanged source snapshot enables exterior masks.");
            File.WriteAllText(urdf, "<robot name='changed'/>");
            Check(UrdfExteriorMeshes.Load(urdf).Count == 0, "Changed URDF must fall back to full rendering.");
            File.WriteAllText(urdf, "<robot/>"); File.WriteAllText(source, "changed");
            Check(UrdfExteriorMeshes.Load(urdf).Count == 0, "Changed source mesh must fall back to full rendering.");
            File.WriteAllText(source, "source"); File.WriteAllText(mask, "changed");
            Check(UrdfExteriorMeshes.Load(urdf).Count == 0, "Changed mask must fall back to full rendering.");
        }
        finally
        {
            if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
        }
    }
}
