using UnityEngine;

namespace CarCam
{
    public enum Easing
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut
    }

    public static class EasingCurves
    {
        /// <summary>Curve from (0,0) to (duration,1). Hermite tangents: EaseIn = t², EaseOut = 2t − t², EaseInOut = smoothstep.</summary>
        public static AnimationCurve Create(Easing easing, float duration)
        {
            // tangents in normalized units (value per normalized time), scaled to value per second
            var (outStart, inEnd) = easing switch
            {
                Easing.EaseIn => (0f, 2f),
                Easing.EaseOut => (2f, 0f),
                Easing.EaseInOut => (0f, 0f),
                _ => (1f, 1f)
            };
            var slope = 1f / duration;
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, outStart * slope),
                new Keyframe(duration, 1f, inEnd * slope, 0f));
        }
    }
}
