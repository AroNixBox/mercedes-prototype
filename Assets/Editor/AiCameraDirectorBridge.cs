using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Splines;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

public static class AiCameraDirectorBridge
{
    /// <param name="vCam">Ref to the created CinemachineCamera</param>
    /// <returns>Success or Failure</returns>
    public static string TryCreateVCam(string name, Transform target, out CinemachineCamera vCam)
    {
        vCam = null;
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
            return "[[PROMPTRETURN]] Ask the user: Override it? " +                                                         
                   "If yes: call TryCreateVCam again with the name + '_OVERRIDE'. " +                     
                   "If no: stop and do nothing.";
        }

        return CreateVCam(name, target, out vCam);
    }

    static string CreateVCam(string name, Transform target, out CinemachineCamera vCam)
    {
        var mainCam = Camera.main;
        if (mainCam == null)
        {
            var mainCamObj = new GameObject
            {
                name = "Main Camera"
            };
            mainCam = mainCamObj.AddComponent<Camera>();
        }

        if (mainCam.GetComponent<CinemachineBrain>() == null)
        {
            mainCam.AddComponent<CinemachineBrain>();
        }
        
        var vCamGo = new GameObject
        {
            name = name,
        };

        vCam = vCamGo.AddComponent<CinemachineCamera>();
        vCam.Target.TrackingTarget = target;
        return "[[PROMPTRETURN]] Success";
    }

    // TODO: Could add closed option
    public static string TryCreateSpline(string name, List<Vector3> positionsInOrder, out SplineContainer splineContainer)
    {
        splineContainer = null;
        var skipValidation = name.Contains("_OVERRIDE");
        if (skipValidation)
        {
            name = name.Replace("_OVERRIDE", "");                                                                                                                                        
        }
        
        // 1. query the currently opened scene if theres a vcam somewhere that already has the same name..
        var splines = Object.FindObjectsByType<SplineContainer>();
        if (splines.Any(e => e.name == name) && !skipValidation)
        {
            // already existing vcam found (same name)
            return "[[PROMPTRETURN]] Ask the user: Override it? " +                                                         
                   "If yes: call TryCreateSpline again with the name + '_OVERRIDE'. " +                     
                   "If no: stop and do nothing.";
        }

        return CreateSpline(name, positionsInOrder, out splineContainer);
    }

    static string CreateSpline(string name, List<Vector3> positionsInOrder, out SplineContainer splineContainer)
    {
        splineContainer = null;
        if (positionsInOrder is { Count: < 1 })
        {
            return "[[PROMPTRETURN]] Failure: You tried creting a spline without knots";
        }

        var splineGo = new GameObject
        {
            name = name
        };
        splineContainer = splineGo.AddComponent<SplineContainer>();
        var spline = splineContainer.Spline; // TODO: Relevant for closedloop if wanted
        foreach (var curPosition in positionsInOrder)
        {
            spline.Add(new BezierKnot(curPosition));
        }

        return "[[PROMPTRETURN]] Success";
    }

    public static string AddCameraDollyToSpline(SplineContainer container, CinemachineCamera vCam)
    {
        if (container == null)
        {
            return "[[PROMPTRETURN]] Failure: The SplineContainer you provided is null";
        }
        if (vCam == null)
        {
            return "[[PROMPTRETURN]] Failure: The CinemachineCamera you provided is null";
        }

        var dolly = vCam.gameObject.AddComponent<CinemachineSplineDolly>();
        dolly.Spline = container;
        
        return "[[PROMPTRETURN]] Success";
    }
}
