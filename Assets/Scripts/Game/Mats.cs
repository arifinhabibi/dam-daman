using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pamor
{
    // BELAJAR UNITY - MATERIAL:
    // Mesh (bentuk 3D) hanya menentukan BENTUK. Warna, kilap, dan "bahan" ditentukan oleh
    // Material. Material memakai Shader (program kecil yang menghitung tampilan di GPU).
    // Proyek ini memakai URP (Universal Render Pipeline), jadi shader-nya "URP/Lit": bahan
    // yang bereaksi terhadap cahaya dan bayangan.
    //
    // Membuat Material baru itu mahal dan boros memori kalau dilakukan berkali-kali untuk warna
    // yang sama. Karena itu class ini menyimpan (cache) material per kombinasi warna.

    /// <summary>
    /// Shared URP Lit materials built from palette colours, created once per colour and reused.
    /// Based on the pipeline's default material, so the Lit shader is always included in builds.
    /// </summary>
    public static class Mats
    {
        // Dictionary = daftar "kunci -> nilai". Kuncinya gabungan warna+kilap+metalik+emisi.
        static readonly Dictionary<(Color, float, float, float), Material> cache = new Dictionary<(Color, float, float, float), Material>();

        // Shader.PropertyToID mengubah nama properti shader (teks) menjadi nomor. Memanggil
        // material.SetColor dengan nomor lebih cepat daripada dengan teks, jadi dihitung sekali.
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int Metallic = Shader.PropertyToID("_Metallic");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        /// <summary>A lit material. Emission above 0 makes it glow (HDR, so it can bloom).</summary>
        public static Material Lit(Color color, float smoothness = 0.35f, float metallic = 0f, float emission = 0f)
        {
            var key = (color, smoothness, metallic, emission);
            // Kalau sudah pernah dibuat, pakai lagi (cek "m != null" karena Unity bisa menghancurkan objek).
            if (cache.TryGetValue(key, out var m) && m != null) return m;

            // Salin dari material bawaan pipeline supaya shader Lit pasti ikut ke dalam build game.
            m = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial) { name = "Pamor " + ColorUtility.ToHtmlStringRGB(color) };
            m.SetColor(BaseColor, color);
            m.SetFloat(Smoothness, smoothness);   // 0 = kasar/doff, 1 = mengilap
            m.SetFloat(Metallic, metallic);       // 0 = bukan logam, 1 = logam
            if (emission > 0f)
            {
                // Emisi = material "memancarkan" cahaya sendiri (dipakai untuk tanda tujuan yang menyala).
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
            // CreatePrimitive membuat bentuk bawaan Unity (Cube, Sphere, Cylinder, ...) sebagai
            // GameObject lengkap dengan MeshFilter, MeshRenderer, dan Collider.
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            // Collider dipakai untuk fisika/klik. Papan ini memilih titik lewat perhitungan sendiri,
            // jadi Collider-nya tidak perlu dan dibuang supaya lebih ringan.
            Object.Destroy(go.GetComponent<Collider>());

            // SetParent(parent, false): jadikan anak dari "parent". "false" = pertahankan posisi LOKAL
            // (relatif terhadap parent), bukan posisi dunia. Setelah itu localPosition/localScale
            // dihitung relatif terhadap parent.
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            // MeshRenderer yang menggambar mesh. sharedMaterial = pakai material bersama
            // (kalau "material" biasa, Unity membuat salinan baru tiap objek).
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;   // objek ini menghasilkan bayangan
            r.receiveShadows = true;                      // dan menerima bayangan dari objek lain
            return go.transform;
        }
    }
}
