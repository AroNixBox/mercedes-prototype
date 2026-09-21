using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.AI.Assistant.FunctionCalling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using static System.FormattableString;

namespace CarCam
{
    /// <summary>AI Assistant tools of the car showroom spline workflow (proof of concept: spline only, no camera).
    /// Stateless: every BuildPath call carries the full path description; the scene only receives a plain SplineContainer.</summary>
    public static class CarCamTools
    {
        public const string DefaultCarName = "BlockoutCar";
        public const string PathObjectPrefix = "CarCamPath_";

        [AgentTool("Step 1 of the car showroom spline workflow. Returns the car's size, the floor height and all named car parts with their " +
                   "car-relative position (azimuthDeg / distance / height), so you can translate the user's move description into CamPoints. " +
                   "Call this before CarCam.BuildPath.", "CarCam.GetCarContext")]
        public static string GetCarContext(
            [ToolParameter("Name of the car GameObject in the open scene.")] string carName = DefaultCarName)
            => Run("CarCam GetCarContext", () =>
            {
                if (!TryGetCarFrame(carName, out var frame, out var error))
                {
                    return BridgeProtocol.Failure(error);
                }

                var sb = new StringBuilder();
                sb.AppendLine(Invariant($"Car '{carName}': length {frame.Size.z:0.00} m, width {frame.Size.x:0.00} m, height {frame.Size.y:0.00} m. Floor at world y {frame.FloorY:0.00}."));
                sb.AppendLine("CamPoint: azimuthDeg 0 = front, 90 = right side, 180 = rear, -90 = left side; distance = horizontal meters from the car center; height = meters above the floor.");
                sb.AppendLine("Parts (car-relative positions, useful to place points near a part):");
                foreach (var partName in frame.PartNames())
                {
                    var p = frame.ToCamPoint(CarFrame.PartBounds(frame.FindPart(partName)).center);
                    sb.AppendLine(Invariant($"- {partName}: az {p.azimuthDeg:0.0}°, d {p.distance:0.00} m, h {p.height:0.00} m"));
                }
                var missing = CarColliderScope.CountMissing(frame.Car);
                sb.AppendLine(missing == 0
                    ? "Colliders: all mesh parts have colliders."
                    : $"Colliders: {missing} mesh parts have no collider; they are added temporarily during path validation.");
                sb.Append("Next: call CarCam.BuildPath.");
                return BridgeProtocol.Success(sb.ToString());
            });

        [AgentTool("Step 2 of the car showroom spline workflow. Builds (or replaces) the spline 'CarCamPath_<shotName>' from start → intermediate points → end, " +
                   "all in car-relative CamPoints, and validates it from t = 0 to 1 (clipping through geometry, floor clearance, distance to the car, " +
                   "no kinks or back-and-forth). Returns SUCCESS or VALIDATION_FAILED with the problematic t ranges and the nearest point to fix. " +
                   "Call it again with the same shotName to adjust the path.", "CarCam.BuildPath")]
        public static string BuildPath(
            [ToolParameter("Unique path name, e.g. 'WalkUp'. Reusing a name replaces that path.")] string shotName,
            [ToolParameter("Start position: azimuthDeg (0 front, 90 right, 180 rear, -90 left), distance (meters from the car center, > 0), height (meters above the floor).")] CamPoint start,
            [ToolParameter("End position, same format as start.")] CamPoint end,
            [ToolParameter("Ordered intermediate CamPoints between start and end (may be empty or omitted). They shape the move, e.g. a slight rise and fall.")] List<CamPoint> intermediatePoints = null,
            [ToolParameter("Name of the car GameObject in the open scene.")] string carName = DefaultCarName)
            => Run("CarCam BuildPath", () =>
            {
                if (string.IsNullOrWhiteSpace(shotName)) return BridgeProtocol.Failure("shotName must not be empty.");
                intermediatePoints ??= new List<CamPoint>();
                if (start.distance <= 0f || end.distance <= 0f || intermediatePoints.Any(p => p.distance <= 0f))
                {
                    return BridgeProtocol.Failure("Every CamPoint needs distance > 0 (meters from the car center).");
                }
                if (!TryGetCarFrame(carName, out var frame, out var error)) return BridgeProtocol.Failure(error);

                var world = new List<Vector3> { frame.ToWorld(start) };
                world.AddRange(intermediatePoints.Select(p => frame.ToWorld(p)));
                world.Add(frame.ToWorld(end));

                var container = GetOrCreatePath(shotName, world);

                PathReport report;
                using (new CarColliderScope(frame.Car))
                {
                    report = PathValidator.Validate(new PathInput
                    {
                        Spline = container,
                        Car = frame.Car,
                        FloorY = frame.FloorY
                    }, PathValidationSettings.Default);
                }

                var summary = Invariant($"Path '{container.gameObject.name}': start {start} → {intermediatePoints.Count} intermediate point(s) → end {end}.\n") + report.ToText();
                return report.IsValid
                    ? BridgeProtocol.Success(summary + "\nThe spline is valid and visible in the scene. Tell the user which object to inspect.")
                    : BridgeProtocol.ValidationFailed(summary);
            });

        /// <summary>Finds or creates the root GameObject "CarCamPath_&lt;shotName&gt;" (plain SplineContainer only) and replaces its knots.</summary>
        internal static SplineContainer GetOrCreatePath(string shotName, IReadOnlyList<Vector3> worldPositions)
        {
            var pathName = PathObjectPrefix + shotName;
            var container = UnityEngine.Object.FindObjectsByType<SplineContainer>().FirstOrDefault(c => c.gameObject.name == pathName);
            GameObject host;
            if (container != null)
            {
                host = container.gameObject;
                Undo.RecordObject(container, "CarCam BuildPath");
            }
            else
            {
                host = new GameObject(pathName);
                Undo.RegisterCreatedObjectUndo(host, "Create CarCam Path");
            }
            container = AiCameraDirectorBridge.BuildSpline(host, worldPositions);
            EditorSceneManager.MarkSceneDirty(host.scene);
            return container;
        }

        static bool TryGetCarFrame(string carName, out CarFrame frame, out string error)
        {
            frame = default;
            error = null;
            var car = GameObject.Find(carName);
            if (car == null)
            {
                error = $"No active GameObject named '{carName}' in the open scene.";
                return false;
            }
            if (!CarFrame.TryCreate(car.transform, out frame))
            {
                error = $"'{carName}' has no Renderers, so the car cannot be measured.";
                return false;
            }
            return true;
        }

        static string Run(string undoName, Func<string> body)
        {
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            try
            {
                return body();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return BridgeProtocol.Failure($"{undoName} threw {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }
        }
    }
}
