using System;

namespace CarCam
{
    /// <summary>Camera position relative to the car: azimuth around the car (0 = front, 90 = right, 180 = rear, -90 = left),
    /// horizontal distance from the car center in meters, height above the floor in meters.</summary>
    [Serializable]
    public struct CamPoint
    {
        public float azimuthDeg;
        public float distance;
        public float height;

        public CamPoint(float azimuthDeg, float distance, float height)
        {
            this.azimuthDeg = azimuthDeg;
            this.distance = distance;
            this.height = height;
        }

        public override string ToString() =>
            FormattableString.Invariant($"(az {azimuthDeg:0.#}°, d {distance:0.##} m, h {height:0.##} m)");
    }
}
