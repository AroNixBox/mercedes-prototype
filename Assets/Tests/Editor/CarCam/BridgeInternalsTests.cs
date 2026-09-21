using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Timeline;

namespace CarCam.Tests
{
    public class BridgeInternalsTests
    {
        const string TestTimeline = "CarCamTimeline_BridgeTest";

        [SetUp]
        public void SetUp() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset($"Assets/{TestTimeline}.playable");

        [Test]
        public void GetOrCreateTimelineAsset_ReturnsSameAssetTwice()
        {
            var a = TimelineAiBridge.GetOrCreateTimelineAsset(TestTimeline);
            var b = TimelineAiBridge.GetOrCreateTimelineAsset(TestTimeline);
            Assert.IsNotNull(a);
            Assert.AreSame(a, b);
            Assert.AreEqual($"Assets/{TestTimeline}.playable", AssetDatabase.GetAssetPath(a));
        }

        [Test]
        public void EnsureBrain_CreatesTaggedMainCameraOnlyOnce()
        {
            var b1 = AiCameraDirectorBridge.EnsureBrain();
            var b2 = AiCameraDirectorBridge.EnsureBrain();
            Assert.AreSame(b1, b2);
            Assert.AreEqual("MainCamera", b1.gameObject.tag);
            Assert.AreEqual(1, Object.FindObjectsByType<Camera>().Length);
        }

        [Test]
        public void BuildSpline_UsesWorldPositions_AndReplacesKnots()
        {
            var host = new GameObject("Host");
            host.transform.position = new Vector3(10, 0, 0);
            var container = AiCameraDirectorBridge.BuildSpline(host, new[] { new Vector3(0, 1, 0), new Vector3(0, 1, 5) });
            Assert.AreEqual(2, container.Spline.Count);
            Assert.That(Vector3.Distance(new Vector3(0, 1, 5), container.EvaluatePosition(1f)), Is.LessThan(1e-3f));

            AiCameraDirectorBridge.BuildSpline(host, new[] { Vector3.zero, Vector3.one, new Vector3(2, 0, 2) });
            Assert.AreEqual(3, container.Spline.Count);
        }

        [Test]
        public void AddAnimationClip_And_SetClipCurve_WriteSplinePositionCurve()
        {
            var timeline = TimelineAiBridge.GetOrCreateTimelineAsset(TestTimeline);
            var track = timeline.CreateTrack<AnimationTrack>(null, "Anim");
            var clip = TimelineAiBridge.AddAnimationClip(timeline, track, "Move", 2, 4);
            TimelineAiBridge.SetClipCurve(timeline, clip.animationClip, typeof(CinemachineSplineDolly),
                "m_SplineSettings.Position", EasingCurves.Create(Easing.Linear, 4));

            Assert.AreEqual(2, clip.start, 1e-6);
            Assert.AreEqual(4, clip.duration, 1e-6);
            Assert.AreEqual("Move", clip.displayName);
            var binding = EditorCurveBinding.FloatCurve("", typeof(CinemachineSplineDolly), "m_SplineSettings.Position");
            var curve = AnimationUtility.GetEditorCurve(clip.animationClip, binding);
            Assert.IsNotNull(curve);
            Assert.AreEqual(0.5f, curve.Evaluate(2f), 1e-3f);
        }

        [TestCase(Easing.Linear, 0.5f)]
        [TestCase(Easing.EaseIn, 0.25f)]
        [TestCase(Easing.EaseOut, 0.75f)]
        [TestCase(Easing.EaseInOut, 0.5f)]
        public void EasingCurves_HitExpectedMidpoint(Easing easing, float expectedMid)
        {
            var curve = EasingCurves.Create(easing, 6f);
            Assert.AreEqual(0f, curve.Evaluate(0f), 1e-4f);
            Assert.AreEqual(1f, curve.Evaluate(6f), 1e-4f);
            Assert.AreEqual(expectedMid, curve.Evaluate(3f), 1e-3f);
        }

        [Test]
        public void EasingCurves_EaseInOut_IsSlowAtEnds()
        {
            var curve = EasingCurves.Create(Easing.EaseInOut, 6f);
            var linear = EasingCurves.Create(Easing.Linear, 6f);
            Assert.Less(curve.Evaluate(0.6f), linear.Evaluate(0.6f));
            Assert.Greater(curve.Evaluate(5.4f), linear.Evaluate(5.4f));
        }

        [Test]
        public void Protocol_SuccessAndValidationFailed_Format()
        {
            StringAssert.StartsWith("[[PROMPTRETURN]] SUCCESS\n", BridgeProtocol.Success("details"));
            var retry = BridgeProtocol.ValidationFailed("t 0.10–0.20: KINK", 1, 3);
            StringAssert.StartsWith("[[PROMPTRETURN]] VALIDATION_FAILED (attempt 1/3)", retry);
            StringAssert.Contains("You MAY fix this yourself", retry);
            var stop = BridgeProtocol.ValidationFailed("t 0.10–0.20: KINK", 3, 3);
            StringAssert.Contains("Stop all tool calls", stop);
        }
    }
}
