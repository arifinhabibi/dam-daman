using System.Collections.Generic;
using Pamor.Core;
using UnityEngine;

namespace Pamor
{
    /// <summary>
    /// Placeholder wayang-kulit figures for the six roles: flat profile silhouettes (facing +X) built from
    /// outlines, with gold (prada) ornaments, cloth headwear, a role prop and the horn stick (gapit).
    /// Original shapes in the general wayang style; real character art replaces them later.
    /// </summary>
    public static class WayangFigures
    {
        public enum Mat { Body, Gold, Cloth, Horn }

        public readonly struct Part
        {
            public readonly Vector2[] Outline;
            public readonly Mat Mat;
            /// <summary>Depth: negative is toward the viewer, positive behind the body.</summary>
            public readonly float Z;
            public Part(Vector2[] outline, Mat mat, float z) { Outline = outline; Mat = mat; Z = z; }
        }

        static Vector2[] V(params float[] xy)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return pts;
        }

        // ---------- shared body parts

        static readonly Vector2[] Robe = V(-0.22f, 0f, 0.24f, 0f, 0.21f, 0.07f, 0.15f, 0.28f, 0.10f, 0.46f, 0.13f, 0.60f, 0.09f, 0.71f,
                                           0.04f, 0.74f, -0.06f, 0.74f, -0.10f, 0.66f, -0.11f, 0.50f, -0.16f, 0.30f, -0.25f, 0.07f);
        static readonly Vector2[] Head = V(-0.07f, 0.72f, 0.05f, 0.72f, 0.08f, 0.77f, 0.16f, 0.81f, 0.08f, 0.85f, 0.06f, 0.90f, -0.07f, 0.92f, -0.11f, 0.84f);
        static readonly Vector2[] ArmFront = V(0.05f, 0.68f, 0.10f, 0.70f, 0.17f, 0.56f, 0.25f, 0.47f, 0.23f, 0.43f, 0.13f, 0.54f);
        static readonly Vector2[] ArmBack = V(-0.05f, 0.68f, -0.01f, 0.69f, -0.07f, 0.52f, -0.13f, 0.40f, -0.17f, 0.42f, -0.10f, 0.55f);
        static readonly Vector2[] Sash = WayangMesh.Rect(-0.115f, 0.455f, 0.115f, 0.50f);
        static readonly Vector2[] Hem = V(-0.24f, 0.02f, 0.235f, 0.02f, 0.225f, 0.06f, -0.245f, 0.06f);
        static readonly Vector2[] Gapit = WayangMesh.Rect(-0.012f, -0.05f, 0.012f, 0.70f);

        // ---------- role parts

        static readonly Vector2[] PrajuritCap = V(-0.08f, 0.88f, 0.06f, 0.89f, 0.01f, 0.96f, -0.08f, 0.95f);
        static readonly Vector2[] PrajuritShield = WayangMesh.Circle(new Vector2(0.22f, 0.50f), 0.085f, 10);

        static readonly Vector2[] PendekarUdeng = V(-0.11f, 0.86f, 0.04f, 0.90f, -0.02f, 0.97f, -0.12f, 0.95f);
        static readonly Vector2[] PendekarKeris = V(0.22f, 0.44f, 0.25f, 0.46f, 0.29f, 0.56f, 0.27f, 0.62f, 0.32f, 0.71f, 0.30f, 0.76f, 0.26f, 0.68f, 0.24f, 0.61f, 0.20f, 0.49f);

        static readonly Vector2[] SenopatiTopong = V(-0.09f, 0.88f, 0.07f, 0.88f, 0.06f, 0.97f, 0f, 1.03f, -0.06f, 1.01f, -0.10f, 0.95f);
        static readonly Vector2[] SpearShaft = WayangMesh.Rect(0.235f, 0.02f, 0.255f, 1.05f);
        static readonly Vector2[] SpearTip = V(0.225f, 1.04f, 0.265f, 1.04f, 0.245f, 1.17f);

        static readonly Vector2[] DukunBlangkon = V(-0.10f, 0.86f, 0.06f, 0.87f, 0.05f, 0.94f, -0.02f, 0.97f, -0.11f, 0.94f);
        static readonly Vector2[] StaffShaft = WayangMesh.Rect(0.235f, 0f, 0.255f, 0.95f);
        static readonly Vector2[] StaffHead = WayangMesh.Circle(new Vector2(0.245f, 0.99f), 0.045f, 10);

        static readonly Vector2[] TelikHood = V(-0.13f, 0.72f, 0.10f, 0.74f, 0.13f, 0.80f, 0.09f, 0.94f, -0.05f, 1.00f, -0.15f, 0.90f);
        static readonly Vector2[] TelikMask = WayangMesh.Rect(0.0f, 0.80f, 0.12f, 0.83f);

        static readonly Vector2[] PatihMakutha = V(-0.08f, 0.89f, 0.06f, 0.89f, 0.05f, 1.00f, 0.02f, 1.08f, 0.03f, 1.16f, -0.01f, 1.24f, -0.05f, 1.15f, -0.04f, 1.04f, -0.09f, 0.98f);
        static readonly Vector2[] PatihSumping = V(-0.06f, 0.86f, -0.02f, 0.84f, -0.03f, 0.78f, -0.07f, 0.80f);
        static readonly Vector2[] PatihTrain = V(-0.16f, 0.30f, -0.12f, 0.20f, -0.20f, 0.02f, -0.36f, 0.02f, -0.30f, 0.14f);

        static readonly Dictionary<Role, Part[]> figures = new Dictionary<Role, Part[]>
        {
            { Role.Prajurit, Build(Common(), new Part(PrajuritCap, Mat.Gold, -0.03f), new Part(PrajuritShield, Mat.Gold, -0.07f)) },
            { Role.Pendekar, Build(Common(), new Part(PendekarUdeng, Mat.Cloth, -0.03f), new Part(PendekarKeris, Mat.Gold, -0.07f)) },
            { Role.Senopati, Build(Common(), new Part(SenopatiTopong, Mat.Gold, -0.03f), new Part(SpearShaft, Mat.Horn, -0.065f), new Part(SpearTip, Mat.Gold, -0.065f)) },
            { Role.Dukun, Build(Common(), new Part(DukunBlangkon, Mat.Cloth, -0.03f), new Part(StaffShaft, Mat.Horn, -0.065f), new Part(StaffHead, Mat.Gold, -0.065f)) },
            { Role.TelikSandi, Build(Common(), new Part(TelikHood, Mat.Cloth, -0.03f), new Part(TelikMask, Mat.Gold, -0.05f)) },
            { Role.Patih, Build(Common(), new Part(PatihTrain, Mat.Body, 0.02f), new Part(PatihMakutha, Mat.Gold, -0.03f), new Part(PatihSumping, Mat.Gold, -0.04f)) },
        };

        static List<Part> Common() => new List<Part>
        {
            new Part(ArmBack, Mat.Body, 0.035f),
            new Part(Robe, Mat.Body, 0f),
            new Part(Head, Mat.Body, 0f),
            new Part(Hem, Mat.Gold, -0.025f),
            new Part(Sash, Mat.Gold, -0.025f),
            new Part(Gapit, Mat.Horn, -0.022f),
            new Part(ArmFront, Mat.Body, -0.04f),
        };

        static Part[] Build(List<Part> common, params Part[] extra)
        {
            common.AddRange(extra);
            return common.ToArray();
        }

        public static Part[] For(Role role) => figures[role];

        /// <summary>The Patih stands a little larger than everyone else.</summary>
        public static float Scale(Role role) => role == Role.Patih ? 1.12f : 1f;
    }
}
