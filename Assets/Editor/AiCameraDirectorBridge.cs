using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Splines;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
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
            return "[[PROMPTRETURN]] AWAITING_INPUT: Override it? " +                                                         
                   "OPTION_1: call TryCreateVCam again with the name + '_OVERRIDE'. " +                     
                   "OPTION_2: stop and do nothing.";
        }

        if (!TryParseGid(targetGlobalIdString, out var targetGid))
        {
            return "[[PROMPTRETURN]] FAILURE: the passed in targetgid is not parsable: " + targetGlobalIdString;
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
            return "[[PROMPTRETURN]] FAILURE: Could not resolve a Transform from the provided targetGlobalId. Make sure the target GameObject exists in the currently open scene.";
        }

        return CreateVCam(name, target, out vCamGlobalId, out brainGlobalId);
    }

    static string CreateVCam(string name, Transform target, out string vCamGlobalId, out string brainGlobalId)
    {
        var mainCam = Camera.main;
        if (mainCam == null)
        {
            var mainCamObj = new GameObject { name = "Main Camera" };
            mainCam = mainCamObj.AddComponent<Camera>();
        }

        var brain = mainCam.GetComponent<CinemachineBrain>();
        if (brain == null)
        {
            brain = mainCam.AddComponent<CinemachineBrain>();
        }

        brainGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(brain).ToString();

        var vCamGo = new GameObject { name = name };
        var vCam = vCamGo.AddComponent<CinemachineCamera>();
        vCam.Target.TrackingTarget = target;
        AddRotationTarget(vCam.transform);
        vCamGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(vCam).ToString();

        EditorUtility.SetDirty(vCam);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(vCamGo.scene);

        return "[[PROMPTRETURN]] SUCCESS";
    }

    static void AddRotationTarget(Transform vCamTr)
    {
        var rotationComposer = vCamTr.AddComponent<CinemachineRotationComposer>();
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
            return "[[PROMPTRETURN]] Ask the user: Override it? " +                                                         
                   "If yes: call TryCreateSpline again with the name + '_OVERRIDE'. " +                     
                   "If no: stop and do nothing.";
        }

        return CreateSpline(name, positionsInOrder, out splineContainerGlobalId);
    }

    static string CreateSpline(string name, List<Vector3> positionsInOrder, out string splineContainerGlobalId)
    {
        splineContainerGlobalId = string.Empty;
        if (positionsInOrder is { Count: < 1 })
        {
            return "[[PROMPTRETURN]] Failure: You tried creting a spline without knots";
        }

        var splineGo = new GameObject
        {
            name = name
        };
        var splineContainer = splineGo.AddComponent<SplineContainer>();
        var spline = splineContainer.Spline;
        foreach (var curPosition in positionsInOrder)
        {
            var knot = new BezierKnot(curPosition);
            spline.Add(knot);
        }
        
        // Glatte Tangenten berechnen
        for (int i = 0; i < spline.Knots.Count(); i++)
        {
            spline.SetTangentMode(i, TangentMode.AutoSmooth);
        }
        splineContainerGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(splineContainer).ToString();
        return "[[PROMPTRETURN]] Success";
    }

    public static string AddCameraDollyToSpline(string containerGlobalIdString, string vCamGlobalIdString)
    {
        if (!TryParseGid(containerGlobalIdString, out var containerGid))
        {
            return "[[PROMPTRETURN]] Failure: The Global Object ID for the container you provided is not parsable";
        }
        var container = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(containerGid) as SplineContainer;
        if (container == null)
        {
            return "[[PROMPTRETURN]] Failure: The Container from that GID is null";
        }

        if (!TryParseGid(vCamGlobalIdString, out var vCamGid))
        {
            return "[[PROMPTRETURN]] Failure: The Global Object ID for the vcam you provided is not parsable";
        }

        var vCam = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(vCamGid) as CinemachineCamera;
        if (vCam == null)
        {
            return "[[PROMPTRETURN]] Failure: The CinemachineCamera from that GID is null";
        }

        var dolly = vCam.gameObject.AddComponent<CinemachineSplineDolly>();
        dolly.Spline = container;
        
        return "[[PROMPTRETURN]] Success";
    }
}
