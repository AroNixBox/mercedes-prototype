using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Splines;
using static System.FormattableString;

namespace CarCam
{
    public struct PathValidationSettings
    {
        public float SampleSpacing;
        public int MinSamples;
        public float CameraRadius;
        public float MinFloorClearance;
        public float MinCarDistance;
        public float MaxKinkDeg;

        public static PathValidationSettings Default => new()
        {
            SampleSpacing = 0.1f,
            MinSamples = 50,
            CameraRadius = 0.15f,
            MinFloorClearance = 0.2f,
            MinCarDistance = 0.25f,
            MaxKinkDeg = 30f
        };
    }

    public enum PathIssue
    {
        Clipping,
        BelowMinHeight,
        TooCloseToCar,
        Kink
    }

    public struct PathInput
    {
        public SplineContainer Spline;
        public Transform Car;
        public float FloorY;
    }

    public class PathProblem
    {
        public PathIssue Issue;
        public float TStart;
        public float TEnd;
        public string Detail;
        public string NearestPoint;

        public string ToText() => Invariant($"t {TStart:0.00}–{TEnd:0.00}: {IssueCode(Issue)} {Detail} – nearest point: {NearestPoint}");

        public static string IssueCode(PathIssue issue) => issue switch
        {
            PathIssue.Clipping => "CLIPPING",
            PathIssue.BelowMinHeight => "BELOW_MIN_HEIGHT",
            PathIssue.TooCloseToCar => "TOO_CLOSE_TO_CAR",
            PathIssue.Kink => "KINK",
            _ => issue.ToString()
        };
    }

    public class PathReport
    {
        public float Length;
        public float MinHeightAboveFloor;
        public int SampleCount;
        public List<PathProblem> Problems = new();

        public bool IsValid => Problems.Count == 0;

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Invariant($"Path length {Length:0.00} m, {SampleCount} samples, min height above floor {MinHeightAboveFloor:0.00} m."));
            foreach (var problem in Problems)
            {
                sb.AppendLine(problem.ToText());
            }
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>Samples the spline from t = 0 to 1 and checks every position (the spline alone — no camera or look direction).</summary>
    public static class PathValidator
    {
        public static PathReport Validate(PathInput input, PathValidationSettings settings)
        {
            Physics.SyncTransforms();
            var container = input.Spline;
            var length = container.CalculateLength();
            var count = Mathf.Max(settings.MinSamples, Mathf.CeilToInt(length / settings.SampleSpacing) + 1);

            var ts = new float[count];
            var perSample = new List<(PathIssue issue, string detail)>[count];
            var minHeight = float.MaxValue;
            var prevPos = Vector3.zero;
            Vector3? lastTangent = null;

            for (int i = 0; i < count; i++)
            {
                var t = i / (float)(count - 1);
                ts[i] = t;
                var issues = perSample[i] = new List<(PathIssue, string)>();
                Vector3 pos = container.EvaluatePosition(t);

                // floor
                var height = pos.y - input.FloorY;
                minHeight = Mathf.Min(minHeight, height);
                if (height < settings.MinFloorClearance)
                {
                    issues.Add((PathIssue.BelowMinHeight, Invariant($"height {height:0.00} m < {settings.MinFloorClearance:0.00} m")));
                }

                // clipping with non-car geometry (overlap + sweep from the previous sample)
                var blocker = FirstNonCar(Physics.OverlapSphere(pos, settings.CameraRadius, ~0, QueryTriggerInteraction.Ignore), input.Car);
                if (blocker == null && i > 0)
                {
                    var delta = pos - prevPos;
                    if (delta.sqrMagnitude > 1e-8f)
                    {
                        var hits = Physics.SphereCastAll(prevPos, settings.CameraRadius, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
                        blocker = FirstNonCar(hits.Select(h => h.collider), input.Car);
                    }
                }
                if (blocker != null)
                {
                    issues.Add((PathIssue.Clipping, $"with {blocker.name}"));
                }

                // distance to the car
                var carHit = Physics.OverlapSphere(pos, settings.MinCarDistance, ~0, QueryTriggerInteraction.Ignore)
                    .FirstOrDefault(c => IsCar(c, input.Car));
                if (carHit != null)
                {
                    issues.Add((PathIssue.TooCloseToCar, Invariant($"{carHit.name} closer than {settings.MinCarDistance:0.00} m")));
                }

                // kinks / reversals
                Vector3 tangent = container.EvaluateTangent(t);
                if (tangent.sqrMagnitude > 1e-8f)
                {
                    if (lastTangent.HasValue)
                    {
                        var turn = Vector3.Angle(lastTangent.Value, tangent);
                        if (turn > settings.MaxKinkDeg)
                        {
                            issues.Add((PathIssue.Kink, Invariant($"direction change {turn:0}°")));
                        }
                    }
                    lastTangent = tangent;
                }

                prevPos = pos;
            }

            return new PathReport
            {
                Length = length,
                MinHeightAboveFloor = minHeight,
                SampleCount = count,
                Problems = GroupIntoRanges(container, ts, perSample)
            };
        }

        static List<PathProblem> GroupIntoRanges(SplineContainer container, float[] ts, List<(PathIssue issue, string detail)>[] perSample)
        {
            var problems = new List<PathProblem>();
            var count = ts.Length;
            foreach (PathIssue issue in Enum.GetValues(typeof(PathIssue)))
            {
                int runStart = -1;
                string detail = null;
                for (int i = 0; i <= count; i++)
                {
                    var has = i < count && perSample[i].Any(x => x.issue == issue);
                    if (has && runStart < 0)
                    {
                        runStart = i;
                        detail = perSample[i].First(x => x.issue == issue).detail;
                    }
                    else if (!has && runStart >= 0)
                    {
                        var tStart = ts[runStart];
                        var tEnd = ts[i - 1];
                        problems.Add(new PathProblem
                        {
                            Issue = issue,
                            TStart = tStart,
                            TEnd = tEnd,
                            Detail = detail,
                            NearestPoint = NearestPointLabel(container, (tStart + tEnd) * 0.5f)
                        });
                        runStart = -1;
                    }
                }
            }
            return problems.OrderBy(p => p.TStart).ToList();
        }

        static string NearestPointLabel(SplineContainer container, float t)
        {
            var spline = container.Spline;
            int best = 0;
            var bestDistance = float.MaxValue;
            for (int k = 0; k < spline.Count; k++)
            {
                var knotT = spline.ConvertIndexUnit(k, PathIndexUnit.Knot, PathIndexUnit.Normalized);
                var distance = Mathf.Abs(knotT - t);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = k;
                }
            }
            if (best == 0) return "start";
            if (best == spline.Count - 1) return "end";
            return $"intermediate #{best - 1}";
        }

        static bool IsCar(Collider collider, Transform car) => car != null && collider.transform.IsChildOf(car);

        static Collider FirstNonCar(IEnumerable<Collider> colliders, Transform car) =>
            colliders.FirstOrDefault(c => c != null && !IsCar(c, car));
    }
}
