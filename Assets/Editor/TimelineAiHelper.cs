using UnityEngine;

namespace Editor
{
    // TODO: Add 
    public static class TimelineAiHelper
    {
        /// <returns>between the target and the origin is a clear line of sight</returns>
        public static bool HasLineOfSightToTarget(Vector3 origin, Vector3 target)
        {
            var toTarget = origin - target;
            var distance = toTarget.magnitude;
            // TODO: Convert to Spherecast, but objects that are in start/endsphere wont be captured
            // TODO: Maybe pass target and origin "object" to exclude them from the ray
            return !Physics.Raycast(origin, toTarget.normalized, distance);
        }
    }
}