using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CarCam.Tests
{
    public class CarFrameTests
    {
        Transform m_Car;

        [SetUp]
        public void SetUp()
        {
            CarCamTestScene.NewEmptyScene();
            m_Car = CarCamTestScene.CreateCar();
        }

        static void AssertVec(Vector3 expected, Vector3 actual) =>
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-3f), $"expected {expected} but was {actual}");

        CarFrame Frame()
        {
            Assert.IsTrue(CarFrame.TryCreate(m_Car, out var frame));
            return frame;
        }

        [Test]
        public void TryCreate_FailsWithoutRenderers()
        {
            Assert.IsFalse(CarFrame.TryCreate(new GameObject("Empty").transform, out _));
            Assert.IsFalse(CarFrame.TryCreate(null, out _));
        }

        [Test]
        public void Measures_Size_Origin_Center()
        {
            var frame = Frame();
            AssertVec(new Vector3(2f, 1.4f, 4.5f), frame.Size);
            AssertVec(Vector3.zero, frame.Origin);
            AssertVec(CarCamTestScene.CarCenter, frame.Bounds.center);
            Assert.AreEqual(0f, frame.FloorY, 1e-4f);
        }

        [TestCase(0f, 0f, 1f, 5f)]
        [TestCase(90f, 5f, 1f, 0f)]
        [TestCase(180f, 0f, 1f, -5f)]
        [TestCase(-90f, -5f, 1f, 0f)]
        public void ToWorld_UsesCarForwardAndRight(float az, float x, float y, float z)
        {
            AssertVec(new Vector3(x, y, z), Frame().ToWorld(new CamPoint(az, 5f, 1f)));
        }

        [Test]
        public void ToWorld_FollowsCarRotation()
        {
            m_Car.rotation = Quaternion.Euler(0f, 90f, 0f); // front now +X, right side now -Z
            var frame = Frame();
            AssertVec(new Vector3(5f, 1f, 0f), frame.ToWorld(new CamPoint(0f, 5f, 1f)));
            AssertVec(new Vector3(0f, 1f, -5f), frame.ToWorld(new CamPoint(90f, 5f, 1f)));
            AssertVec(new Vector3(2f, 1.4f, 4.5f), frame.Size);
        }

        [Test]
        public void ToCamPoint_RoundTrips()
        {
            var frame = Frame();
            var p = frame.ToCamPoint(frame.ToWorld(new CamPoint(135f, 4f, 2f)));
            Assert.AreEqual(135f, p.azimuthDeg, 1e-2f);
            Assert.AreEqual(4f, p.distance, 1e-3f);
            Assert.AreEqual(2f, p.height, 1e-3f);
        }

        [Test]
        public void PartNames_ListsRenderedChildrenOnly()
        {
            var names = Frame().PartNames().ToList();
            CollectionAssert.AreEquivalent(new[] { "Body", "Headlight_L", "Wheel_FR", "Wheel_RR" }, names);
        }
    }
}
