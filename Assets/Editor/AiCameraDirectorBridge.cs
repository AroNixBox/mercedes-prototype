using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Splines;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

public static class AiCameraDirectorBridge
{
    static bool TryParseGid(string gidString, out GlobalObjectId gid)
    {
        if (string.IsNullOrEmpty(gidString))
        {
            gid = default;
            return false;
        }
        string cleanGid = gidString.Trim('[', ']', ' ');
        return GlobalObjectId.TryParse(cleanGid, out gid);
    }

    /// <returns>Success or Failure</returns>
    public static string TryCreateVCam(string name, string targetGlobalIdString, out string vCamGlobalId, out string brainGlobalId)
    {
        vCamGlobalId = string.Empty;
        brainGlobalId = string.Empty;
        var skipValidation = name.Contains("_OVERRIDE");
        if (skipValidation)
        {
            name = name.Replace("_OVERRIDE", "");                                                                                                                                        
        }
        
        // 1. query the currently opened scene if theres a vcam somewhere that already has the same name..
        var vCams = Object.FindObjectsByType<CinemachineCamera>();
        if (vCams.Any(e => e.name == name) && !skipValidation)
        {
            // already existing vcam found (same name)
            return BridgeProtocol.AwaitingInput("A VCam with the name '" + name + "' already exists. Override it?",
                "Call TryCreateVCam again with the name + '_OVERRIDE'",
                "Stop and do nothing");
        }

        if (!TryParseGid(targetGlobalIdString, out var targetGid))
        {
            return BridgeProtocol.Failure("the passed in targetGlobalId is not parsable: " + targetGlobalIdString);
        }
        
        var targetObj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(targetGid);
        
        Transform target = null;
        if (targetObj is GameObject go)
        {
            target = go.transform;
        }
        else if (targetObj is Component comp)
        {
            target = comp.transform;
        }

        if (target == null)
        {
            return BridgeProtocol.Failure("Could not resolve a Transform from the provided targetGlobalId. Make sure the target GameObject exists in the currently open scene.");
        }

        return CreateVCam(name, target, out vCamGlobalId, out brainGlobalId);
    }

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

    static void AddRotationTarget(Transform vCamTr)
    {
        var rotationComposer = vCamTr.gameObject.AddComponent<CinemachineRotationComposer>();
        rotationComposer.CenterOnActivate = true;
        rotationComposer.Damping = new Vector2(.5f, .5f);
        rotationComposer.Composition.HardLimits.Enabled = true;
        rotationComposer.Composition.HardLimits.Size = new Vector2(0.8f, 0.8f);
        // TODO: Can edit settings on rotation compose
    }

    // TODO: Could add closed option
    public static string TryCreateSpline(string name, List<Vector3> positionsInOrder, out string splineContainerGlobalId)
    {
        splineContainerGlobalId = string.Empty;
        var skipValidation = name.Contains("_OVERRIDE");
        if (skipValidation)
        {
            name = name.Replace("_OVERRIDE", "");                                                                                                                                        
        }
        
        // query the currently opened scene if theres a vcam somewhere that already has the same name..
        var splines = Object.FindObjectsByType<SplineContainer>();
        if (splines.Any(e => e.name == name) && !skipValidation)
        {
            // already existing vcam found (same name)
            return BridgeProtocol.AwaitingInput("A Spline with the name '" + name + "' already exists. Override it?",
                "Call TryCreateSpline again with the name + '_OVERRIDE'",
                "Stop and do nothing");
        }

        return CreateSpline(name, positionsInOrder, out splineContainerGlobalId);
    }

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

    public static string AddCameraDollyToSpline(string containerGlobalIdString, string vCamGlobalIdString)
    {
        if (!TryParseGid(containerGlobalIdString, out var containerGid))
        {
            return BridgeProtocol.Failure("The Global Object ID for the container you provided is not parsable.");
        }
        var container = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(containerGid) as SplineContainer;
        if (container == null)
        {
            return BridgeProtocol.Failure("The SplineContainer from that GID is null.");
        }

        if (!TryParseGid(vCamGlobalIdString, out var vCamGid))
        {
            return BridgeProtocol.Failure("The Global Object ID for the VCam you provided is not parsable.");
        }

        var vCam = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(vCamGid) as CinemachineCamera;
        if (vCam == null)
        {
            return BridgeProtocol.Failure("The CinemachineCamera from that GID is null.");
        }

        var dolly = vCam.gameObject.AddComponent<CinemachineSplineDolly>();
        dolly.Spline = container;
        
        return BridgeProtocol.SUCCESS;
    }
}