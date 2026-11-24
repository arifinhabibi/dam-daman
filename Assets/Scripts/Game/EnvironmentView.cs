using UnityEngine;

namespace Pamor
{
    /// <summary>
    /// The daylight setting around the board: a stepped andesite plinth in a stone courtyard on a meadow,
    /// a split candi gate (candi bentar) with low walls behind each player's side, tropical trees, rocks,
    /// bushes and green hills on the horizon. Built from primitives at start-up, colours from the Palette.
    /// </summary>
    public class EnvironmentView : MonoBehaviour
    {
        [SerializeField] int seed = 7;

        void Start() => Build();

        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            var grass = Mats.Lit(Palette.GrassLight, 0.15f);
            var hills = Mats.Lit(Palette.GrassDark, 0.1f);
            var leaf = Mats.Lit(Palette.Leaf, 0.2f);
            var leafLight = Mats.Lit(Color.Lerp(Palette.Leaf, Palette.GrassLight, 0.45f), 0.2f);
            var trunk = Mats.Lit(Palette.Trunk, 0.25f);
            var stone = Mats.Lit(Palette.Stone, 0.2f);
            var stoneDark = Mats.Lit(Palette.StoneDark, 0.15f);
            var paving = Mats.Lit(Color.Lerp(Palette.Stone, Palette.Kelir, 0.35f), 0.1f);

            // Ground and courtyard.
            P(PrimitiveType.Cylinder, "Meadow", grass, new Vector3(0, -1.8f, 0), new Vector3(150, 0.1f, 150));
            P(PrimitiveType.Cube, "Courtyard", paving, new Vector3(0, -1.72f, 0), new Vector3(30, 0.06f, 21));
            P(PrimitiveType.Cube, "Courtyard Edge", stoneDark, new Vector3(0, -1.74f, 0), new Vector3(31, 0.05f, 22));

            // Stepped plinth under the board's tray.
            P(PrimitiveType.Cube, "Plinth Top", stone, new Vector3(0, -0.8f, 0), new Vector3(15.6f, 0.6f, 9.6f));
            P(PrimitiveType.Cube, "Plinth Base", stoneDark, new Vector3(0, -1.4f, 0), new Vector3(18f, 0.6f, 12f));

            // A split gate and low walls behind each player's side (the board's long axis is X).
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 15.5f;
                foreach (float z in new[] { -1.7f, 1.7f }) GateHalf(new Vector3(x, -1.7f, z), z > 0 ? 1f : -1f, stone, stoneDark);
                foreach (float z in new[] { -6.2f, 6.2f })
                    P(PrimitiveType.Cube, "Wall", stoneDark, new Vector3(x, -1.2f, z), new Vector3(0.9f, 1.0f, 7.5f));
                P(PrimitiveType.Cube, "Wall Cap", stone, new Vector3(x, -0.66f, -6.2f), new Vector3(1.1f, 0.12f, 7.7f));
                P(PrimitiveType.Cube, "Wall Cap", stone, new Vector3(x, -0.66f, 6.2f), new Vector3(1.1f, 0.12f, 7.7f));
            }

            // Trees in a loose ring, kept off the gate lines.
            for (int i = 0; i < 18; i++)
            {
                float a = i / 18f * Mathf.PI * 2f + R(-0.12f, 0.12f);
                if (Mathf.Abs(Mathf.Cos(a)) > 0.93f) continue; // leave the gates clear
                float r = R(13.5f, 24f);
                Tree(new Vector3(Mathf.Cos(a) * r * 1.25f, -1.75f, Mathf.Sin(a) * r), R(0.85f, 1.35f), trunk, rng.NextDouble() < 0.5 ? leaf : leafLight);
            }

            // Rocks and bushes around the courtyard.
            for (int i = 0; i < 16; i++)
            {
                float a = R(0, Mathf.PI * 2f), r = R(12f, 20f);
                var pos = new Vector3(Mathf.Cos(a) * r * 1.3f, -1.7f, Mathf.Sin(a) * r);
                if (i % 2 == 0) P(PrimitiveType.Sphere, "Rock", stoneDark, pos, new Vector3(R(0.8f, 1.6f), R(0.4f, 0.8f), R(0.8f, 1.4f)));
                else P(PrimitiveType.Sphere, "Bush", leaf, pos + Vector3.up * 0.2f, Vector3.one * R(1.0f, 1.8f));
            }

            // Hills on the horizon.
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f + R(-0.2f, 0.2f), r = R(62f, 80f);
                P(PrimitiveType.Sphere, "Hill", hills, new Vector3(Mathf.Cos(a) * r, -7f, Mathf.Sin(a) * r), new Vector3(R(28f, 44f), R(10f, 18f), R(28f, 44f)));
            }
        }

        /// <summary>One half of a candi bentar: a stepped stone tower with a flat inner face toward the gap.</summary>
        void GateHalf(Vector3 basePos, float outward, Material stone, Material dark)
        {
            float y = basePos.y;
            var tiers = new[] { (2.4f, 1.0f), (2.0f, 1.3f), (1.6f, 1.1f), (1.2f, 0.9f), (0.8f, 0.7f), (0.4f, 0.6f) };
            for (int i = 0; i < tiers.Length; i++)
            {
                var (w, h) = tiers[i];
                // Each tier keeps its inner edge on the gap, so the halves read as one split tower.
                float zOffset = outward * (2.4f - w) * 0.5f;
                P(PrimitiveType.Cube, "Gate Tier", i % 2 == 0 ? stone : dark, new Vector3(basePos.x, y + h * 0.5f, basePos.z + zOffset), new Vector3(w, h, w));
                y += h;
            }
        }

        void Tree(Vector3 at, float scale, Material trunk, Material leaves)
        {
            P(PrimitiveType.Cylinder, "Trunk", trunk, at + Vector3.up * 1.1f * scale, new Vector3(0.35f, 1.1f, 0.35f) * scale);
            P(PrimitiveType.Sphere, "Canopy", leaves, at + new Vector3(0, 2.8f, 0) * scale, new Vector3(3.0f, 2.2f, 3.0f) * scale);
            P(PrimitiveType.Sphere, "Canopy", leaves, at + new Vector3(0.8f, 2.3f, 0.5f) * scale, new Vector3(2.0f, 1.6f, 2.0f) * scale);
            P(PrimitiveType.Sphere, "Canopy", leaves, at + new Vector3(-0.7f, 2.4f, -0.6f) * scale, new Vector3(2.1f, 1.7f, 2.1f) * scale);
        }

        Transform P(PrimitiveType type, string name, Material mat, Vector3 pos, Vector3 scale) =>
            Mats.Prim(type, transform, name, mat, pos, scale);
    }
}
