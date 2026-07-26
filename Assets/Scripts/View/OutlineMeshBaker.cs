using System.Collections.Generic;
using UnityEngine;

// Produces a copy of a mesh whose normals are smoothed (averaged across coincident
// vertices), so an inverted-hull outline doesn't crack at hard edges. Cached per
// source mesh; baked once, shared by every piece using that mesh
public static class OutlineMeshBaker
{
    private static readonly Dictionary<Mesh, Mesh> _cache = new Dictionary<Mesh, Mesh>();

    public static Mesh GetSmoothed(Mesh source)
    {
        if (source == null) return null;
        if (_cache.TryGetValue(source, out Mesh cached) && cached != null)
            return cached;

        Mesh baked = BuildSmoothed(source);
        _cache[source] = baked;
        return baked;
    }

    private static Mesh BuildSmoothed(Mesh source)
    {
        Mesh smoothed = Object.Instantiate(source);       // copies verts/tris/uv/submeshes
        smoothed.name = source.name + "_SmoothedOutline";

        if (smoothed.normals == null || smoothed.normals.Length != smoothed.vertexCount)
            smoothed.RecalculateNormals();

        Vector3[] verts = smoothed.vertices;
        Vector3[] normals = smoothed.normals;

        // Group vertex indices by welded position (quantized), average each group's normals.
        var groups = new Dictionary<Vector3Int, List<int>>();
        const float quant = 10000f;   // ~0.0001-unit weld tolerance
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 v = verts[i];
            var key = new Vector3Int(
                Mathf.RoundToInt(v.x * quant),
                Mathf.RoundToInt(v.y * quant),
                Mathf.RoundToInt(v.z * quant));
            if (!groups.TryGetValue(key, out List<int> list))
                groups[key] = list = new List<int>();
            list.Add(i);
        }

        Vector3[] result = new Vector3[verts.Length];
        foreach (List<int> group in groups.Values)
        {
            Vector3 sum = Vector3.zero;
            foreach (int i in group) sum += normals[i];
            Vector3 avg = sum.sqrMagnitude > 1e-12f ? sum.normalized : Vector3.up;
            foreach (int i in group) result[i] = avg;
        }

        smoothed.normals = result;
        return smoothed;
    }
}