using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Splines;

namespace CarCam.Tests
{
    public class PathValidatorTests
    {
        Transform m_Car;

        [SetUp]
        public void SetUp()
        {
            CarCamTestScene.NewEmptyScene();
            m_Car = CarCamTestScene.CreateCar();
        }

        PathInput Input(SplineContainer spline) => new PathInput { Spline = spline, Car = m_Car, FloorY = 0f };

        static SplineContainer WalkUp(float midHeight = 1.5f) => CarCamTestScene.Spline(
            new Vector3(0f, 1.6f, 9f), new Vector3(0f, midHeight, 6.5f), new Vector3(0f, 1.4f, 4f));

        static PathReport Validate(PathInput input) => PathValidator.Validate(input, PathValidationSettings.Default);

        [Test]
        public void FreeWalkUpPath_IsValid()
        {
            var report = Validate(Input(WalkUp()));
            Assert.IsTrue(report.IsValid, report.ToText());
            Assert.GreaterOrEqual(report.SampleCount, 50);
            Assert.AreEqual(5f, report.Length, 0.2f);
            Assert.AreEqual(1.4f, report.MinHeightAboveFloor, 0.1f);
        }

        [Test]
        public void Obstacle_OnPath_ReportsClippingAroundMiddle()
        {
            CarCamTestScene.CreatePillar(new Vector3(0f, 1.5f, 6.5f));
            var report = Validate(Input(WalkUp()));
            var clip = report.Problems.FirstOrDefault(p => p.Issue == PathIssue.Clipping);
            Assert.IsNotNull(clip, report.ToText());
            StringAssert.Contains("Pillar", clip.Detail);
            Assert.Less(clip.TStart, 0.5f);
            Assert.Greater(clip.TEnd, 0.5f);
            Assert.AreEqual("intermediate #0", clip.NearestPoint);
            StringAssert.Contains("CLIPPING with Pillar", report.ToText());
        }

        [Test]
        public void Dip_BelowFloorClearance_ReportsBelowMinHeight()
        {
            var report = Validate(Input(WalkUp(midHeight: 0.1f)));
            Assert.IsTrue(report.Problems.Any(p => p.Issue == PathIssue.BelowMinHeight), report.ToText());
        }

        [Test]
        public void EndTooCloseToCar_ReportsTooCloseToCar_NotClipping()
        {
            var spline = CarCamTestScene.Spline(
                new Vector3(0f, 1.6f, 9f), new Vector3(0f, 1.3f, 5.5f), new Vector3(0f, 1.0f, 2.4f));
            var report = Validate(Input(spline));
            Assert.IsTrue(report.Problems.Any(p => p.Issue == PathIssue.TooCloseToCar), report.ToText());
            Assert.IsFalse(report.Problems.Any(p => p.Issue == PathIssue.Clipping), report.ToText());
        }

        [Test]
        public void BackAndForth_ReportsKink()
        {
            var spline = CarCamTestScene.Spline(
                new Vector3(0f, 1.5f, 9f), new Vector3(0f, 1.5f, 5f), new Vector3(0f, 1.5f, 8f));
            var report = Validate(Input(spline));
            Assert.IsTrue(report.Problems.Any(p => p.Issue == PathIssue.Kink), report.ToText());
        }

        [Test]
        public void CarColliderScope_AddsAndRemovesTemporaryColliders()
        {
            CarCamTestScene.NewEmptyScene();
            var car = CarCamTestScene.CreateCar(withColliders: false);
            Assert.AreEqual(4, CarColliderScope.CountMissing(car));
            using (var scope = new CarColliderScope(car))
            {
                Assert.AreEqual(4, scope.AddedCount);
                Assert.AreEqual(4, car.GetComponentsInChildren<MeshCollider>().Length);
            }
            Assert.AreEqual(0, car.GetComponentsInChildren<MeshCollider>().Length);
        }

        [Test]
        public void CollidersFromScope_AreSeenByValidator_TooCloseToCar()
        {
            CarCamTestScene.NewEmptyScene();
            var car = CarCamTestScene.CreateCar(withColliders: false);

            // a non-box mesh part (capsule), also without a collider, so the regression covers a non-convex-by-default shape
            var bumper = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bumper.name = "Bumper";
            Object.DestroyImmediate(bumper.GetComponent<Collider>());
            bumper.transform.SetParent(car, false);
            bumper.transform.localPosition = new Vector3(0f, 0.5f, 2.4f);
            bumper.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

            m_Car = car;
            var spline = CarCamTestScene.Spline(
                new Vector3(0f, 1.6f, 9f), new Vector3(0f, 1.3f, 5.5f), new Vector3(0f, 0.5f, 2.65f));

            // sanity check (no scope): must NOT report TooCloseToCar without added colliders
            var reportNoScope = Validate(Input(spline));
            Assert.IsFalse(reportNoScope.Problems.Any(p => p.Issue == PathIssue.TooCloseToCar), reportNoScope.ToText());

            using (new CarColliderScope(car))
            {
                var report = Validate(Input(spline));
                Assert.IsTrue(report.Problems.Any(p => p.Issue == PathIssue.TooCloseToCar), report.ToText());
            }
        }
    }
}
