# Car Showroom Shot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ein Unity-AI-Assistant-Skill mit vier `[AgentTool]`s, der aus einem User-Prompt genau einen Assetto-Corsa-artigen Kamera-Shot (Spline-Dolly + Timeline) um das Auto baut – standardisiert und ohne Aufruf-Fehler.

**Architecture:** Der Agent beschreibt Punkte nur auto-relativ (`CamPoint`). Vier Tools bilden die Pipeline ab (Kontext → Endpunkte → Pfad + Validierung → Finalisieren); der Zustand liegt in einer Szenen-Komponente `CarCamShot`. Die Blickrichtung wird per `FixedLookBlend`-Extension zwischen Start- und End-Rotation (max. 10°) interpoliert; die bestehenden Bridges dienen als interne Bausteine.

**Tech Stack:** Unity 6000.6.0f1, C# 9, `com.unity.ai.assistant` 2.9.0-pre.2 (`[AgentTool]`, Skills), Cinemachine 6.6.0 (`Unity.Cinemachine`), Splines 2.9.0, Timeline 6.6.0, Unity Test Framework (EditMode, NUnit).

**Spec:** `docs/superpowers/specs/2026-09-18-car-showroom-shot-design.md`

## Global Constraints

- **NICHT committen.** Der User will keine Commits (weder Code noch Plan/Spec). Es gibt keine Commit-Schritte; Dateien bleiben uncommitted. Fremde uncommittete Änderungen im Working Tree nicht anfassen.
- Tool-IDs exakt: `CarCam.GetCarContext`, `CarCam.SetEndpoints`, `CarCam.BuildPath`, `CarCam.FinalizeShot`.
- Namespace für allen neuen Code: `CarCam` (Tests: `CarCam.Tests`).
- Max. Blickdrehung: `10°` (`CarFrame.MaxLookRotationDeg`).
- Validierungs-Defaults: Sample-Abstand `0.1 m`, min. `50` Samples, Kamera-Radius `0.15 m`, Bodenabstand `0.2 m`, Auto-Abstand `0.25 m`, Knick `30°`.
- Default-FOV `35`, Default-Easing `EaseInOut`, Default-Auto `BlockoutCar`, Default-Timeline `CarCamTimeline`, max. automatische Reparaturversuche `3`.
- Zahlen in Tool-Rückgaben immer kulturinvariant formatieren (`FormattableString.Invariant`) – der User-Rechner kann deutsche Locale haben.
- Alle Tool-Rückgaben beginnen mit `[[PROMPTRETURN]] SUCCESS`, `... FAILURE`, `... AWAITING_INPUT` oder `... VALIDATION_FAILED`.

### Tests ausführen

- **Editor offen:** `Window > General > Test Runner > EditMode`, Filter `CarCam`, *Run Selected*. Vorher offene Szene speichern (Tests öffnen leere Szenen).
- **Editor geschlossen (CLI):**
  ```bash
  "/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics \
    -projectPath "/Users/aroboss/Unity/Repositories/mercedes-prototype" \
    -runTests -testPlatform EditMode -testFilter "CarCam" \
    -testResults "$TMPDIR/carcam-results.xml" -logFile -
  ```
  Ergebnis in `$TMPDIR/carcam-results.xml` (`result="Passed"`). Meldet Unity „another Unity instance is running“, den User bitten, die Tests im Test Runner auszuführen und das Ergebnis zu nennen.
- **Nur Kompilieren prüfen (Editor geschlossen):** gleicher Aufruf ohne `-runTests …`, stattdessen `-quit`, dann im Log nach `error CS` suchen.

---

## File Structure

| Datei | Verantwortung |
|---|---|
| `Assets/CarCam/Runtime/CarCam.Runtime.asmdef` | Runtime-Assembly |
| `Assets/CarCam/Runtime/CamPoint.cs` | Auto-relativer Punkt (Tool-Parameter + Serialisierung) |
| `Assets/CarCam/Runtime/LookTarget.cs` | Blickziel: Teilname/`Center` + Höhen-Offset |
| `Assets/CarCam/Runtime/CarCamShot.cs` | Session-Komponente + `ShotState` |
| `Assets/CarCam/Runtime/FixedLookBlend.cs` | Cinemachine-Extension: Rotation = Slerp(start, end, Dolly-Fortschritt) |
| `Assets/Editor/MercedesPrototype.Editor.asmdef` | Editor-Assembly (bestehende Bridges + CarCam-Editor) |
| `Assets/Editor/AssemblyInfo.cs` | `InternalsVisibleTo("CarCam.Tests")` |
| `Assets/Editor/BridgeProtocol.cs` (mod) | + `Success(details)`, `ValidationFailed(...)` |
| `Assets/Editor/AiCameraDirectorBridge.cs` (mod) | + `EnsureBrain()`, `BuildSpline()` intern |
| `Assets/Editor/TimelineAiBridge.cs` (mod) | + `GetOrCreateTimelineAsset`, `CreateDirector`, `CreateTrack`, `AddShotClip`, `AddAnimationClip`, `SetClipCurve` intern |
| `Assets/Editor/TimelineAiHelper.cs` (mod) | ungenutztes `using Unity.Collections` entfernen |
| `Assets/Editor/CarCam/EasingCurves.cs` | `Easing`-Enum + Kurven-Erzeugung |
| `Assets/Editor/CarCam/CarFrame.cs` | Auto-Bezugssystem, Umrechnung, Blick-Klemmung |
| `Assets/Editor/CarCam/PathValidator.cs` | Sampling + Checks + Report-Typen |
| `Assets/Editor/CarCam/CarColliderScope.cs` | Temporäre MeshCollider während der Validierung |
| `Assets/Editor/CarCam/ShotBuilder.cs` | Spline-Neubau, Kamera + Timeline |
| `Assets/Editor/CarCam/CarCamSessions.cs` | Sessions finden/anlegen, Auto finden |
| `Assets/Editor/CarCam/CarCamTools.cs` | Die vier `[AgentTool]`-Einstiegspunkte |
| `Assets/AI_Instructions/Skills/car-showroom-shot/SKILL.md` | Skill-Anweisungen |
| `Assets/AI_Instructions/Skills/car-showroom-shot/references/translation-guide.md` | Prompt → CamPoints |
| `Assets/Tests/Editor/CarCam/CarCam.Tests.asmdef` + `*Tests.cs`, `CarCamTestScene.cs` | EditMode-Tests |

---

### Task 1: Assemblies aufsetzen und Bridges asmdef-tauglich machen

**Files:**
- Create: `Assets/CarCam/Runtime/CarCam.Runtime.asmdef`
- Create: `Assets/Editor/MercedesPrototype.Editor.asmdef`
- Create: `Assets/Editor/AssemblyInfo.cs`
- Create: `Assets/Tests/Editor/CarCam/CarCam.Tests.asmdef`
- Create: `Assets/Tests/Editor/CarCam/AssemblySetupTests.cs`
- Modify: `Assets/Editor/AiCameraDirectorBridge.cs`, `Assets/Editor/TimelineAiBridge.cs`, `Assets/Editor/TimelineAiHelper.cs`

**Interfaces:**
- Produces: Assemblies `CarCam.Runtime`, `MercedesPrototype.Editor`, `CarCam.Tests`. Tests sehen `internal` Member von `MercedesPrototype.Editor`.

**Hintergrund:** Tests in einer asmdef können `Assembly-CSharp-Editor` nicht referenzieren, daher bekommt `Assets/Editor` eine eigene asmdef. Die Bridges nutzen aktuell `Unity.VisualScripting`s `Component.AddComponent<T>()`-Extension – die wird durch `gameObject.AddComponent<T>()` ersetzt, damit keine VisualScripting-Referenz nötig ist.

- [ ] **Step 1: Test-asmdef und fehlschlagenden Test schreiben**

`Assets/Tests/Editor/CarCam/CarCam.Tests.asmdef`:
```json
{
    "name": "CarCam.Tests",
    "rootNamespace": "CarCam.Tests",
    "references": [
        "MercedesPrototype.Editor",
        "CarCam.Runtime",
        "Unity.Cinemachine",
        "Unity.Splines",
        "Unity.Mathematics",
        "Unity.Timeline",
        "Unity.AI.Assistant.Runtime",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [ "Editor" ],
    "overrideReferences": true,
    "precompiledReferences": [ "nunit.framework.dll" ],
    "autoReferenced": false,
    "defineConstraints": [ "UNITY_INCLUDE_TESTS" ]
}
```

`Assets/Tests/Editor/CarCam/AssemblySetupTests.cs`:
```csharp
using NUnit.Framework;

namespace CarCam.Tests
{
    public class AssemblySetupTests
    {
        [Test]
        public void Bridges_LiveInEditorAssembly()
        {
            Assert.AreEqual("MercedesPrototype.Editor", typeof(TimelineAiBridge).Assembly.GetName().Name);
            Assert.AreEqual("MercedesPrototype.Editor", typeof(AiCameraDirectorBridge).Assembly.GetName().Name);
            Assert.AreEqual("[[PROMPTRETURN]] SUCCESS", BridgeProtocol.SUCCESS);
        }
    }
}
```

- [ ] **Step 2: Kompilieren – muss fehlschlagen**

Erwartet: Compile-Fehler bzw. Warnung, dass Assembly `MercedesPrototype.Editor` nicht gefunden wird (Test nicht lauffähig).

- [ ] **Step 3: asmdefs + InternalsVisibleTo anlegen**

`Assets/CarCam/Runtime/CarCam.Runtime.asmdef`:
```json
{
    "name": "CarCam.Runtime",
    "rootNamespace": "CarCam",
    "references": [ "Unity.Cinemachine", "Unity.Splines", "Unity.Mathematics" ],
    "autoReferenced": true
}
```

`Assets/Editor/MercedesPrototype.Editor.asmdef`:
```json
{
    "name": "MercedesPrototype.Editor",
    "references": [
        "CarCam.Runtime",
        "Unity.Cinemachine",
        "Unity.Splines",
        "Unity.Mathematics",
        "Unity.Timeline",
        "Unity.AI.Assistant.Runtime"
    ],
    "includePlatforms": [ "Editor" ],
    "autoReferenced": true
}
```

`Assets/Editor/AssemblyInfo.cs`:
```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CarCam.Tests")]
```

Der Ordner `Assets/CarCam/Runtime/` ist bis Task 3 leer bis auf die asmdef – Unity akzeptiert das.

- [ ] **Step 4: VisualScripting-Abhängigkeit entfernen**

In `Assets/Editor/AiCameraDirectorBridge.cs`:
- Zeile `using Unity.VisualScripting;` löschen.
- `brain = mainCam.AddComponent<CinemachineBrain>();` → `brain = mainCam.gameObject.AddComponent<CinemachineBrain>();`
- `var rotationComposer = vCamTr.AddComponent<CinemachineRotationComposer>();` → `var rotationComposer = vCamTr.gameObject.AddComponent<CinemachineRotationComposer>();`

In `Assets/Editor/TimelineAiBridge.cs`:
- Zeile `using Unity.VisualScripting;` löschen.
- `clipSource = audioSourceHolder.AddComponent<AudioSource>();` → `clipSource = audioSourceHolder.gameObject.AddComponent<AudioSource>();`

In `Assets/Editor/TimelineAiHelper.cs`:
- Zeile `using Unity.Collections;` löschen.

Danach mit `grep -n "AddComponent" Assets/Editor/*.cs` prüfen: jeder Aufruf erfolgt auf einem `GameObject` (`...gameObject.AddComponent`, `vCamGo.AddComponent`, `splineGo.AddComponent`, `directorGo.AddComponent`).

- [ ] **Step 5: Kompilieren und Test ausführen**

Erwartet: keine `error CS`; `AssemblySetupTests.Bridges_LiveInEditorAssembly` PASS.
Falls ein weiterer Compile-Fehler wegen fehlender Assembly-Referenz auftaucht (z. B. `CS0012 … defined in an assembly that is not referenced`), die genannte Assembly in `MercedesPrototype.Editor.asmdef` ergänzen.

---

### Task 2: Bridge-Bausteine, Protokoll-Erweiterung, Easing

**Files:**
- Modify: `Assets/Editor/BridgeProtocol.cs`
- Modify: `Assets/Editor/AiCameraDirectorBridge.cs` (`CreateVCam`, `CreateSpline`)
- Modify: `Assets/Editor/TimelineAiBridge.cs` (`CreateTimelineAsset`-Umfeld, `CreateTrack`, `CreatePlayableDirector`, `AddAnimationToAnimationTrack`, `SetAnimationTrackCurve`, `SetCinemachineTrack`)
- Create: `Assets/Editor/CarCam/EasingCurves.cs`
- Test: `Assets/Tests/Editor/CarCam/BridgeInternalsTests.cs`

**Interfaces:**
- Produces:
  - `BridgeProtocol.Success(string details) : string`
  - `BridgeProtocol.ValidationFailed(string report, int attempt, int maxAttempts) : string`
  - `internal static CinemachineBrain AiCameraDirectorBridge.EnsureBrain()`
  - `internal static SplineContainer AiCameraDirectorBridge.BuildSpline(GameObject host, IReadOnlyList<Vector3> worldPositions)`
  - `internal static TimelineAsset TimelineAiBridge.GetOrCreateTimelineAsset(string name)` (Pfad `Assets/{name}.playable`)
  - `internal static PlayableDirector TimelineAiBridge.CreateDirector(string directorName, TimelineAsset timeline)` (GameObject-Name `{directorName}_Director`)
  - `internal static T TimelineAiBridge.CreateTrack<T>(TimelineAsset timeline, PlayableDirector director, string name, Object binding) where T : TrackAsset, new()`
  - `internal static TimelineClip TimelineAiBridge.AddShotClip(TimelineAsset timeline, CinemachineTrack track, PlayableDirector director, CinemachineCamera cam, double start, double duration)`
  - `internal static TimelineClip TimelineAiBridge.AddAnimationClip(TimelineAsset timeline, AnimationTrack track, string displayName, double start, double duration)`
  - `internal static void TimelineAiBridge.SetClipCurve(TimelineAsset timeline, AnimationClip clip, System.Type componentType, string propertyName, AnimationCurve curve)`
  - `public enum CarCam.Easing { Linear, EaseIn, EaseOut, EaseInOut }`
  - `public static AnimationCurve CarCam.EasingCurves.Create(Easing easing, float duration)` – Wert 0 bei t=0, 1 bei t=duration.

- [ ] **Step 1: Fehlschlagende Tests schreiben**

`Assets/Tests/Editor/CarCam/BridgeInternalsTests.cs`:
```csharp
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
```

- [ ] **Step 2: Tests laufen lassen – müssen fehlschlagen**

Erwartet: Compile-Fehler (`EnsureBrain`, `BuildSpline`, `GetOrCreateTimelineAsset`, `EasingCurves`, `Success` … nicht definiert).

- [ ] **Step 3: `BridgeProtocol` erweitern**

Folgende Methoden in `public static class BridgeProtocol` ergänzen (Bestehendes bleibt):
```csharp
    public static string Success(string details) => $"{SUCCESS}\n{details}";

    public static string ValidationFailed(string report, int attempt, int maxAttempts)
    {
        var header = $"[[PROMPTRETURN]] VALIDATION_FAILED (attempt {attempt}/{maxAttempts}):\n{report}\n";
        if (attempt < maxAttempts)
        {
            return header +
                   "🟠 AGENT: You MAY fix this yourself. Move, add or remove intermediate points near the reported t ranges " +
                   "(t 0 = start, 1 = end; 'nearest point' names the point to change) and call CarCam.BuildPath again. " +
                   "Do NOT change the endpoints without asking the user.";
        }
        return header +
               "🔵 AGENT: Stop all tool calls. The maximum number of automatic fix attempts is reached. " +
               "Show the report to the user and propose 2-3 numbered options (e.g. adjust intermediate points, change endpoints, change the look target). " +
               "Do NOT execute any option yourself. Wait for the user to pick.";
    }
```

- [ ] **Step 4: `AiCameraDirectorBridge` – `EnsureBrain` und `BuildSpline` extrahieren**

`using UnityEditor;` ist vorhanden. `CreateVCam` so ändern, dass der Brain-Teil über `EnsureBrain` läuft:
```csharp
    static string CreateVCam(string name, Transform target, out string vCamGlobalId, out string brainGlobalId)
    {
        var brain = EnsureBrain();
        brainGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(brain).ToString();

        var vCamGo = new GameObject { name = name };
        var vCam = vCamGo.AddComponent<CinemachineCamera>();
        vCam.Target.TrackingTarget = target;
        AddRotationTarget(vCam.transform);
        vCamGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(vCam).ToString();

        EditorUtility.SetDirty(vCam);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(vCamGo.scene);

        return BridgeProtocol.SUCCESS;
    }

    /// <summary>Returns the CinemachineBrain on the main camera; creates a tagged Main Camera and/or brain if missing.</summary>
    internal static CinemachineBrain EnsureBrain()
    {
        var mainCam = Camera.main;
        if (mainCam == null)
        {
            var mainCamObj = new GameObject { name = "Main Camera", tag = "MainCamera" };
            Undo.RegisterCreatedObjectUndo(mainCamObj, "Create Main Camera");
            mainCam = mainCamObj.AddComponent<Camera>();
        }

        if (!mainCam.TryGetComponent<CinemachineBrain>(out var brain))
        {
            brain = Undo.AddComponent<CinemachineBrain>(mainCam.gameObject);
        }
        return brain;
    }
```

`CreateSpline` ersetzen und `BuildSpline` ergänzen:
```csharp
    static string CreateSpline(string name, List<Vector3> positionsInOrder, out string splineContainerGlobalId)
    {
        splineContainerGlobalId = string.Empty;
        if (positionsInOrder is not { Count: >= 1 })
        {
            return BridgeProtocol.Failure("You tried creating a spline without knots. Provide at least 1 position.");
        }

        var splineGo = new GameObject { name = name };
        var splineContainer = BuildSpline(splineGo, positionsInOrder);
        splineContainerGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(splineContainer).ToString();
        return BridgeProtocol.SUCCESS;
    }

    /// <summary>Adds (or reuses) a SplineContainer on host and replaces its knots with the given world positions (AutoSmooth tangents).</summary>
    internal static SplineContainer BuildSpline(GameObject host, IReadOnlyList<Vector3> worldPositions)
    {
        if (!host.TryGetComponent<SplineContainer>(out var splineContainer))
        {
            splineContainer = host.AddComponent<SplineContainer>();
        }

        var spline = splineContainer.Spline;
        spline.Clear();
        foreach (var worldPosition in worldPositions)
        {
            spline.Add(new BezierKnot(host.transform.InverseTransformPoint(worldPosition)));
        }

        // smooth tangents
        for (int i = 0; i < spline.Count; i++)
        {
            spline.SetTangentMode(i, TangentMode.AutoSmooth);
        }
        EditorUtility.SetDirty(splineContainer);
        return splineContainer;
    }
```

- [ ] **Step 5: `TimelineAiBridge` – Bausteine extrahieren**

Oben ergänzen: `using UnityEditor.SceneManagement;` (die bestehenden voll qualifizierten `UnityEditor.SceneManagement.EditorSceneManager`-Aufrufe dürfen bleiben).

Nach `GetTimelineAsset(...)` einfügen:
```csharp
    internal static TimelineAsset GetOrCreateTimelineAsset(string name)
    {
        if (GetTimelineAsset(name, out var timeline, out var fullPath))
        {
            return timeline;
        }
        CreateTimelineAsset(fullPath);
        GetTimelineAsset(name, out timeline, out _);
        return timeline;
    }
```

`CreateTrack<T>` ersetzen (jetzt `internal`, gibt Track zurück):
```csharp
    internal static T CreateTrack<T>(TimelineAsset timeline, PlayableDirector director, string newTrackName, [CanBeNull] UnityEngine.Object trackReference) where T : TrackAsset, new()
    {
        var track = timeline.CreateTrack<T>(null, newTrackName);

        // the created track needs a scene reference -> thats what we set here:
        if (trackReference != null)
        {
            director.SetGenericBinding(track, trackReference);
        }
        // object change in scene only needs to be marked dirty, can be saved manually from user.
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        // asset needs to be marked dirty and saved
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
        return track;
    }
```
(`TryCreateTrack<T>` ruft `CreateTrack<T>(...)` weiter unverändert auf; der Rückgabewert wird dort ignoriert.)

`CreatePlayableDirector` ersetzen und `CreateDirector` ergänzen:
```csharp
    public static string CreatePlayableDirector(string directorName, string timelineName, out string directorGlobalId)
    {
        directorGlobalId = string.Empty;
        if (!GetTimelineAsset(timelineName, out var timeline, out _))
        {
            return BridgeProtocol.Failure("No Timeline-Asset found under this path: " + timelineName);
        }

        var director = CreateDirector(directorName, timeline);
        directorGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(director).ToString();
        return BridgeProtocol.SUCCESS;
    }

    internal static PlayableDirector CreateDirector(string directorName, TimelineAsset timeline)
    {
        var directorGo = new GameObject(directorName + "_Director");
        Undo.RegisterCreatedObjectUndo(directorGo, "Create Director");
        var director = directorGo.AddComponent<PlayableDirector>();
        director.playableAsset = timeline;
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        return director;
    }
```

In `AddAnimationToAnimationTrack` den Block ab `AnimationClip clip = new AnimationClip();` bis `return BridgeProtocol.SUCCESS;` ersetzen durch:
```csharp
        AddAnimationClip(timeline, animationTrack, animationName, startTime, duration);
        animationTimelineClipName = animationName; // set to the same name that was put in to remind the agent: hey this is what you passed in
        return BridgeProtocol.SUCCESS;
```
und darunter ergänzen:
```csharp
    internal static TimelineClip AddAnimationClip(TimelineAsset timeline, AnimationTrack track, string displayName, double start, double duration)
    {
        var clip = new AnimationClip { name = displayName };
        AssetDatabase.AddObjectToAsset(clip, timeline);
        var timelineClip = track.CreateClip(clip);
        timelineClip.start = start;
        timelineClip.duration = duration;
        timelineClip.displayName = displayName;

        EditorUtility.SetDirty(clip);
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
        return timelineClip;
    }

    internal static void SetClipCurve(TimelineAsset timeline, AnimationClip clip, System.Type componentType, string propertyName, AnimationCurve curve)
    {
        // "" means no parent
        clip.SetCurve("", componentType, propertyName, curve);
        EditorUtility.SetDirty(clip);
        EditorUtility.SetDirty(timeline); // because animclip is subasset of timeline
        AssetDatabase.SaveAssets();
    }
```

In `SetAnimationTrackCurve<T>` die letzten Zeilen ab `animClip.SetCurve("", typeof(T), propertyName, curve);` bis vor `return BridgeProtocol.SUCCESS;` ersetzen durch:
```csharp
        SetClipCurve(timeline, animClip, typeof(T), propertyName, curve);
```

In `SetCinemachineTrack` den Block ab `var shotClip = cmTrack.CreateClip<CinemachineShot>();` bis vor `return BridgeProtocol.SUCCESS;` ersetzen durch:
```csharp
        AddShotClip(timeline, cmTrack, director, assignedCamera, startTime, duration);
```
und ergänzen:
```csharp
    internal static TimelineClip AddShotClip(TimelineAsset timeline, CinemachineTrack track, PlayableDirector director, CinemachineCamera cam, double start, double duration)
    {
        var shotClip = track.CreateClip<CinemachineShot>();
        shotClip.start = start;
        shotClip.duration = duration;

        var shot = (CinemachineShot)shotClip.asset;
        shot.VirtualCamera.exposedName = new PropertyName(GUID.Generate().ToString());
        director.SetReferenceValue(shot.VirtualCamera.exposedName, cam);

        EditorUtility.SetDirty(timeline);
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        AssetDatabase.SaveAssets();
        return shotClip;
    }
```

- [ ] **Step 6: `EasingCurves` anlegen**

`Assets/Editor/CarCam/EasingCurves.cs`:
```csharp
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
```

- [ ] **Step 7: Tests laufen lassen – müssen passen**

Erwartet: alle `BridgeInternalsTests` + `AssemblySetupTests` PASS.

---

### Task 3: Runtime-Typen und `FixedLookBlend`

**Files:**
- Create: `Assets/CarCam/Runtime/CamPoint.cs`, `LookTarget.cs`, `CarCamShot.cs`, `FixedLookBlend.cs`
- Test: `Assets/Tests/Editor/CarCam/RuntimeTypesTests.cs`

**Interfaces:**
- Produces:
  - `[Serializable] public struct CamPoint { public float azimuthDeg, distance, height; CamPoint(float,float,float); ToString() }`
  - `[Serializable] public class LookTarget { public const string Center = "Center"; public string part = Center; public float heightOffset; LookTarget(); LookTarget(string part, float heightOffset = 0f) }`
  - `public enum ShotState { EndpointsSet, PathValid, Finalized }`
  - `public class CarCamShot : MonoBehaviour` mit Feldern `car, start, end, hasStartLookTarget, startLookTarget, endLookTarget, fov, startRotation, endRotation, startLookPoint, endLookPoint, intermediatePoints, state, validationAttempts, lastReport, spline, vcam`, Konstante `ObjectPrefix = "CarCamShot_"`, Property `ShotName`.
  - `public class FixedLookBlend : CinemachineExtension` mit `StartRotation, EndRotation, Dolly` und `public static Quaternion Evaluate(Quaternion start, Quaternion end, float progress)`.

**Hinweis:** Die Felder von `CamPoint`/`LookTarget` sind öffentliche Felder in camelCase – der AI-Assistant erzeugt das JSON-Schema aus öffentlichen Feldern, die Namen sind also genau die, die der Agent sieht. Unity serialisiert `LookTarget`-Felder nie als `null`, deshalb gibt es `hasStartLookTarget`.

- [ ] **Step 1: Fehlschlagende Tests schreiben**

`Assets/Tests/Editor/CarCam/RuntimeTypesTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace CarCam.Tests
{
    public class RuntimeTypesTests
    {
        [Test]
        public void FixedLookBlend_Evaluate_InterpolatesAndClamps()
        {
            var start = Quaternion.identity;
            var end = Quaternion.Euler(0f, 10f, 0f);
            Assert.AreEqual(0f, Quaternion.Angle(start, FixedLookBlend.Evaluate(start, end, 0f)), 1e-3f);
            Assert.AreEqual(0f, Quaternion.Angle(end, FixedLookBlend.Evaluate(start, end, 1f)), 1e-3f);
            Assert.AreEqual(5f, Quaternion.Angle(start, FixedLookBlend.Evaluate(start, end, 0.5f)), 1e-2f);
            Assert.AreEqual(0f, Quaternion.Angle(end, FixedLookBlend.Evaluate(start, end, 1.7f)), 1e-3f);
            Assert.AreEqual(0f, Quaternion.Angle(start, FixedLookBlend.Evaluate(start, end, -0.3f)), 1e-3f);
        }

        [Test]
        public void CarCamShot_ShotName_StripsPrefix()
        {
            var go = new GameObject(CarCamShot.ObjectPrefix + "WalkUp");
            try
            {
                Assert.AreEqual("WalkUp", go.AddComponent<CarCamShot>().ShotName);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void LookTarget_DefaultsToCenter()
        {
            Assert.AreEqual(LookTarget.Center, new LookTarget().part);
            Assert.AreEqual("(az 90°, d 3.5 m, h 1.25 m)", new CamPoint(90f, 3.5f, 1.25f).ToString());
        }
    }
}
```

- [ ] **Step 2: Tests laufen lassen – müssen fehlschlagen**

Erwartet: Compile-Fehler (`FixedLookBlend`, `CarCamShot`, `LookTarget`, `CamPoint` nicht definiert).

- [ ] **Step 3: Typen implementieren**

`Assets/CarCam/Runtime/CamPoint.cs`:
```csharp
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
```

`Assets/CarCam/Runtime/LookTarget.cs`:
```csharp
using System;

namespace CarCam
{
    /// <summary>What the camera looks at: a named car part (or "Center") plus a vertical offset in meters.</summary>
    [Serializable]
    public class LookTarget
    {
        public const string Center = "Center";

        public string part = Center;
        public float heightOffset;

        public LookTarget() { }

        public LookTarget(string part, float heightOffset = 0f)
        {
            this.part = part;
            this.heightOffset = heightOffset;
        }

        public override string ToString() => heightOffset == 0f ? part : FormattableString.Invariant($"{part} {heightOffset:+0.##;-0.##} m");
    }
}
```

`Assets/CarCam/Runtime/CarCamShot.cs`:
```csharp
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

namespace CarCam
{
    public enum ShotState
    {
        EndpointsSet,
        PathValid,
        Finalized
    }

    /// <summary>Session state of one car showroom shot. Lives on the root object "CarCamShot_&lt;name&gt;"; spline and camera are children.</summary>
    [DisallowMultipleComponent]
    public class CarCamShot : MonoBehaviour
    {
        public const string ObjectPrefix = "CarCamShot_";

        public Transform car;
        public CamPoint start;
        public CamPoint end;
        public bool hasStartLookTarget;
        public LookTarget startLookTarget = new();
        public LookTarget endLookTarget = new();
        public float fov = 35f;

        public Quaternion startRotation = Quaternion.identity;
        public Quaternion endRotation = Quaternion.identity;
        public Vector3 startLookPoint;
        public Vector3 endLookPoint;

        public List<CamPoint> intermediatePoints = new();
        public ShotState state;
        public int validationAttempts;
        [TextArea(3, 12)] public string lastReport;

        public SplineContainer spline;
        public CinemachineCamera vcam;

        public string ShotName => name.StartsWith(ObjectPrefix) ? name.Substring(ObjectPrefix.Length) : name;
    }
}
```

`Assets/CarCam/Runtime/FixedLookBlend.cs`:
```csharp
using Unity.Cinemachine;
using UnityEngine;

namespace CarCam
{
    /// <summary>Assetto-Corsa-style look: the camera orientation is a slerp between a fixed start and end rotation,
    /// driven by the spline dolly's normalized position. No target tracking.</summary>
    [AddComponentMenu("Cinemachine/Procedural/Extensions/Fixed Look Blend")]
    public class FixedLookBlend : CinemachineExtension
    {
        public Quaternion StartRotation = Quaternion.identity;
        public Quaternion EndRotation = Quaternion.identity;
        public CinemachineSplineDolly Dolly;

        public static Quaternion Evaluate(Quaternion start, Quaternion end, float progress) =>
            Quaternion.Slerp(start, end, Mathf.Clamp01(progress));

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Aim || Dolly == null)
            {
                return;
            }
            // dolly units are Normalized, so CameraPosition is the 0..1 progress along the path
            state.RawOrientation = Evaluate(StartRotation, EndRotation, Dolly.CameraPosition);
        }
    }
}
```

- [ ] **Step 4: Tests laufen lassen – müssen passen**

Erwartet: `RuntimeTypesTests` PASS (3 Tests).

---

### Task 4: `CarFrame` – Auto-Bezugssystem, Umrechnung, Blick-Klemmung

**Files:**
- Create: `Assets/Editor/CarCam/CarFrame.cs`
- Create: `Assets/Tests/Editor/CarCam/CarCamTestScene.cs` (Test-Fixture, wird in Task 5–7 wiederverwendet)
- Test: `Assets/Tests/Editor/CarCam/CarFrameTests.cs`

**Interfaces:**
- Consumes: `CamPoint`, `LookTarget` (Task 3); `AiCameraDirectorBridge.BuildSpline` (Task 2, für die Fixture).
- Produces (`public readonly struct CarFrame`):
  - Felder `Transform Car`, `Bounds Bounds`, `Vector3 Origin`, `Vector3 Forward`, `Vector3 Right`, `Vector3 Size` (x = Breite, y = Höhe, z = Länge), Property `float FloorY`
  - `const float MaxLookRotationDeg = 10f`
  - `static bool TryCreate(Transform car, out CarFrame frame)`
  - `Vector3 ToWorld(CamPoint p)`, `CamPoint ToCamPoint(Vector3 world)`
  - `bool TryResolveLookPoint(LookTarget target, out Vector3 point)`
  - `Transform FindPart(string partName)`, `IEnumerable<string> PartNames()`, `static Bounds PartBounds(Transform part)`
  - `static Quaternion LookAt(Vector3 from, Vector3 to)` (Roll 0)
  - `static Quaternion ClampStartRotation(Quaternion end, Quaternion desiredStart, float maxDeg, out float requestedDeg)` – Ergebnis hat Roll 0 und `Quaternion.Angle(end, result) <= maxDeg`
- Produces (Test-Fixture `CarCam.Tests.CarCamTestScene`): `NewEmptyScene()`, `CreateCar(bool withColliders = true)`, `CreatePillar(Vector3 position)`, `Spline(params Vector3[] points)`, Konstante `CarName = "TestCar"`, `CarCenter = (0, 0.7, 0)`.

**Testauto:** Boden-Oberkante y = 0; Auto 2 m breit, 1.4 m hoch, 4.5 m lang, Mitte im Ursprung, Front zeigt nach +Z.

- [ ] **Step 1: Test-Fixture schreiben**

`Assets/Tests/Editor/CarCam/CarCamTestScene.cs`:
```csharp
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace CarCam.Tests
{
    /// <summary>Floor top at y = 0. Car "TestCar": 2 m wide, 1.4 m high, 4.5 m long, centered at the origin, front = +Z.</summary>
    public static class CarCamTestScene
    {
        public const string CarName = "TestCar";
        public static readonly Vector3 CarCenter = new(0f, 0.7f, 0f);

        public static void NewEmptyScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        public static Transform CreateCar(bool withColliders = true)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);

            var car = new GameObject(CarName).transform;
            Part(car, "Body", new Vector3(0f, 0.7f, 0f), new Vector3(2f, 1.4f, 4.5f), withColliders);
            Part(car, "Headlight_L", new Vector3(-0.7f, 0.6f, 2.2f), new Vector3(0.3f, 0.15f, 0.1f), withColliders);
            Part(car, "Wheel_FR", new Vector3(0.85f, 0.35f, 1.4f), new Vector3(0.3f, 0.7f, 0.7f), withColliders);
            Part(car, "Wheel_RR", new Vector3(0.85f, 0.35f, -1.4f), new Vector3(0.3f, 0.7f, 0.7f), withColliders);
            Physics.SyncTransforms();
            return car;
        }

        public static GameObject CreatePillar(Vector3 position)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "Pillar";
            pillar.transform.position = position;
            pillar.transform.localScale = new Vector3(0.5f, 3f, 0.5f);
            Physics.SyncTransforms();
            return pillar;
        }

        public static SplineContainer Spline(params Vector3[] points) =>
            AiCameraDirectorBridge.BuildSpline(new GameObject("TestPath"), points);

        static void Part(Transform car, string name, Vector3 position, Vector3 scale, bool withCollider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(car, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            if (!withCollider)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
        }
    }
}
```

- [ ] **Step 2: Fehlschlagende Tests schreiben**

`Assets/Tests/Editor/CarCam/CarFrameTests.cs`:
```csharp
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
        public void TryResolveLookPoint_CenterPartAndUnknown()
        {
            var frame = Frame();
            Assert.IsTrue(frame.TryResolveLookPoint(new LookTarget(LookTarget.Center, 0.3f), out var center));
            AssertVec(new Vector3(0f, 1.0f, 0f), center);
            Assert.IsTrue(frame.TryResolveLookPoint(new LookTarget("Headlight_L"), out var headlight));
            AssertVec(new Vector3(-0.7f, 0.6f, 2.2f), headlight);
            Assert.IsFalse(frame.TryResolveLookPoint(new LookTarget("Spoiler"), out _));
            Assert.IsFalse(frame.TryResolveLookPoint(null, out _));
        }

        [Test]
        public void PartNames_ListsRenderedChildrenOnly()
        {
            var names = Frame().PartNames().ToList();
            CollectionAssert.AreEquivalent(new[] { "Body", "Headlight_L", "Wheel_FR", "Wheel_RR" }, names);
        }

        [Test]
        public void ClampStartRotation_LimitsToMaxAngle()
        {
            var end = Quaternion.LookRotation(Vector3.forward);
            var desired = Quaternion.Euler(0f, 30f, 0f);
            var result = CarFrame.ClampStartRotation(end, desired, CarFrame.MaxLookRotationDeg, out var requested);
            Assert.AreEqual(30f, requested, 1e-2f);
            Assert.That(Quaternion.Angle(end, result), Is.InRange(9.9f, 10.0001f));
        }

        [Test]
        public void ClampStartRotation_KeepsSmallRotations()
        {
            var end = Quaternion.LookRotation(Vector3.forward);
            var desired = Quaternion.Euler(0f, 5f, 0f);
            var result = CarFrame.ClampStartRotation(end, desired, CarFrame.MaxLookRotationDeg, out var requested);
            Assert.AreEqual(5f, requested, 1e-2f);
            Assert.AreEqual(0f, Quaternion.Angle(desired, result), 1e-3f);
        }

        [Test]
        public void ClampStartRotation_WithPitch_StaysWithinMaxAndHasNoRoll()
        {
            var end = Quaternion.LookRotation(new Vector3(0f, -0.3f, 1f));
            var desired = Quaternion.LookRotation(new Vector3(1f, 0.2f, 0.3f));
            var result = CarFrame.ClampStartRotation(end, desired, CarFrame.MaxLookRotationDeg, out _);
            Assert.That(Quaternion.Angle(end, result), Is.LessThanOrEqualTo(10.0001f));
            var right = result * Vector3.right;
            Assert.AreEqual(0f, right.y, 1e-3f, "roll must be 0");
        }
    }
}
```

- [ ] **Step 3: Tests laufen lassen – müssen fehlschlagen**

Erwartet: Compile-Fehler (`CarFrame` nicht definiert).

- [ ] **Step 4: `CarFrame` implementieren**

`Assets/Editor/CarCam/CarFrame.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CarCam
{
    /// <summary>Car-relative reference frame: origin = car bounds center projected to floor height, forward = car forward (horizontal).</summary>
    public readonly struct CarFrame
    {
        public const float MaxLookRotationDeg = 10f;

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

        public bool TryResolveLookPoint(LookTarget target, out Vector3 point)
        {
            point = default;
            if (target == null)
            {
                return false;
            }
            if (string.IsNullOrEmpty(target.part) || target.part == LookTarget.Center)
            {
                point = Bounds.center + Vector3.up * target.heightOffset;
                return true;
            }
            var part = FindPart(target.part);
            if (part == null)
            {
                return false;
            }
            point = PartBounds(part).center + Vector3.up * target.heightOffset;
            return true;
        }

        public Transform FindPart(string partName) =>
            Car.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t != Car && t.name == partName);

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

        public static Quaternion LookAt(Vector3 from, Vector3 to) => Quaternion.LookRotation(to - from, Vector3.up);

        /// <summary>Returns desiredStart if it is within maxDeg of end; otherwise the roll-free rotation
        /// furthest towards desiredStart that stays within maxDeg (binary search on the slerp factor).</summary>
        public static Quaternion ClampStartRotation(Quaternion end, Quaternion desiredStart, float maxDeg, out float requestedDeg)
        {
            requestedDeg = Quaternion.Angle(end, desiredStart);
            if (requestedDeg <= maxDeg)
            {
                return desiredStart;
            }

            float lo = 0f, hi = 1f;
            for (int i = 0; i < 24; i++)
            {
                var mid = (lo + hi) * 0.5f;
                if (Quaternion.Angle(end, NoRoll(Quaternion.Slerp(end, desiredStart, mid))) <= maxDeg)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }
            return NoRoll(Quaternion.Slerp(end, desiredStart, lo));
        }

        static Quaternion NoRoll(Quaternion q) => Quaternion.LookRotation(q * Vector3.forward, Vector3.up);
    }
}
```

- [ ] **Step 5: Tests laufen lassen – müssen passen**

Erwartet: alle `CarFrameTests` PASS.

---

### Task 5: `PathValidator` und `CarColliderScope`

**Files:**
- Create: `Assets/Editor/CarCam/PathValidator.cs`
- Create: `Assets/Editor/CarCam/CarColliderScope.cs`
- Test: `Assets/Tests/Editor/CarCam/PathValidatorTests.cs`

**Interfaces:**
- Consumes: `FixedLookBlend.Evaluate` (Task 3), `CarCamTestScene` (Task 4).
- Produces:
  - `public struct PathValidationSettings { float SampleSpacing; int MinSamples; float CameraRadius, MinFloorClearance, MinCarDistance, MaxKinkDeg; static PathValidationSettings Default }`
  - `public enum PathIssue { Clipping, BelowMinHeight, TooCloseToCar, TargetNotVisible, Kink }`
  - `public struct PathInput { SplineContainer Spline; Transform Car; Quaternion StartRotation, EndRotation; Vector3 StartLookPoint, EndLookPoint; float Fov; float FloorY; }`
  - `public class PathProblem { PathIssue Issue; float TStart, TEnd; string Detail; string NearestPoint; string ToText(); static string IssueCode(PathIssue) }`
  - `public class PathReport { float Length; float MinHeightAboveFloor; int SampleCount; List<PathProblem> Problems; bool IsValid; string ToText(); }`
  - `public static PathReport PathValidator.Validate(PathInput input, PathValidationSettings settings)`
  - `public sealed class CarColliderScope : IDisposable { CarColliderScope(Transform car); int AddedCount; static int CountMissing(Transform car) }`
- Issue-Codes im Text: `CLIPPING`, `BELOW_MIN_HEIGHT`, `TOO_CLOSE_TO_CAR`, `TARGET_NOT_VISIBLE`, `KINK`. Zeilenformat: `t 0.42–0.55: CLIPPING with Pillar – nearest point: intermediate #0`.

- [ ] **Step 1: Fehlschlagende Tests schreiben**

`Assets/Tests/Editor/CarCam/PathValidatorTests.cs`:
```csharp
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

        PathInput Input(SplineContainer spline, Quaternion? rotation = null)
        {
            var look = CarCamTestScene.CarCenter;
            Vector3 endPos = spline.EvaluatePosition(1f);
            var rot = rotation ?? Quaternion.LookRotation(look - endPos, Vector3.up);
            return new PathInput
            {
                Spline = spline, Car = m_Car,
                StartRotation = rot, EndRotation = rot,
                StartLookPoint = look, EndLookPoint = look,
                Fov = 35f, FloorY = 0f
            };
        }

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
        public void LookingAwayFromCar_ReportsTargetNotVisible()
        {
            var report = Validate(Input(WalkUp(), Quaternion.LookRotation(Vector3.forward)));
            var problem = report.Problems.FirstOrDefault(p => p.Issue == PathIssue.TargetNotVisible);
            Assert.IsNotNull(problem, report.ToText());
            Assert.AreEqual(0f, problem.TStart, 1e-4f);
            Assert.AreEqual(1f, problem.TEnd, 1e-4f);
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
    }
}
```

- [ ] **Step 2: Tests laufen lassen – müssen fehlschlagen**

Erwartet: Compile-Fehler (`PathValidator`, `PathInput`, `CarColliderScope` … nicht definiert).

- [ ] **Step 3: `CarColliderScope` implementieren**

`Assets/Editor/CarCam/CarColliderScope.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CarCam
{
    /// <summary>Adds MeshColliders to car parts that have none, for the lifetime of the scope (path validation needs colliders).</summary>
    public sealed class CarColliderScope : IDisposable
    {
        readonly List<MeshCollider> m_Added = new();

        public int AddedCount => m_Added.Count;

        public CarColliderScope(Transform car)
        {
            foreach (var meshFilter in MissingColliders(car))
            {
                var meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.hideFlags = HideFlags.DontSave;
                m_Added.Add(meshCollider);
            }
            Physics.SyncTransforms();
        }

        public static int CountMissing(Transform car) => MissingColliders(car).Count();

        static IEnumerable<MeshFilter> MissingColliders(Transform car) =>
            car.GetComponentsInChildren<MeshFilter>()
                .Where(mf => mf.sharedMesh != null && mf.GetComponent<Collider>() == null);

        public void Dispose()
        {
            foreach (var meshCollider in m_Added)
            {
                if (meshCollider != null)
                {
                    Object.DestroyImmediate(meshCollider);
                }
            }
            m_Added.Clear();
            Physics.SyncTransforms();
        }
    }
}
```

- [ ] **Step 4: `PathValidator` implementieren**

`Assets/Editor/CarCam/PathValidator.cs`:
```csharp
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
        TargetNotVisible,
        Kink
    }

    public struct PathInput
    {
        public SplineContainer Spline;
        public Transform Car;
        public Quaternion StartRotation;
        public Quaternion EndRotation;
        public Vector3 StartLookPoint;
        public Vector3 EndLookPoint;
        public float Fov;
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
            PathIssue.TargetNotVisible => "TARGET_NOT_VISIBLE",
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

    /// <summary>Samples the spline from t = 0 to 1 and checks every camera pose (position + interpolated rotation).</summary>
    public static class PathValidator
    {
        public static PathReport Validate(PathInput input, PathValidationSettings settings)
        {
            Physics.SyncTransforms();
            var container = input.Spline;
            var length = container.CalculateLength();
            var count = Mathf.Max(settings.MinSamples, Mathf.CeilToInt(length / settings.SampleSpacing) + 1);
            var halfFov = input.Fov * 0.5f;

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
                var rot = FixedLookBlend.Evaluate(input.StartRotation, input.EndRotation, t);
                var lookPoint = Vector3.Lerp(input.StartLookPoint, input.EndLookPoint, t);

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

                // look target in frame and not occluded
                var toTarget = lookPoint - pos;
                var offAxis = Vector3.Angle(rot * Vector3.forward, toTarget);
                if (offAxis > halfFov)
                {
                    issues.Add((PathIssue.TargetNotVisible, Invariant($"target {offAxis:0}° off-axis (max {halfFov:0}°)")));
                }
                else if (Physics.Raycast(pos, toTarget.normalized, out var hit, toTarget.magnitude, ~0, QueryTriggerInteraction.Ignore)
                         && !IsCar(hit.collider, input.Car))
                {
                    issues.Add((PathIssue.TargetNotVisible, $"occluded by {hit.collider.name}"));
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
```

- [ ] **Step 5: Tests laufen lassen – müssen passen**

Erwartet: alle `PathValidatorTests` PASS.
Falls `BackAndForth_ReportsKink` scheitert, weil der Spline bei kollinearen Punkten keine sichtbare Umkehr erzeugt: im Test den mittleren Punkt leicht seitlich versetzen (`new Vector3(0.3f, 1.5f, 5f)`) – die Umkehr bleibt, die Tangente wird aber eindeutig. Den Validator dafür NICHT lockern.

---

### Task 6: `ShotBuilder` – Spline-Neubau, Kamera und Timeline

**Files:**
- Create: `Assets/Editor/CarCam/ShotBuilder.cs`
- Test: `Assets/Tests/Editor/CarCam/ShotBuilderTests.cs`

**Interfaces:**
- Consumes: `CarCamShot`, `FixedLookBlend` (Task 3); `AiCameraDirectorBridge.EnsureBrain/BuildSpline`, `TimelineAiBridge.GetOrCreateTimelineAsset/CreateDirector/CreateTrack/AddShotClip/AddAnimationClip/SetClipCurve`, `EasingCurves.Create` (Task 2).
- Produces:
  - `public const string ShotBuilder.DefaultTimelineName = "CarCamTimeline"`
  - `public const string ShotBuilder.SplinePositionProperty = "m_SplineSettings.Position"`
  - `public static SplineContainer ShotBuilder.RebuildSpline(CarCamShot shot, IReadOnlyList<Vector3> worldPositions)` – ersetzt das Kind `Path`
  - `public static ShotBuilder.FinalizeResult ShotBuilder.Finalize(CarCamShot shot, float duration, Easing easing, string timelineName = DefaultTimelineName)`
  - `public struct FinalizeResult { string TimelineName, DirectorName, CameraName; double Start, Duration; }`
- Konventionen: VCam-GameObject heißt `{shotName}_Cam` (Kind des Session-Objekts), Animation-Track heißt genauso; Shot-Clip und Animations-Clip haben `displayName == shotName`.

- [ ] **Step 1: Fehlschlagende Tests schreiben**

`Assets/Tests/Editor/CarCam/ShotBuilderTests.cs`:
```csharp
using System.Linq;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CarCam.Tests
{
    public class ShotBuilderTests
    {
        const string TestTimeline = "CarCamTimeline_ShotBuilderTest";

        [SetUp]
        public void SetUp()
        {
            CarCamTestScene.NewEmptyScene();
            CarCamTestScene.CreateCar();
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset($"Assets/{TestTimeline}.playable");

        static CarCamShot NewShot(string name)
        {
            var shot = new GameObject(CarCamShot.ObjectPrefix + name).AddComponent<CarCamShot>();
            shot.fov = 30f;
            shot.startRotation = Quaternion.Euler(8f, 180f, 0f);
            shot.endRotation = Quaternion.Euler(10f, 185f, 0f);
            ShotBuilder.RebuildSpline(shot, new[] { new Vector3(0f, 1.6f, 9f), new Vector3(0f, 1.5f, 6.5f), new Vector3(0f, 1.4f, 4f) });
            shot.state = ShotState.PathValid;
            return shot;
        }

        static TimelineAsset Timeline() => AssetDatabase.LoadAssetAtPath<TimelineAsset>($"Assets/{TestTimeline}.playable");
        static CinemachineTrack CmTrack() => Timeline().GetOutputTracks().OfType<CinemachineTrack>().Single();

        [Test]
        public void RebuildSpline_ReplacesPathChild()
        {
            var shot = NewShot("A");
            ShotBuilder.RebuildSpline(shot, new[] { Vector3.zero, new Vector3(0f, 1f, 3f) });
            Assert.AreEqual(1, shot.GetComponentsInChildren<UnityEngine.Splines.SplineContainer>().Length);
            Assert.AreEqual(2, shot.spline.Spline.Count);
            Assert.AreEqual("Path", shot.spline.gameObject.name);
        }

        [Test]
        public void Finalize_BuildsCameraWithDollyAndFixedLook()
        {
            var shot = NewShot("A");
            var result = ShotBuilder.Finalize(shot, 6f, Easing.EaseInOut, TestTimeline);

            var vcam = shot.vcam;
            Assert.IsNotNull(vcam);
            Assert.AreEqual("A_Cam", vcam.gameObject.name);
            Assert.AreEqual("A_Cam", result.CameraName);
            Assert.AreEqual(30f, vcam.Lens.FieldOfView, 1e-4f);
            Assert.IsNull(vcam.GetComponent<CinemachineRotationComposer>());
            var dolly = vcam.GetComponent<CinemachineSplineDolly>();
            Assert.AreSame(shot.spline, dolly.Spline);
            var blend = vcam.GetComponent<FixedLookBlend>();
            Assert.AreSame(dolly, blend.Dolly);
            Assert.AreEqual(0f, Quaternion.Angle(shot.startRotation, blend.StartRotation), 1e-3f);
            Assert.AreEqual(0f, Quaternion.Angle(shot.endRotation, blend.EndRotation), 1e-3f);
        }

        [Test]
        public void Finalize_CreatesTimelineShotAndEasedCurve()
        {
            var shot = NewShot("A");
            var result = ShotBuilder.Finalize(shot, 6f, Easing.EaseInOut, TestTimeline);

            Assert.AreEqual(TestTimeline, result.TimelineName);
            Assert.AreEqual(0d, result.Start, 1e-6);
            var clip = CmTrack().GetClips().Single();
            Assert.AreEqual("A", clip.displayName);
            Assert.AreEqual(6d, clip.duration, 1e-6);

            var director = Object.FindObjectsByType<PlayableDirector>().Single();
            Assert.AreSame(Timeline(), director.playableAsset);
            Assert.AreEqual(result.DirectorName, director.gameObject.name);

            var animTrack = Timeline().GetOutputTracks().OfType<AnimationTrack>().Single(t => t.name == "A_Cam");
            var animClip = animTrack.GetClips().Single();
            var binding = EditorCurveBinding.FloatCurve("", typeof(CinemachineSplineDolly), ShotBuilder.SplinePositionProperty);
            var curve = AnimationUtility.GetEditorCurve(animClip.animationClip, binding);
            Assert.AreEqual(0.5f, curve.Evaluate(3f), 1e-3f);
            Assert.Less(curve.Evaluate(0.6f), 0.1f);
        }

        [Test]
        public void Finalize_Twice_ReplacesClipsInsteadOfDuplicating()
        {
            var shot = NewShot("A");
            ShotBuilder.Finalize(shot, 6f, Easing.EaseInOut, TestTimeline);
            ShotBuilder.Finalize(shot, 4f, Easing.Linear, TestTimeline);

            var clip = CmTrack().GetClips().Single();
            Assert.AreEqual(0d, clip.start, 1e-6);
            Assert.AreEqual(4d, clip.duration, 1e-6);
            var animTrack = Timeline().GetOutputTracks().OfType<AnimationTrack>().Single(t => t.name == "A_Cam");
            Assert.AreEqual(1, animTrack.GetClips().Count());
            Assert.AreEqual(1, Object.FindObjectsByType<PlayableDirector>().Length);
            Assert.AreEqual(1, Object.FindObjectsByType<CinemachineCamera>().Length);
        }

        [Test]
        public void Finalize_SecondShot_IsAppendedAfterFirst()
        {
            ShotBuilder.Finalize(NewShot("A"), 6f, Easing.EaseInOut, TestTimeline);
            var result = ShotBuilder.Finalize(NewShot("B"), 5f, Easing.EaseInOut, TestTimeline);

            Assert.AreEqual(6d, result.Start, 1e-6);
            Assert.AreEqual(2, CmTrack().GetClips().Count());
        }
    }
}
```

- [ ] **Step 2: Tests laufen lassen – müssen fehlschlagen**

Erwartet: Compile-Fehler (`ShotBuilder` nicht definiert).

- [ ] **Step 3: `ShotBuilder` implementieren**

`Assets/Editor/CarCam/ShotBuilder.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Splines;
using UnityEngine.Timeline;

namespace CarCam
{
    /// <summary>Builds the scene objects and timeline entries of a CarCamShot, using the existing bridges as building blocks.</summary>
    public static class ShotBuilder
    {
        public const string DefaultTimelineName = "CarCamTimeline";
        public const string SplinePositionProperty = "m_SplineSettings.Position";
        const string CinemachineTrackName = "Cinemachine Track";

        public struct FinalizeResult
        {
            public string TimelineName;
            public string DirectorName;
            public string CameraName;
            public double Start;
            public double Duration;
        }

        public static SplineContainer RebuildSpline(CarCamShot shot, IReadOnlyList<Vector3> worldPositions)
        {
            if (shot.spline != null)
            {
                Undo.DestroyObjectImmediate(shot.spline.gameObject);
            }

            var pathGo = new GameObject("Path");
            Undo.RegisterCreatedObjectUndo(pathGo, "Create CarCam Path");
            pathGo.transform.SetParent(shot.transform, false);
            shot.spline = AiCameraDirectorBridge.BuildSpline(pathGo, worldPositions);
            EditorUtility.SetDirty(shot);
            return shot.spline;
        }

        public static FinalizeResult Finalize(CarCamShot shot, float duration, Easing easing, string timelineName = DefaultTimelineName)
        {
            var shotName = shot.ShotName;
            var brain = AiCameraDirectorBridge.EnsureBrain();
            var vcam = EnsureCamera(shot);

            var timeline = TimelineAiBridge.GetOrCreateTimelineAsset(timelineName);
            var director = Object.FindObjectsByType<PlayableDirector>().FirstOrDefault(d => d.playableAsset == timeline)
                           ?? TimelineAiBridge.CreateDirector(timelineName, timeline);

            // ONE cinemachine track per timeline
            var cmTrack = timeline.GetOutputTracks().OfType<CinemachineTrack>().FirstOrDefault()
                          ?? TimelineAiBridge.CreateTrack<CinemachineTrack>(timeline, director, CinemachineTrackName, brain);
            director.SetGenericBinding(cmTrack, brain);

            // replace this shot's clip, or append after the last clip
            var existing = cmTrack.GetClips().FirstOrDefault(c => c.displayName == shotName);
            var start = existing?.start ?? cmTrack.GetClips().Select(c => c.end).DefaultIfEmpty(0d).Max();
            if (existing != null)
            {
                timeline.DeleteClip(existing);
            }
            var shotClip = TimelineAiBridge.AddShotClip(timeline, cmTrack, director, vcam, start, duration);
            shotClip.displayName = shotName;

            // animation track drives the dolly position 0 -> 1
            var animator = vcam.GetComponent<Animator>();
            var animTrackName = vcam.gameObject.name;
            var animTrack = timeline.GetOutputTracks().OfType<AnimationTrack>().FirstOrDefault(t => t.name == animTrackName);
            if (animTrack == null)
            {
                animTrack = TimelineAiBridge.CreateTrack<AnimationTrack>(timeline, director, animTrackName, animator);
            }
            else
            {
                foreach (var oldClip in animTrack.GetClips().ToList())
                {
                    DeleteClipWithAnimation(timeline, oldClip);
                }
                director.SetGenericBinding(animTrack, animator);
            }
            var animClip = TimelineAiBridge.AddAnimationClip(timeline, animTrack, shotName, start, duration);
            TimelineAiBridge.SetClipCurve(timeline, animClip.animationClip, typeof(CinemachineSplineDolly),
                SplinePositionProperty, EasingCurves.Create(easing, duration));

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(shot.gameObject.scene);

            return new FinalizeResult
            {
                TimelineName = timelineName,
                DirectorName = director.gameObject.name,
                CameraName = vcam.gameObject.name,
                Start = start,
                Duration = duration
            };
        }

        static CinemachineCamera EnsureCamera(CarCamShot shot)
        {
            var vcam = shot.vcam;
            if (vcam == null)
            {
                var camGo = new GameObject(shot.ShotName + "_Cam");
                Undo.RegisterCreatedObjectUndo(camGo, "Create CarCam Camera");
                camGo.transform.SetParent(shot.transform, false);
                vcam = camGo.AddComponent<CinemachineCamera>();
                camGo.AddComponent<CinemachineSplineDolly>();
                camGo.AddComponent<FixedLookBlend>();
                camGo.AddComponent<Animator>();
                shot.vcam = vcam;
            }

            vcam.Lens.FieldOfView = shot.fov;

            var dolly = vcam.GetComponent<CinemachineSplineDolly>();
            dolly.Spline = shot.spline;
            dolly.CameraPosition = 0f;
            dolly.CameraRotation = CinemachineSplineDolly.RotationMode.Default;
            dolly.Damping.Enabled = false;

            var blend = vcam.GetComponent<FixedLookBlend>();
            blend.StartRotation = shot.startRotation;
            blend.EndRotation = shot.endRotation;
            blend.Dolly = dolly;

            // scene-view preview of the first frame
            vcam.transform.SetPositionAndRotation(shot.spline.EvaluatePosition(0f), shot.startRotation);

            EditorUtility.SetDirty(vcam);
            EditorUtility.SetDirty(dolly);
            EditorUtility.SetDirty(blend);
            EditorUtility.SetDirty(shot);
            return vcam;
        }

        static void DeleteClipWithAnimation(TimelineAsset timeline, TimelineClip clip)
        {
            var animation = clip.animationClip;
            timeline.DeleteClip(clip);
            if (animation != null && AssetDatabase.GetAssetPath(animation) == AssetDatabase.GetAssetPath(timeline))
            {
                AssetDatabase.RemoveObjectFromAsset(animation);
                Object.DestroyImmediate(animation, true);
            }
        }
    }
}
```

- [ ] **Step 4: Tests laufen lassen – müssen passen**

Erwartet: alle `ShotBuilderTests` PASS.
Hinweis: `SetPositionAndRotation` erwartet `Vector3`; `EvaluatePosition` liefert `float3` – die implizite Konvertierung braucht die `Unity.Mathematics`-Referenz aus Task 1.

---

### Task 7: `CarCamSessions` und die vier `[AgentTool]`s

**Files:**
- Create: `Assets/Editor/CarCam/CarCamSessions.cs`
- Create: `Assets/Editor/CarCam/CarCamTools.cs`
- Test: `Assets/Tests/Editor/CarCam/CarCamToolsTests.cs`

**Interfaces:**
- Consumes: alles aus Task 2–6.
- Produces:
  - `CarCamSessions.DefaultCarName = "BlockoutCar"`, `Find(string) : CarCamShot`, `Names() : IEnumerable<string>`, `Create(string) : CarCamShot`, `TryGetCarFrame(string carName, out CarFrame, out string error) : bool`
  - `CarCamTools.GetCarContext(string carName = "BlockoutCar") : string` – ID `CarCam.GetCarContext`
  - `CarCamTools.SetEndpoints(string shotName, CamPoint start, CamPoint end, LookTarget endLookTarget, LookTarget startLookTarget = null, float fov = 35f, string carName = "BlockoutCar") : string` – ID `CarCam.SetEndpoints`
  - `CarCamTools.BuildPath(string shotName, List<CamPoint> intermediatePoints) : string` – ID `CarCam.BuildPath`
  - `CarCamTools.FinalizeShot(string shotName, float duration, Easing easing = Easing.EaseInOut) : string` – ID `CarCam.FinalizeShot`
  - `internal static string CarCamTools.TimelineName` (Default `ShotBuilder.DefaultTimelineName`; Tests überschreiben es)
  - `public const int CarCamTools.MaxAutoAttempts = 3`

- [ ] **Step 1: Fehlschlagende Tests schreiben**

`Assets/Tests/Editor/CarCam/CarCamToolsTests.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.AI.Assistant.FunctionCalling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using Unity.Cinemachine;

namespace CarCam.Tests
{
    public class CarCamToolsTests
    {
        const string TestTimeline = "CarCamTimeline_ToolsTest";
        const string Car = CarCamTestScene.CarName;
        const string Success = "[[PROMPTRETURN]] SUCCESS";
        const string Failure = "[[PROMPTRETURN]] FAILURE";

        static readonly CamPoint Start = new(0f, 9f, 1.6f);
        static readonly CamPoint End = new(0f, 4f, 1.4f);
        static readonly LookTarget Center = new(LookTarget.Center);

        [SetUp]
        public void SetUp()
        {
            CarCamTestScene.NewEmptyScene();
            CarCamTestScene.CreateCar();
            CarCamTools.TimelineName = TestTimeline;
        }

        [TearDown]
        public void TearDown()
        {
            CarCamTools.TimelineName = ShotBuilder.DefaultTimelineName;
            AssetDatabase.DeleteAsset($"Assets/{TestTimeline}.playable");
        }

        static List<CamPoint> Mid(float height = 1.5f) => new() { new CamPoint(0f, 6.5f, height) };

        [Test]
        public void Tools_AreRegisteredWithExpectedIds()
        {
            var ids = typeof(CarCamTools).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Select(m => m.GetCustomAttribute<AgentToolAttribute>()?.Id)
                .Where(id => id != null)
                .ToList();
            CollectionAssert.AreEquivalent(
                new[] { "CarCam.GetCarContext", "CarCam.SetEndpoints", "CarCam.BuildPath", "CarCam.FinalizeShot" }, ids);
        }

        [Test]
        public void GetCarContext_ListsPartsAndSize()
        {
            var result = CarCamTools.GetCarContext(Car);
            StringAssert.StartsWith(Success, result);
            StringAssert.Contains("length 4.50 m", result);
            StringAssert.Contains("Headlight_L", result);
            StringAssert.Contains("CarCam.SetEndpoints", result);
        }

        [Test]
        public void GetCarContext_UnknownCar_Fails()
        {
            StringAssert.StartsWith(Failure, CarCamTools.GetCarContext("NoSuchCar"));
        }

        [Test]
        public void SetEndpoints_UnknownPart_FailsWithValidPartList()
        {
            var result = CarCamTools.SetEndpoints("A", Start, End, new LookTarget("Spoiler"), carName: Car);
            StringAssert.StartsWith(Failure, result);
            StringAssert.Contains("Headlight_L", result);
        }

        [Test]
        public void SetEndpoints_InvalidParameters_Fail()
        {
            StringAssert.StartsWith(Failure, CarCamTools.SetEndpoints("", Start, End, Center, carName: Car));
            StringAssert.StartsWith(Failure, CarCamTools.SetEndpoints("A", new CamPoint(0f, 0f, 1f), End, Center, carName: Car));
            StringAssert.StartsWith(Failure, CarCamTools.SetEndpoints("A", Start, End, Center, fov: 0f, carName: Car));
            StringAssert.StartsWith(Failure, CarCamTools.SetEndpoints("A", Start, End, null, carName: Car));
        }

        [Test]
        public void SetEndpoints_ClampsLargeStartRotation()
        {
            var result = CarCamTools.SetEndpoints("A", new CamPoint(40f, 6f, 1.2f), End, Center,
                startLookTarget: new LookTarget("Wheel_RR"), carName: Car);
            StringAssert.StartsWith(Success, result);
            StringAssert.Contains("clamped to 10", result);
            var shot = CarCamSessions.Find("A");
            Assert.That(Quaternion.Angle(shot.startRotation, shot.endRotation), Is.LessThanOrEqualTo(10.0001f));
            Assert.AreEqual(ShotState.EndpointsSet, shot.state);
        }

        [Test]
        public void BuildPath_UnknownShot_Fails()
        {
            var result = CarCamTools.BuildPath("Nope", Mid());
            StringAssert.StartsWith(Failure, result);
            StringAssert.Contains("CarCam.SetEndpoints", result);
        }

        [Test]
        public void FinalizeShot_WithoutValidPath_Fails()
        {
            CarCamTools.SetEndpoints("A", Start, End, Center, carName: Car);
            var result = CarCamTools.FinalizeShot("A", 6f);
            StringAssert.StartsWith(Failure, result);
            StringAssert.Contains("CarCam.BuildPath", result);
        }

        [Test]
        public void FullFlow_BuildsShotAndTimeline()
        {
            StringAssert.StartsWith(Success, CarCamTools.SetEndpoints("WalkUp", Start, End, Center, carName: Car));
            var build = CarCamTools.BuildPath("WalkUp", Mid());
            StringAssert.StartsWith(Success, build);
            StringAssert.Contains("CarCam.FinalizeShot", build);
            var fin = CarCamTools.FinalizeShot("WalkUp", 6f);
            StringAssert.StartsWith(Success, fin);
            StringAssert.Contains("duration 6 s", fin);

            var shot = CarCamSessions.Find("WalkUp");
            Assert.AreEqual(ShotState.Finalized, shot.state);
            Assert.IsNotNull(shot.vcam.GetComponent<FixedLookBlend>());
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>($"Assets/{TestTimeline}.playable");
            var clip = timeline.GetOutputTracks().OfType<CinemachineTrack>().Single().GetClips().Single();
            Assert.AreEqual("WalkUp", clip.displayName);
        }

        [Test]
        public void BuildPath_Obstacle_ReturnsValidationFailed_ThenAskUserAfterThreeAttempts()
        {
            CarCamTestScene.CreatePillar(new Vector3(0f, 1.5f, 6.5f));
            CarCamTools.SetEndpoints("A", Start, End, Center, carName: Car);

            var first = CarCamTools.BuildPath("A", Mid());
            StringAssert.StartsWith("[[PROMPTRETURN]] VALIDATION_FAILED (attempt 1/3)", first);
            StringAssert.Contains("CLIPPING with Pillar", first);
            StringAssert.Contains("You MAY fix this yourself", first);

            CarCamTools.BuildPath("A", Mid());
            var third = CarCamTools.BuildPath("A", Mid());
            StringAssert.StartsWith("[[PROMPTRETURN]] VALIDATION_FAILED (attempt 3/3)", third);
            StringAssert.Contains("Stop all tool calls", third);
            Assert.AreEqual(ShotState.EndpointsSet, CarCamSessions.Find("A").state);
        }

        [Test]
        public void SetEndpoints_Again_ResetsAttemptsAndPath()
        {
            CarCamTestScene.CreatePillar(new Vector3(0f, 1.5f, 6.5f));
            CarCamTools.SetEndpoints("A", Start, End, Center, carName: Car);
            CarCamTools.BuildPath("A", Mid());
            CarCamTools.SetEndpoints("A", Start, End, Center, carName: Car);
            var shot = CarCamSessions.Find("A");
            Assert.AreEqual(0, shot.validationAttempts);
            Assert.IsNull(shot.spline);
            Assert.AreEqual(1, Object.FindObjectsByType<CarCamShot>().Length);
        }

        [Test]
        public void FinalizeShot_Twice_KeepsSingleClip()
        {
            CarCamTools.SetEndpoints("A", Start, End, Center, carName: Car);
            CarCamTools.BuildPath("A", Mid());
            CarCamTools.FinalizeShot("A", 6f);
            StringAssert.StartsWith(Success, CarCamTools.FinalizeShot("A", 8f, Easing.EaseOut));
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>($"Assets/{TestTimeline}.playable");
            var clips = timeline.GetOutputTracks().OfType<CinemachineTrack>().Single().GetClips().ToList();
            Assert.AreEqual(1, clips.Count);
            Assert.AreEqual(8d, clips[0].duration, 1e-6);
        }
    }
}
```

- [ ] **Step 2: Tests laufen lassen – müssen fehlschlagen**

Erwartet: Compile-Fehler (`CarCamTools`, `CarCamSessions` nicht definiert).

- [ ] **Step 3: `CarCamSessions` implementieren**

`Assets/Editor/CarCam/CarCamSessions.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CarCam
{
    /// <summary>Finds and creates CarCamShot sessions in the open scene and resolves the car.</summary>
    public static class CarCamSessions
    {
        public const string DefaultCarName = "BlockoutCar";

        public static CarCamShot Find(string shotName) =>
            Object.FindObjectsByType<CarCamShot>().FirstOrDefault(s => s.ShotName == shotName);

        public static IEnumerable<string> Names() =>
            Object.FindObjectsByType<CarCamShot>().Select(s => s.ShotName);

        public static CarCamShot Create(string shotName)
        {
            var go = new GameObject(CarCamShot.ObjectPrefix + shotName);
            Undo.RegisterCreatedObjectUndo(go, "Create CarCam Shot");
            return go.AddComponent<CarCamShot>();
        }

        public static bool TryGetCarFrame(string carName, out CarFrame frame, out string error)
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
    }
}
```

- [ ] **Step 4: `CarCamTools` implementieren**

`Assets/Editor/CarCam/CarCamTools.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.AI.Assistant.FunctionCalling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static System.FormattableString;

namespace CarCam
{
    /// <summary>The four AI Assistant tools of the car showroom shot workflow. Thin: validate parameters, load the session, delegate.</summary>
    public static class CarCamTools
    {
        public const int MaxAutoAttempts = 3;

        internal static string TimelineName = ShotBuilder.DefaultTimelineName;

        [AgentTool("Step 1 of the car showroom shot workflow. Returns the car's size, the floor height and all named car parts with their " +
                   "car-relative position (azimuthDeg / distance / height), so you can translate the user's shot description into CamPoints " +
                   "and LookTargets. Call this before CarCam.SetEndpoints.", "CarCam.GetCarContext")]
        public static string GetCarContext(
            [ToolParameter("Name of the car GameObject in the open scene.")] string carName = CarCamSessions.DefaultCarName)
            => Run("CarCam GetCarContext", () =>
            {
                if (!CarCamSessions.TryGetCarFrame(carName, out var frame, out var error))
                {
                    return BridgeProtocol.Failure(error);
                }

                var sb = new StringBuilder();
                sb.AppendLine(Invariant($"Car '{carName}': length {frame.Size.z:0.00} m, width {frame.Size.x:0.00} m, height {frame.Size.y:0.00} m. Floor at world y {frame.FloorY:0.00}."));
                sb.AppendLine("CamPoint: azimuthDeg 0 = front, 90 = right side, 180 = rear, -90 = left side; distance = horizontal meters from the car center; height = meters above the floor.");
                sb.AppendLine("Parts (use as LookTarget.part, or 'Center'):");
                foreach (var partName in frame.PartNames())
                {
                    var p = frame.ToCamPoint(CarFrame.PartBounds(frame.FindPart(partName)).center);
                    sb.AppendLine(Invariant($"- {partName}: az {p.azimuthDeg:0.0}°, d {p.distance:0.00} m, h {p.height:0.00} m"));
                }
                var missing = CarColliderScope.CountMissing(frame.Car);
                sb.AppendLine(missing == 0
                    ? "Colliders: all mesh parts have colliders."
                    : $"Colliders: {missing} mesh parts have no collider; they are added temporarily during path validation.");
                sb.Append("Next: call CarCam.SetEndpoints.");
                return BridgeProtocol.Success(sb.ToString());
            });

        [AgentTool("Step 2 of the car showroom shot workflow. Defines (or redefines) the start and end camera position of a shot in car-relative " +
                   "coordinates plus the look target at the END. The look direction is fixed in advance and rotates at most 10° over the whole shot; " +
                   "a startLookTarget further away is clamped automatically and reported. Resets any previously built path of this shot.", "CarCam.SetEndpoints")]
        public static string SetEndpoints(
            [ToolParameter("Unique shot name, e.g. 'WalkUp'. Reusing a name redefines that shot.")] string shotName,
            [ToolParameter("Start camera position: azimuthDeg (0 front, 90 right, 180 rear, -90 left), distance (meters from the car center, > 0), height (meters above the floor).")] CamPoint start,
            [ToolParameter("End camera position, same format as start.")] CamPoint end,
            [ToolParameter("What the camera looks at at the END of the shot: part = a part name from CarCam.GetCarContext or 'Center'; heightOffset in meters.")] LookTarget endLookTarget,
            [ToolParameter("Optional: what the camera looks at at the START. Omit it to keep the end look direction for the whole shot.")] LookTarget startLookTarget = null,
            [ToolParameter("Vertical field of view in degrees (default 35 = slightly long lens, showroom look).")] float fov = 35f,
            [ToolParameter("Name of the car GameObject in the open scene.")] string carName = CarCamSessions.DefaultCarName)
            => Run("CarCam SetEndpoints", () =>
            {
                if (string.IsNullOrWhiteSpace(shotName)) return BridgeProtocol.Failure("shotName must not be empty.");
                if (start.distance <= 0f || end.distance <= 0f) return BridgeProtocol.Failure("start.distance and end.distance must be > 0.");
                if (fov <= 1f || fov >= 179f) return BridgeProtocol.Failure("fov must be between 1 and 179 degrees.");
                if (endLookTarget == null) return BridgeProtocol.Failure("endLookTarget is required.");
                if (!CarCamSessions.TryGetCarFrame(carName, out var frame, out var error)) return BridgeProtocol.Failure(error);
                if (!frame.TryResolveLookPoint(endLookTarget, out var endLookPoint)) return UnknownPart(frame, endLookTarget.part);
                var startLookPoint = endLookPoint;
                if (startLookTarget != null && !frame.TryResolveLookPoint(startLookTarget, out startLookPoint)) return UnknownPart(frame, startLookTarget.part);

                var startPos = frame.ToWorld(start);
                var endPos = frame.ToWorld(end);
                var endRot = CarFrame.LookAt(endPos, endLookPoint);
                var desiredStart = startLookTarget != null ? CarFrame.LookAt(startPos, startLookPoint) : endRot;
                var startRot = CarFrame.ClampStartRotation(endRot, desiredStart, CarFrame.MaxLookRotationDeg, out var requested);

                var shot = CarCamSessions.Find(shotName) ?? CarCamSessions.Create(shotName);
                Undo.RecordObject(shot, "CarCam SetEndpoints");
                if (shot.spline != null)
                {
                    Undo.DestroyObjectImmediate(shot.spline.gameObject);
                    shot.spline = null;
                }
                shot.car = frame.Car;
                shot.start = start;
                shot.end = end;
                shot.hasStartLookTarget = startLookTarget != null;
                shot.startLookTarget = startLookTarget ?? new LookTarget();
                shot.endLookTarget = endLookTarget;
                shot.fov = fov;
                shot.startRotation = startRot;
                shot.endRotation = endRot;
                shot.startLookPoint = startLookPoint;
                shot.endLookPoint = endLookPoint;
                shot.intermediatePoints.Clear();
                shot.state = ShotState.EndpointsSet;
                shot.validationAttempts = 0;
                shot.lastReport = string.Empty;
                EditorUtility.SetDirty(shot);
                EditorSceneManager.MarkSceneDirty(shot.gameObject.scene);

                var sb = new StringBuilder();
                sb.Append(Invariant($"Shot '{shotName}' endpoints set: start {start} → end {end}, end look target {endLookTarget}, fov {fov:0.#}°. "));
                sb.Append(Invariant($"Look rotation over the shot: {Quaternion.Angle(startRot, endRot):0.0}°"));
                if (requested > CarFrame.MaxLookRotationDeg + 0.01f)
                {
                    sb.Append(Invariant($" (requested {requested:0.0}°, clamped to {CarFrame.MaxLookRotationDeg:0}°). The start look target will not be centered; if you chose the endpoints yourself, move the start point so it faces the end look direction and call CarCam.SetEndpoints again; if the user chose them, tell the user"));
                }
                sb.AppendLine(".");
                sb.Append("Next: call CarCam.BuildPath with 0..n intermediate CamPoints.");
                return BridgeProtocol.Success(sb.ToString());
            });

        [AgentTool("Step 3 of the car showroom shot workflow. Builds the camera spline start → intermediate points → end and validates every pose " +
                   "from t = 0 to 1 (clipping, floor clearance, distance to the car, look target visible, no kinks). Returns SUCCESS or " +
                   "VALIDATION_FAILED with the problematic t ranges and the nearest point to fix.", "CarCam.BuildPath")]
        public static string BuildPath(
            [ToolParameter("Name of a shot defined with CarCam.SetEndpoints.")] string shotName,
            [ToolParameter("Ordered intermediate CamPoints between start and end (may be empty). They shape the move, e.g. a slight rise and fall.")] List<CamPoint> intermediatePoints)
            => Run("CarCam BuildPath", () =>
            {
                var shot = CarCamSessions.Find(shotName);
                if (shot == null) return UnknownShot(shotName);
                intermediatePoints ??= new List<CamPoint>();
                if (intermediatePoints.Any(p => p.distance <= 0f)) return BridgeProtocol.Failure("Every intermediate point needs distance > 0.");
                if (!CarFrame.TryCreate(shot.car, out var frame)) return BridgeProtocol.Failure("The shot's car is missing or has no Renderers. Call CarCam.SetEndpoints again.");

                var world = new List<Vector3> { frame.ToWorld(shot.start) };
                world.AddRange(intermediatePoints.Select(p => frame.ToWorld(p)));
                world.Add(frame.ToWorld(shot.end));

                Undo.RecordObject(shot, "CarCam BuildPath");
                var wasFinalized = shot.state == ShotState.Finalized;
                var container = ShotBuilder.RebuildSpline(shot, world);
                shot.intermediatePoints = new List<CamPoint>(intermediatePoints);

                PathReport report;
                using (new CarColliderScope(shot.car))
                {
                    report = PathValidator.Validate(new PathInput
                    {
                        Spline = container,
                        Car = shot.car,
                        StartRotation = shot.startRotation,
                        EndRotation = shot.endRotation,
                        StartLookPoint = shot.startLookPoint,
                        EndLookPoint = shot.endLookPoint,
                        Fov = shot.fov,
                        FloorY = frame.FloorY
                    }, PathValidationSettings.Default);
                }
                shot.lastReport = report.ToText();

                if (report.IsValid)
                {
                    shot.state = ShotState.PathValid;
                    shot.validationAttempts = 0;
                    EditorUtility.SetDirty(shot);
                    return BridgeProtocol.Success(report.ToText() + (wasFinalized
                        ? "\nThe path changed: call CarCam.FinalizeShot again to update the camera and timeline."
                        : "\nNext: call CarCam.FinalizeShot."));
                }

                shot.state = ShotState.EndpointsSet;
                shot.validationAttempts++;
                EditorUtility.SetDirty(shot);
                return BridgeProtocol.ValidationFailed(report.ToText(), shot.validationAttempts, MaxAutoAttempts);
            });

        [AgentTool("Step 4 of the car showroom shot workflow. Puts a Cinemachine camera on the validated path (fixed Assetto-Corsa-style look, " +
                   "no target tracking) and adds the shot to the scene timeline with the given duration and easing. Calling it again for the " +
                   "same shot replaces its timeline clips.", "CarCam.FinalizeShot")]
        public static string FinalizeShot(
            [ToolParameter("Name of a shot whose path was validated by CarCam.BuildPath.")] string shotName,
            [ToolParameter("Shot length in seconds (> 0). Showroom moves are usually 5-10 s.")] float duration,
            [ToolParameter("Speed profile along the path. EaseInOut (default) starts and ends softly.")] Easing easing = Easing.EaseInOut)
            => Run("CarCam FinalizeShot", () =>
            {
                if (duration <= 0f) return BridgeProtocol.Failure("duration must be > 0.");
                var shot = CarCamSessions.Find(shotName);
                if (shot == null) return UnknownShot(shotName);
                if (shot.state == ShotState.EndpointsSet || shot.spline == null)
                {
                    return BridgeProtocol.Failure($"Shot '{shotName}' has no validated path. Call CarCam.BuildPath until it returns SUCCESS.");
                }

                var result = ShotBuilder.Finalize(shot, duration, easing, TimelineName);
                Undo.RecordObject(shot, "CarCam FinalizeShot");
                shot.state = ShotState.Finalized;
                EditorUtility.SetDirty(shot);

                return BridgeProtocol.Success(Invariant(
                    $"Shot '{shotName}' built: camera '{result.CameraName}', timeline '{result.TimelineName}' (director '{result.DirectorName}'), " +
                    $"start {result.Start:0.##} s, duration {result.Duration:0.##} s, easing {easing}.\n" +
                    $"Tell the user: open Window > Sequencing > Timeline, select '{result.DirectorName}' and press Play to preview."));
            });

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

        static string UnknownPart(CarFrame frame, string part) =>
            BridgeProtocol.Failure($"Unknown part '{part}'. Valid parts: {LookTarget.Center}, {string.Join(", ", frame.PartNames())}.");

        static string UnknownShot(string shotName)
        {
            var names = CarCamSessions.Names().ToList();
            return BridgeProtocol.Failure($"No shot named '{shotName}'. Existing shots: {(names.Count == 0 ? "none" : string.Join(", ", names))}. Call CarCam.SetEndpoints first.");
        }
    }
}
```

- [ ] **Step 5: Tests laufen lassen – müssen passen**

Erwartet: alle `CarCamToolsTests` PASS und alle früheren Tests weiterhin PASS (Filter `CarCam`).
Falls `SetEndpoints_ClampsLargeStartRotation` nicht klemmt (gewünschter Winkel ≤ 10°): `startLookTarget` auf `new LookTarget("Headlight_L")` und Start auf `new CamPoint(70f, 6f, 1.2f)` ändern – Ziel des Tests ist ein gewünschter Winkel deutlich > 10°.

---

### Task 8: Skill `car-showroom-shot` und manuelle Abnahme

**Files:**
- Create: `Assets/AI_Instructions/Skills/car-showroom-shot/SKILL.md`
- Create: `Assets/AI_Instructions/Skills/car-showroom-shot/references/translation-guide.md`

**Interfaces:**
- Consumes: Tool-IDs und Parameter-Namen aus Task 7 (`shotName`, `start`, `end`, `endLookTarget`, `startLookTarget`, `fov`, `carName`, `intermediatePoints`, `duration`, `easing`; `CamPoint.azimuthDeg/distance/height`; `LookTarget.part/heightOffset`).

`ProjectGuidelines.txt` bleibt unverändert und wird vom Skill nicht referenziert.

- [ ] **Step 1: `SKILL.md` anlegen**

`Assets/AI_Instructions/Skills/car-showroom-shot/SKILL.md`:
````markdown
---
name: car-showroom-shot
description: Builds ONE cinematic camera shot (Kamerafahrt) around the car in the open scene from the user's description, in Assetto Corsa showroom style (slow, calm, the camera already knows where it will look and turns at most 10°). Activate when the user asks for a camera move, camera shot, camera path, Kamerafahrt or cinematic around the car.
required_packages:
  com.unity.cinemachine: ">=3.0.0"
  com.unity.timeline: ">=1.8.0"
tools:
  - CarCam.GetCarContext
  - CarCam.SetEndpoints
  - CarCam.BuildPath
  - CarCam.FinalizeShot
---

# Car Showroom Shot

Build exactly ONE camera shot around the car that matches the user's description as closely as possible.
Style reference: the Assetto Corsa showroom camera. Calm, slow, cinematic moves. The camera knows from the start
where it will look at the end and only turns slightly (max 10° over the whole shot) to adjust.

## Workflow – strict order, exactly one tool call at a time

1. `CarCam.GetCarContext` – read the car size, the floor and the part positions.
2. Translate the prompt into a shot plan using `references/translation-guide.md`:
   shot name, start CamPoint, end CamPoint, end LookTarget (optional start LookTarget), fov, intermediate points, duration, easing.
   If the user gave no start or end, choose them yourself from the description.
   Tell the user the plan in 1–3 plain sentences, then continue.
3. `CarCam.SetEndpoints`. Read the reported look rotation.
   - Clamped and YOU chose the endpoints → adjust them so the rotation stays ≤ 10°, call `CarCam.SetEndpoints` again.
   - Clamped and the USER specified the endpoints → tell the user and ask whether to keep it.
4. `CarCam.BuildPath` with the intermediate points.
5. `CarCam.FinalizeShot` with duration and easing.
6. Tell the user what was built and how to preview it (the result of FinalizeShot says how).

## Rules

- Use ONLY the four CarCam tools for this workflow. Never write C# scripts, never call other bridge methods, never edit scene objects by other means.
- Never use world coordinates. Only CamPoints and part names returned by `CarCam.GetCarContext` (or `Center`).
- Decide the END look target first. The whole shot must stay within 10° of rotation.
- Cinematic means: slow, smooth, no sudden direction changes. Prefer 5–10 s, `EaseInOut`, 0–4 intermediate points.
- One shot per request. To change an existing shot, reuse its shotName.

## Tool results

- `[[PROMPTRETURN]] SUCCESS` → continue with the step named in the result.
- `[[PROMPTRETURN]] VALIDATION_FAILED` → follow the embedded instruction. While attempts remain you may fix the path yourself:
  move, add or remove intermediate points near the reported t ranges (t 0 = start, 1 = end; "nearest point" names the point).
  Do not change the endpoints without asking the user. See "Fixing validation problems" in the translation guide.
- `[[PROMPTRETURN]] FAILURE` → stop. Show the reason to the user, propose 2–3 numbered options, wait for their pick.
- `[[PROMPTRETURN]] AWAITING_INPUT` → stop. Show the question and options, wait for their pick.
````

- [ ] **Step 2: `translation-guide.md` anlegen**

`Assets/AI_Instructions/Skills/car-showroom-shot/references/translation-guide.md`:
````markdown
# Translating a shot description into CamPoints

## Coordinates

- `azimuthDeg`: 0 = in front of the car, 90 = right side, 180 = behind, -90 = left side. 45 = front-right corner, 135 = rear-right corner.
- `distance`: horizontal meters from the car CENTER (not from the surface). Half the car length is roughly the front/rear surface,
  half the width the side surface. Always stay ≥ 0.3 m outside the surface.
- `height`: meters above the floor.
- Part positions from `CarCam.GetCarContext` are in the same format – use them to place the camera near a part
  (e.g. a headlight close-up: same azimuth as the headlight, distance = headlight distance + 0.6–1.2 m).

## Typical values

| Description | height | distance |
|---|---|---|
| Ground level / very low | 0.3–0.5 | – |
| Hero / bumper height | 0.6–0.9 | – |
| Eye level, "walking" | 1.5–1.7 | – |
| Elevated / crane | 2.5–4 | – |
| Close-up | – | surface + 0.5–1.2 |
| Medium | – | surface + 2–3.5 |
| Wide / establishing | – | surface + 5–8 |

Default fov 35 (slightly long lens). Wider (45–55) for tight spaces or dramatic close walk-ups, longer (20–28) for compressed hero shots.

## The 10° look rule – how to plan for it

The look direction at the end is fixed by `endLookTarget`. Along the path the orientation blends from the start rotation to the end rotation,
and the two may differ by at most 10°. So the direction from the camera to what it looks at must stay nearly constant:

- Yaw: the change in azimuth of the look target as seen from the camera stays small.
- Pitch = atan((cameraHeight − targetHeight) / horizontalDistanceToTarget). Keep the pitch change small:
  if the camera rises, it must also move further away.
- Orbits around a car-center target change the yaw by the azimuth change → at most ~10° of azimuth. For bigger lateral moves use a parallel pass.
- Only set `startLookTarget` when the user describes a shift of attention; it is clamped to 10° anyway.

## Patterns

**Walk up to the car ("geh auf das Auto zu", camera bobs cinematically)**
- start: az A, distance = surface + 6, height 1.6 · end: same az A (±5), distance = surface + 1.5, height 1.5
- intermediates: 2–3 points evenly between them, heights alternating ±0.05–0.12 m around 1.6 (gentle bob, not bouncy)
- endLookTarget: Center or the part the user walks towards · duration 6–8 s · EaseInOut

**Low slow pass along the side ("tiefe, langsame Fahrt an der Seite entlang")**
- camera moves PARALLEL to the car at a constant lateral offset (e.g. 2 m outside the side surface), height 0.4–0.6
- start beside the front wheel, end beside the rear wheel (use the wheel parts' azimuths from the context; for the right side
  start az ≈ 60–70, end az ≈ 110–120 at the same lateral offset – compute distance = sqrt(lateral² + forwardOffset²))
- startLookTarget = front wheel, endLookTarget = rear wheel → the look direction stays perpendicular to the car (≈ 0° rotation)
- intermediates: none (a straight line is the parallel pass) · duration 7–10 s · EaseInOut

**Rise from the rear, looking at the car ("vom Heck nach oben steigen, am Ende Blick auf die Front")**
- start: az 180, distance = surface + 3, height 1.0 · end: az 180, distance = surface + 6, height 2.6 (moving back keeps the pitch change small)
- endLookTarget: Center (or a front part with heightOffset) · 1 intermediate point halfway, slightly above the straight line
- duration 6–8 s · EaseInOut

**Detail reveal (e.g. headlight)**
- start and end both near the part (distance = part distance + 0.8…1.5), sliding sideways by 0.5–1 m at constant height
- endLookTarget: the part · duration 4–6 s · EaseOut

## Fixing validation problems

| Report | Fix |
|---|---|
| `CLIPPING with X` | Move the nearest point away from X (more distance or height), or add a point that routes around X. |
| `BELOW_MIN_HEIGHT` | Raise the nearest point(s) to ≥ 0.3 m. If the dip is between points (spline overshoot), add a point there with a higher height. |
| `TOO_CLOSE_TO_CAR` | Increase the distance of the nearest point by 0.3–0.5 m. |
| `TARGET_NOT_VISIBLE … off-axis` | The camera left the look direction: bring the nearest point back in line with the look direction (see the 10° rule). If the endpoints cause it, ask the user. |
| `TARGET_NOT_VISIBLE occluded by X` | Move the nearest point so X is no longer between camera and target (higher or sideways). |
| `KINK` | Remove the point that makes the path go back and forth, or spread the points more evenly along the path. |
````

- [ ] **Step 3: Kompilieren + komplette Test-Suite**

Erwartet: keine `error CS`; alle Tests mit Filter `CarCam` PASS.

- [ ] **Step 4: Manuelle Abnahme im AI Assistant (mit dem User)**

Den User bitten:
1. Unity öffnen, `SampleScene` laden, unter **Unity > Settings > AI > Skills** den Project-Skill `car-showroom-shot` auf **Allow** setzen.
2. Im Assistant nacheinander eingeben:
   - „Geh auf das Auto zu, die Kamera wippt dabei leicht cinematisch.“
   - „Eine tiefe, langsame Fahrt an der rechten Seite entlang.“
   - „Steig vom Heck nach oben, am Ende Blick auf die Front.“
3. Pro Prompt prüfen: Nur die vier CarCam-Tools wurden aufgerufen; ein `CarCamShot_<name>`-Objekt existiert; in der Timeline (`CarCamTimeline_Director`) läuft der Shot ohne Clipping; die Kamera dreht sich kaum; die Bewegung passt zum Prompt.

Ergebnisse (auch Abweichungen) an den User berichten. Nichts committen.
