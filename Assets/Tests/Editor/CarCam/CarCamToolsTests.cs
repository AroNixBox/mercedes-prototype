using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.AI.Assistant.FunctionCalling;
using UnityEngine;
using UnityEngine.Splines;

namespace CarCam.Tests
{
    public class CarCamToolsTests
    {
        const string Car = CarCamTestScene.CarName;
        const string Success = "[[PROMPTRETURN]] SUCCESS";
        const string Failure = "[[PROMPTRETURN]] FAILURE";
        const string ValidationFailed = "[[PROMPTRETURN]] VALIDATION_FAILED";

        static readonly CamPoint Start = new(0f, 9f, 1.6f);
        static readonly CamPoint End = new(0f, 4f, 1.4f);

        [SetUp]
        public void SetUp()
        {
            CarCamTestScene.NewEmptyScene();
            CarCamTestScene.CreateCar();
        }

        static List<CamPoint> Mid(float height = 1.5f) => new() { new CamPoint(0f, 6.5f, height) };

        static SplineContainer[] Paths(string shotName) =>
            Object.FindObjectsByType<SplineContainer>().Where(c => c.gameObject.name == CarCamTools.PathObjectPrefix + shotName).ToArray();

        [Test]
        public void Tools_AreRegisteredWithExpectedIds_AndAllParametersDescribed()
        {
            var tools = typeof(CarCamTools).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<AgentToolAttribute>() != null)
                .ToList();
            CollectionAssert.AreEquivalent(new[] { "CarCam.GetCarContext", "CarCam.BuildPath" },
                tools.Select(m => m.GetCustomAttribute<AgentToolAttribute>().Id));
            foreach (var tool in tools)
            {
                Assert.IsTrue(tool.GetParameters().All(p => p.GetCustomAttribute<ToolParameterAttribute>() != null),
                    $"{tool.Name}: every parameter needs [ToolParameter], otherwise the Assistant silently drops the tool");
            }
        }

        [Test]
        public void GetCarContext_ListsPartsAndSize()
        {
            var result = CarCamTools.GetCarContext(Car);
            StringAssert.StartsWith(Success, result);
            StringAssert.Contains("length 4.50 m", result);
            StringAssert.Contains("Headlight_L", result);
            StringAssert.Contains("CarCam.BuildPath", result);
        }

        [Test]
        public void GetCarContext_UnknownCar_Fails()
        {
            StringAssert.StartsWith(Failure, CarCamTools.GetCarContext("NoSuchCar"));
        }

        [Test]
        public void BuildPath_InvalidParameters_Fail()
        {
            StringAssert.StartsWith(Failure, CarCamTools.BuildPath("", Start, End, carName: Car));
            StringAssert.StartsWith(Failure, CarCamTools.BuildPath("A", new CamPoint(0f, 0f, 1f), End, carName: Car));
            StringAssert.StartsWith(Failure, CarCamTools.BuildPath("A", Start, End, new List<CamPoint> { new(0f, -1f, 1f) }, Car));
            StringAssert.StartsWith(Failure, CarCamTools.BuildPath("A", Start, End, carName: "NoSuchCar"));
        }

        [Test]
        public void BuildPath_ValidWalkUp_CreatesPlainSplineObject()
        {
            var result = CarCamTools.BuildPath("WalkUp", Start, End, Mid(), Car);
            StringAssert.StartsWith(Success, result);
            StringAssert.Contains("CarCamPath_WalkUp", result);

            var path = Paths("WalkUp").Single();
            Assert.AreEqual(3, path.Spline.Count);
            Assert.That(Vector3.Distance(new Vector3(0f, 1.6f, 9f), path.EvaluatePosition(0f)), Is.LessThan(1e-3f));
            Assert.That(Vector3.Distance(new Vector3(0f, 1.4f, 4f), path.EvaluatePosition(1f)), Is.LessThan(1e-3f));

            // proof of concept: only built-in components in the scene, no custom scripts, no camera
            var components = path.GetComponents<Component>().Select(c => c.GetType()).ToArray();
            CollectionAssert.AreEquivalent(new[] { typeof(Transform), typeof(SplineContainer) }, components);
        }

        [Test]
        public void BuildPath_NullIntermediatePoints_BuildsTwoKnotSpline()
        {
            StringAssert.StartsWith(Success, CarCamTools.BuildPath("Straight", Start, End, null, Car));
            Assert.AreEqual(2, Paths("Straight").Single().Spline.Count);
        }

        [Test]
        public void BuildPath_SameName_ReplacesKnotsInsteadOfDuplicating()
        {
            CarCamTools.BuildPath("A", Start, End, Mid(), Car);
            StringAssert.StartsWith(Success, CarCamTools.BuildPath("A", Start, End, null, Car));
            var paths = Paths("A");
            Assert.AreEqual(1, paths.Length);
            Assert.AreEqual(2, paths[0].Spline.Count);
        }

        [Test]
        public void BuildPath_Obstacle_ReturnsValidationFailed_ButKeepsSplineVisible()
        {
            CarCamTestScene.CreatePillar(new Vector3(0f, 1.5f, 6.5f));
            var result = CarCamTools.BuildPath("A", Start, End, Mid(), Car);
            StringAssert.StartsWith(ValidationFailed, result);
            StringAssert.Contains("CLIPPING with Pillar", result);
            StringAssert.Contains("You MAY fix it yourself", result);
            Assert.AreEqual(1, Paths("A").Length);
        }
    }
}
