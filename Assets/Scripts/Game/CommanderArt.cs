using System;
using Pamor.Core;
using UnityEngine;

namespace Pamor
{
    /// <summary>
    /// Where each Commander's 3D model lives. Drop a rigged, animated model (see docs/ART_PIPELINE.md)
    /// into its slot in Assets/Resources/CommanderArt.asset; empty slots show a placeholder figure.
    /// </summary>
    [CreateAssetMenu(menuName = "Pamor/Commander Art", fileName = "CommanderArt")]
    public class CommanderArt : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public Commander commander;
            [Tooltip("The model prefab (FBX or a prefab made from it) with an Animator. Leave empty for the placeholder.")]
            public GameObject model;
            [Tooltip("Uniform scale applied to the model so it stands about 1.6 board units tall.")]
            public float scale = 1f;
            [Tooltip("Extra rotation if the model doesn't face +Z.")]
            public Vector3 rotationOffset;
        }

        public Entry[] entries = Array.Empty<Entry>();

        public Entry For(Commander c)
        {
            foreach (var e in entries) if (e != null && e.commander == c) return e;
            return null;
        }

        static CommanderArt cached;
        public static CommanderArt Load() => cached != null ? cached : cached = Resources.Load<CommanderArt>("CommanderArt");

        /// <summary>Animator trigger names every Commander model is expected to have (Idle is the default state).</summary>
        public const string Cast = "Cast", Hit = "Hit", Victory = "Victory", Defeat = "Defeat";
    }
}
