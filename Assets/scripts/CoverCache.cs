using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class CoverCache
{
    static readonly Dictionary<string, Texture2D> cache = new();

    public static Texture2D Get(string path, Texture2D fallback = null)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return fallback;
        if (cache.TryGetValue(path, out var t) && t) return t;

        var bytes = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(bytes)) { Object.Destroy(tex); return fallback; }
        tex.wrapMode = TextureWrapMode.Clamp;
        cache[path] = tex;
        return tex;
    }
}