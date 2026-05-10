using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using Unity.Cinemachine;
using UnityEditor;

// TODO: Rename in the md file instructions

// Assets are saved and returned as strings
// Scene GameObjects are returned as their Type
public static class TimelineAiBridge
{
    const string FolderPath = "Assets";
    const string Format = "playable";


    /// <param name="timelineAssetPath">the full path to the asset</param>
    public static string TryCreateAsset(string name, out string timelineAssetPath)
    {
        timelineAssetPath = string.Empty;
        var skipValidation = name.Contains("_OVERRIDE");
        if (skipValidation)
        {
            name = name.Replace("_OVERRIDE", "");                                                                                                                                        
        }

        if (GetTimelineAsset(name, out _, out var fullPath) && !skipValidation)
        {
            // already existing timeline-asset found (same name)
            return "[[PROMPTRETURN]] AWAITING_INPUT: Override it? " +                                                         
                   "OPTION_1: call TryCreateAsset again with the name + '_OVERRIDE'. " +                     
                   "OPTION_2: stop and do nothing.";
        }
        
        return CreateAsset(fullPath, out timelineAssetPath);
    }
    static string CreateAsset(string fullPath, out string timelineAssetPath)
    {
        var timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timelineAsset, fullPath);
        AssetDatabase.SaveAssets();
        timelineAssetPath = fullPath;
        return "[[PROMPTRETURN]] SUCCESS";
    }
    static bool GetTimelineAsset(string name, out TimelineAsset timelineAsset, out string fullPath)
    {
        fullPath = $"{FolderPath}/{name}.{Format}";
        timelineAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(fullPath);
        return timelineAsset != null;
    }
    
    // TODO: Convert name to id, because there can be two gameobjects with the same name
    /// <returns>The name of the created playable director in the scene</returns>
    public static string CreatePlayableDirector(CinemachineCamera vcam, string timelinePath, out PlayableDirector director)
    {
        director = null;
        
        if (vcam == null)
        {
            // TODO: This should link the Create VCam Method, so we create one instead of aborting here
            return "[[PROMPTRETURN]] FAILURE: Vcam null";
        }
        
        // 2. create playable director
        var directorGO = new GameObject(vcam.name + "_Director");
        director = directorGO.AddComponent<PlayableDirector>();
        if (!GetTimelineAsset(timelinePath, out var timeline, out _))
        {
            // TODO: Maybe instruct to recall
            return "[[PROMPTRETURN]] FAILURE: No Timeline-Asset found under this path";
        }
        director.playableAsset = timeline;
        
        // TODO: Change to some sort of ID: Maybe EntityID?
        return "[[PROMPTRETURN]] Success";
    }
    
    // TODO: Move to camera bridge
    // TODO: Move this to cinemachine class instead:
    // var brain = Object.FindAnyObjectByType<CinemachineBrain>();
    //     if (brain == null)
    // {
    //     var mainCam = GameObject.FindWithTag("MainCamera");
    //     if (mainCam != null) brain = mainCam.AddComponent<CinemachineBrain>();
    // }

    // TODO: Instead should be accessed to 
    
    
    /// <param name="timelinePath">exact assetpath</param>
    public static string CreateCinemachineTrack(string timelinePath, CinemachineBrain brain, PlayableDirector director)
    {
        if (!GetTimelineAsset(timelinePath, out var timeline, out _))
        {
            // TODO: Maybe instruct to recall
            return "[[PROMPTRETURN]] FAILURE: No Timeline-Asset found under this path";
        }

        if (director == null)
        {
            return "[[PROMPTRETURN]] FAILURE: Null director passed to this method";
        }
        
        if (brain == null)
        {
            return "[[PROMPTRETURN]] FAILURE: Null brain passed to this method";
        }
        
        // TODO: More descriptive name so we can have multiple tracks
        var cmTrack = timeline.CreateTrack<CinemachineTrack>(null, "Cinemachine Track");
        // asset needs to be marked dirty and saved
        EditorUtility.SetDirty(timeline);
        
        // the created track needs a scene reference -> thats what we set here:
        director.SetGenericBinding(cmTrack, brain);
        // object change in scene only needs to be marked dirty, can be saved manually from user.
        EditorUtility.SetDirty(director); // if we dont do this, there is no change that will be saved, not even when the user saves manually
        AssetDatabase.SaveAssets();

        return "[[PROMPTRETURN]] SUCCESS";
    }

    public static string SetCinemachineTrack(string timelinePath, string trackName, CinemachineCamera assignedCamera, PlayableDirector director, float startTime, float duration)
    {
        if (!GetTimelineAsset(timelinePath, out var timeline, out _))
        {
            // TODO: Maybe instruct to recall
            return "[[PROMPTRETURN]] FAILURE: No Timeline-Asset found under this path";
        }

        var cmTrack = timeline.GetOutputTracks()
            .OfType<CinemachineTrack>()
            .FirstOrDefault(t => t.name == trackName);

        if (cmTrack == null)
        {
            return "[[PROMPTRETURN]] FAILURE: No CinemachineTrack found with that name, the track was not created before trying to access its";
        }

        if (duration < 0)
        {
            return "[[PROMPTRETURN]] FAILURE: You tried creating a cinemachine-clip with a negative duration";
        }
        if (startTime < 0)
        {
            return "[[PROMPTRETURN]] FAILURE: You tried creating a cinemachine-clip with a negative startTime";
        }
        
        var shotClip = cmTrack.CreateClip<CinemachineShot>();
        shotClip.start = startTime;
        shotClip.duration = duration;
        
        var shot = shotClip.asset as CinemachineShot;
        director.SetReferenceValue(shot!.VirtualCamera.exposedName, assignedCamera);
        
        EditorUtility.SetDirty(timeline);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        EditorUtility.SetDirty(director);
        AssetDatabase.SaveAssets();
        
        return "[[PROMPTRETURN]] SUCCESS";
    }
    
    /// <param name="timelinePath">exact assetpath</param>
    public static string CreateAnimationTrack(string timelinePath, Transform animatedObj, float startTime, float duration, PlayableDirector director)
    {
        if (!GetTimelineAsset(timelinePath, out var timeline, out _))
        {
            // TODO: Maybe instruct to recall
            return "[[PROMPTRETURN]] FAILURE: No Timeline-Asset found under this path";
        }

        if (director == null)
        {
            return "[[PROMPTRETURN]] FAILURE: Null director passed to this method";
        }

        if (animatedObj == null)
        {
            return "[[PROMPTRETURN]] FAILURE: Null 'animatedObj' passed to this method";
        }
        
        if (duration < 0)
        {
            return "[[PROMPTRETURN]] FAILURE: You tried creating a cinemachine-clip with a negative duration";
        }
        if (startTime < 0)
        {
            return "[[PROMPTRETURN]] FAILURE: You tried creating a cinemachine-clip with a negative startTime";
        }
        
        // track setup
        var animTrack = timeline.CreateTrack<AnimationTrack>(null, "Camera Animation");
        var animator = animatedObj.GetComponent<Animator>();
        if (animator == null) animator = animatedObj.gameObject.AddComponent<Animator>();
        
        director.SetGenericBinding(animTrack, animator);
    
        AnimationClip clip = new AnimationClip();
        clip.name = animatedObj.name;
        
        // order important
        // 1 add to timeline
        AssetDatabase.AddObjectToAsset(clip, timeline);

        // 2 create clip
        var animClip = animTrack.CreateClip(clip);
        animClip.start = startTime;
        animClip.duration = duration;
        // TODO: Change aswell, maybe ask user for name?
        animClip.displayName = animatedObj.name;
    
        EditorUtility.SetDirty(clip);
        EditorUtility.SetDirty(animatedObj.gameObject);
        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();

        return "[[PROMPTRETURN]] SUCCESS";
    }

    // TODO: Add option to set Camera Target
    // TODO: Set option for FOV
    // TODO: Are these componentTypeNames accordingly converted to the types after???

    // TODO: I think keyframes need to be broken here
    // public static string SetAnimationTrackTrackingTarget(string timelinePath, string trackName, string animationClipName,
    //     List<(float dutchValue, float clipTime)> trackPositions)
    // {
    //     return SetAnimationTrackCurve(timelinePath, trackName, animationClipName, "CinemachineCamera", "Target.TrackingTarget",
    //         trackPositions);
    // }
    
    public static string SetAnimationTrackLensDutch(string timelinePath, string trackName, string animationClipName, List<(float dutchValue, float clipTime)> trackPositions)
    {
        return SetAnimationTrackCurve(timelinePath, trackName, animationClipName, "CinemachineCamera", "Lens.Dutch",
            trackPositions);
    }  
    public static string SetAnimationTrackSplinePosition(string timelinePath, string trackName, string animationClipName, List<(float dutchValue, float clipTime)> trackPositions)
    {
        return SetAnimationTrackCurve(timelinePath, trackName, animationClipName, "CinemachineSplineDolly", "m_SplineSettings.Position",
            trackPositions);
    }

    /// <param name="componentTypeName">GetType().ToString - used because we use reflection to assign some properties</param>
    /// <param name="propertyName">Name of the property that we are trying to access via reflection like Lens.dutch</param>
    /// <param name="trackPositions">                                                                                                                                             
    ///     List of (splinePosition, clipTime) tuples.
    ///     splinePosition: 0-1 value representing where on the spline.                                                                                                               
    ///     clipTime: time in seconds within the clip.
    /// </param>
    static string SetAnimationTrackCurve(string timelinePath, string trackName, string animationClipName, string componentTypeName, string propertyName,  List<(float dutchValue, float clipTime)> trackPositions)
    {
        if (!GetTimelineAsset(timelinePath, out var timeline, out _))
        {
            // TODO: Maybe instruct to recall
            return "[[PROMPTRETURN]] FAILURE: No Timeline-Asset found under this path";
        }
        
        var track = timeline.GetOutputTracks()
            .OfType<AnimationTrack>()
            .FirstOrDefault(t => t.name == trackName);

        if (track == null)
        {
            return "[[PROMPTRETURN]] FAILURE: No AnimationTrack found with that name, the track was not created before trying to access its";
        }
        
        if (trackPositions.Any(e => e.clipTime < 0))
        {
            return "[[PROMPTRETURN]] FAILURE: You tried creating a cinemachine-clip with a negative track-position time";
        }

        // Dolly Position Curve
        AnimationCurve curve = new();
        foreach (var trackPosition in trackPositions)
        {
            curve.AddKey(trackPosition.clipTime, trackPosition.dutchValue);
        }

        var clip = track.GetClips()
            .FirstOrDefault(e => e.displayName == animationClipName);

        if (clip == null)
            return "Timeline Clip with the provided name wasnt found";
        
        var animClip = clip.animationClip;
        // "" means no parent
        if (animClip == null)
        {
            Debug.LogError("Clip is Null");
        }
        
        // TODO: If this doesnt work:
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == componentTypeName);
        
        if (type == null)
        {
            return "[[PROMPTRETURN]] FAILURE: to recreate the type from the string";
        }
        // TODO:
        // other solution:
        // AppDomain.CurrentDomain.GetAssemblies()
        // .SelectMany(a => a.GetTypes())                                                                                                                                 
        // .FirstOrDefault(t => t.Name == componentTypeName);
        animClip.SetCurve("", type, propertyName, curve);
        
        EditorUtility.SetDirty(animClip);
        EditorUtility.SetDirty(timeline); // because animclip is subasset of timeline
        AssetDatabase.SaveAssets();

        return "[[PROMPTRETURN]] SUCCESS";
    }
}