# Translating a move description into CamPoints

## Coordinates

- `azimuthDeg`: 0 = in front of the car, 90 = right side, 180 = behind, -90 = left side. 45 = front-right corner, 135 = rear-right corner.
- `distance`: horizontal meters from the car CENTER (not from the surface). Half the car length is roughly the front/rear surface,
  half the width the side surface. Always stay ≥ 0.3 m outside the surface.
- `height`: meters above the floor.
- Part positions from `CarCam.GetCarContext` are in the same format – use them to place points near a part
  (e.g. near a headlight: same azimuth as the headlight, distance = headlight distance + 0.6–1.2 m).
- The spline runs through the points in order (start → intermediates → end) with smooth automatic tangents.
  A straight line between two points = no intermediates.

## Typical values

| Description | height | distance |
|---|---|---|
| Ground level / very low | 0.3–0.5 | – |
| Hero / bumper height | 0.6–0.9 | – |
| Eye level, "walking" | 1.5–1.7 | – |
| Elevated / crane | 2.5–4 | – |
| Close | – | surface + 0.5–1.2 |
| Medium | – | surface + 2–3.5 |
| Wide / establishing | – | surface + 5–8 |

## Patterns

**Walk up to the car ("geh auf das Auto zu", cinematic bob)**
- start: az A, distance = surface + 6, height 1.6 · end: same az A (±5), distance = surface + 1.5, height 1.5
- intermediates: 2–3 points evenly between them, heights alternating ±0.05–0.12 m around 1.6 (gentle bob, not bouncy)

**Low slow pass along the side ("tiefe, langsame Fahrt an der Seite entlang")**
- a straight line PARALLEL to the car side at a constant lateral offset (e.g. 2 m outside the side surface), height 0.4–0.6
- start beside the front wheel, end beside the rear wheel (use the wheel parts' azimuths from the context; for the right side
  start az ≈ 60–70, end az ≈ 110–120 at the same lateral offset – distance = sqrt(lateral² + forwardOffset²))
- intermediates: none

**Rise from the rear ("vom Heck nach oben steigen")**
- start: az 180, distance = surface + 3, height 1.0 · end: az 180, distance = surface + 6, height 2.6
- 1 intermediate point halfway, slightly above the straight line (soft crane curve)

**Arc / orbit around a corner ("halb ums Auto herum")**
- constant distance and height, azimuth changes; place intermediates every 20–30° of azimuth so the arc stays round
  (with only start and end the spline would cut straight through the corner)

**Detail slide (e.g. along a headlight)**
- start and end both near the part (distance = part distance + 0.8…1.5), sliding sideways by 0.5–1 m at constant height; no intermediates

## Fixing validation problems

| Report | Fix |
|---|---|
| `CLIPPING with X` | Move the nearest point away from X (more distance or height), or add a point that routes around X. |
| `BELOW_MIN_HEIGHT` | Raise the nearest point(s) to ≥ 0.3 m. If the dip is between points (spline overshoot), add a point there with a higher height. |
| `TOO_CLOSE_TO_CAR` | Increase the distance of the nearest point by 0.3–0.5 m; for arcs add intermediates so the spline doesn't cut the corner. |
| `KINK` | Remove the point that makes the path go back and forth, or spread the points more evenly along the path. |
