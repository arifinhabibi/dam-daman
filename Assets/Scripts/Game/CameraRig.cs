using Unity.Cinemachine;
using UnityEngine;

namespace Pamor
{
    /// <summary>
    /// Player-controlled orbit around the board, driving a Cinemachine Orbital Follow.
    /// Yaw spins freely all the way round; the tilt is clamped so the camera always stays above the board:
    /// at most <see cref="maxTiltFromTop"/> degrees away from looking straight down.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] CinemachineOrbitalFollow orbit;
        [Tooltip("Furthest the camera may tilt away from straight down, in degrees.")]
        public float maxTiltFromTop = 40f;
        [Tooltip("Starting tilt from straight down, in degrees.")]
        public float startTiltFromTop = 28f;
        [Tooltip("Starting yaw: 90 puts the camera behind Hitam, so the player's own pieces are at the bottom of the screen.")]
        public float startYaw = 90f;
        [Tooltip("Orbit distance from the board centre at zoom 1. Frames the whole board on a ~2:1 phone screen.")]
        public float radius = 21.5f;
        public float degreesPerPixel = 0.25f;
        public Vector2 zoomRange = new Vector2(0.75f, 1.35f);

        public float Yaw => orbit.HorizontalAxis.Value;
        public float TiltFromTop => 90f - orbit.VerticalAxis.Value;

        void Awake()
        {
            // Vertical axis = elevation above the board plane. 90 = straight down; 50 = 40 degrees of tilt.
            orbit.VerticalAxis.Range = new Vector2(90f - maxTiltFromTop, 89.5f);
            orbit.VerticalAxis.Wrap = false;
            orbit.HorizontalAxis.Range = new Vector2(-180f, 180f);
            orbit.HorizontalAxis.Wrap = true;
            orbit.RadialAxis.Range = zoomRange;
            orbit.Radius = radius;
            ResetView();
        }

        public void ResetView()
        {
            orbit.HorizontalAxis.Value = startYaw;
            orbit.VerticalAxis.Value = 90f - startTiltFromTop;
            orbit.RadialAxis.Value = 1f;
        }

        /// <summary>Drag in screen pixels: horizontal spins around the board, vertical tilts.</summary>
        public void Rotate(Vector2 pixels)
        {
            var h = orbit.HorizontalAxis;
            h.Value = h.ClampValue(h.Value + pixels.x * degreesPerPixel);
            orbit.HorizontalAxis = h;
            var v = orbit.VerticalAxis;
            v.Value = v.ClampValue(v.Value - pixels.y * degreesPerPixel);
            orbit.VerticalAxis = v;
        }

        /// <summary>Positive zooms in.</summary>
        public void Zoom(float amount)
        {
            var r = orbit.RadialAxis;
            r.Value = r.ClampValue(r.Value - amount);
            orbit.RadialAxis = r;
        }
    }
}
