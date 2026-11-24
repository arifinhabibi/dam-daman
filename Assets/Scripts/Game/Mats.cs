using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pamor
{
    /// <summary>
    /// Shared URP Lit materials built from palette colours, created once per colour and reused.
    /// Based on the pipeline's default material, so the Lit shader is always included in builds.
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<(Color, float, float, float), Material> cache = new Dictionary<(Color, float, float, float), Material>();
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int Metallic = Shader.PropertyToID("_Metallic");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        /// <summary>A lit material. Emission above 0 makes it glow (HDR, so it can bloom).</summary>
        public static Material Lit(Color color, float smoothness = 0.35f, float metallic = 0f, float emission = 0f)
        {
            var key = (color, smoothness, metallic, emission);
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial) { name = "Pamor " + ColorUtility.ToHtmlStringRGB(color) };
            m.SetColor(BaseColor, color);
            m.SetFloat(Smoothness, smoothness);
            m.SetFloat(Metallic, metallic);
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor(EmissionColor, color * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            cache[key] = m;
            return m;
        }

        /// <summary>A primitive without its collider, coloured with a shared material.</summary>
        public static Transform Prim(PrimitiveType type, Transform parent, string name, Material mat, Vector3 localPos, Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            return go.transform;
        }
    }
}
