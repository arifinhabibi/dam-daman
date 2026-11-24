using System;
using UnityEngine;

namespace Pamor
{
    /// <summary>Every feel number in one place, so it can be tuned live from the Inspector.</summary>
    [Serializable]
    public class FeelTunables
    {
        [Header("Selection")]
        public float selectLiftScale = 1.12f;
        public float selectDuration = 0.12f;

        [Header("Movement")]
        public float hopDuration = 0.2f;
        public float hopArcScale = 1.15f;
        public float landSquashY = 0.85f;
        public float landSquashDuration = 0.1f;

        [Header("Capture")]
        public float capturePopScale = 1.25f;
        public float captureDuration = 0.22f;
        public int captureParticles = 10;

        [Header("Promotion")]
        public float crownDuration = 0.35f;

        [Header("Hints")]
        public float pulsePeriod = 1.1f;
        public float shakeAmplitude = 0.08f;
        public float shakeDuration = 0.25f;
    }
}
