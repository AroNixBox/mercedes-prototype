using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CarCam
{
    /// <summary>Car-relative reference frame: origin = car bounds center projected to floor height, forward = car forward (horizontal).</summary>
    public readonly struct CarFrame
    {
        public readonly Transform Car;
        public readonly Bounds Bounds;
        public readonly Vector3 Origin;
        public readonly Vector3 Forward;
        public readonly Vector3 Right;
        /// <summary>x = width, y = height, z = length (measured in the car's own axes).</summary>
        public readonly Vector3 Size;

        public float FloorY => Origin.y;

        CarFrame(Transform car, Bounds bounds, Vector3 forward, Vector3 size)
        {
            Car = car;
            Bounds = bounds;
            Origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Forward = forward;
            Right = Vector3.Cross(Vector3.up, forward);
            Size = size;
        }

        public static bool TryCreate(Transform car, out CarFrame frame)
        {
            frame = default;
            if (car == null)
            {
                return false;
            }
            var renderers = car.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return false;
            }

            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }
            var forward = Vector3.ProjectOnPlane(car.forward, Vector3.up);
            forward = forward.sqrMagnitude < 1e-6f ? Vector3.forward : forward.normalized;
            frame = new CarFrame(car, bounds, forward, MeasureSize(renderers, forward));
            return true;
        }

        static Vector3 MeasureSize(Renderer[] renderers, Vector3 forward)
        {
            var right = Vector3.Cross(Vector3.up, forward);
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            foreach (var r in renderers)
            {
                var lb = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var sign = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                    var world = r.transform.TransformPoint(lb.center + Vector3.Scale(lb.extents, sign));
                    var p = new Vector3(Vector3.Dot(world, right), world.y, Vector3.Dot(world, forward));
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }
            return max - min;
        }

        public Vector3 ToWorld(CamPoint p)
        {
            var direction = Quaternion.AngleAxis(p.azimuthDeg, Vector3.up) * Forward;
            return Origin + direction * p.distance + Vector3.up * p.height;
        }

        public CamPoint ToCamPoint(Vector3 world)
        {
            var flat = Vector3.ProjectOnPlane(world - Origin, Vector3.up);
            return new CamPoint(Vector3.SignedAngle(Forward, flat, Vector3.up), flat.magnitude, world.y - Origin.y);
        }

        public Transform FindPart(string partName)
        {
            var car = Car;
            return car.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t != car && t.name == partName);
        }

        /// <summary>Descendants (not the car root) that have their own Renderer.</summary>
        public IEnumerable<string> PartNames()
        {
            var car = Car;
            return car.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.transform != car)
                .Select(r => r.transform.name)
                .Distinct();
        }

        public static Bounds PartBounds(Transform part)
        {
            var renderers = part.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(part.position, Vector3.zero);
            }
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }
    }
}
