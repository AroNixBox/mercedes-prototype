---
name: car-showroom-shot
description: Builds ONE spline (the future camera path / Kamerafahrt) around the car in the open scene from the user's description, in Assetto Corsa showroom style (slow, calm, cinematic moves). Proof of concept - spline only, no camera. Activate when the user asks for a camera move, camera path, spline, Kamerafahrt or cinematic move around the car.
required_packages:
  com.unity.splines: ">=2.0.0"
tools:
  - CarCam.GetCarContext
  - CarCam.BuildPath
---

# Car Showroom Spline

Build exactly ONE spline around the car that matches the user's description as closely as possible.
This spline is the path a camera will later travel along. Proof of concept: build ONLY the spline — no camera, no timeline.
Style reference: the Assetto Corsa showroom camera. Calm, slow, cinematic moves; no sudden direction changes.

## Workflow – exactly one tool call at a time

1. `CarCam.GetCarContext` – read the car size, the floor and the part positions.
2. Translate the prompt into a path using `references/translation-guide.md`:
   path name (shotName), start CamPoint, end CamPoint, 0–4 intermediate CamPoints.
   If the user gave no start or end, choose them yourself from the description.
   Tell the user the plan in 1–3 plain sentences, then continue.
3. `CarCam.BuildPath` with shotName, start, end and the intermediate points (pass `[]` or omit them when the move needs none, e.g. a straight pass).
4. `VALIDATION_FAILED` → fix the intermediate points and call `CarCam.BuildPath` again with the SAME shotName (it replaces the spline).
   At most 3 automatic fixes per request, then ask the user.
5. On SUCCESS tell the user which object to inspect: `CarCamPath_<shotName>` in the scene.

## Rules

- Use ONLY the two CarCam tools. Never write C# scripts, never call other bridge methods, never create cameras, timelines or other scene objects.
- Never use world coordinates. Only CamPoints (azimuthDeg / distance / height), using part positions from `CarCam.GetCarContext` as reference.
- Cinematic means: smooth, evenly spaced points, no back-and-forth, no zig-zag.
- One path per request. To change an existing path, reuse its shotName.

## Tool results

- `[[PROMPTRETURN]] SUCCESS` → continue with the step named in the result.
- `[[PROMPTRETURN]] VALIDATION_FAILED` → follow the embedded instruction (see "Fixing validation problems" in the translation guide).
  Do not change start/end without asking the user if the user specified them.
- `[[PROMPTRETURN]] FAILURE` → stop. Show the reason to the user, propose 2–3 numbered options, wait for their pick.
