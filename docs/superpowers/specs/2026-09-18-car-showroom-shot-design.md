# Car Showroom Shot – Design

**Datum:** 2026-09-18
**Status:** Design abgenommen, bereit für Implementierungsplan
**Unity:** 6000.6.0f1 · `com.unity.ai.assistant` 2.9.0-pre.2 · `com.unity.cinemachine` 6.6.0 · `com.unity.timeline` 6.6.0

## Ziel

Der User beschreibt im **Unity AI Assistant Chat** eine Kamerafahrt um das Auto in der Szene (auch abstrakt, z. B. „geh auf das Auto zu, die Kamera wippt cinematisch“). Der Assistant erzeugt daraus **genau einen Kamera-Shot** (Spline-Dolly + Timeline), der dem Prompt entspricht.

- Der **User steuert**, der **Agent hat Freiraum** bei der Übersetzung des Prompts in Punkte.
- Die **Tools erzwingen ein standardisiertes Verfahren** und verhindern Aufruf-Fehler (keine GID-Ketten, keine freie Aufruf-Reihenfolge).
- Referenz-Look: **Assetto Corsa Showroom** – ruhige Fahrt, die Blickrichtung ist von Anfang an bekannt und dreht sich über den gesamten Shot **maximal 10°**.

## Nicht-Ziele (V1)

- Validierung von Start-/Endpunkt (nur der Spline wird validiert)
- Animierte FOV, Dutch/Roll, Cinemachine Noise
- Mehrere Shots als zusammenhängende Sequenz / Loop
- Eigenes EditorWindow bzw. `RunHeadless`-Pipeline
- Vordefinierte Shot-Bibliothek

## Einstieg: Skill im AI Assistant

Ein Project-Skill `car-showroom-shot` (`SKILL.md`) ersetzt für diesen Workflow `ProjectGuidelines.txt`. Der Skill listet im Frontmatter ausschließlich die vier CarCam-Tools unter `tools:`. `ProjectGuidelines.txt` bleibt unverändert im Projekt, wird vom Skill nicht referenziert.

Der Skill muss nach dem Anlegen einmalig unter **Unity > Settings > AI > Skills** auf **Allow** gesetzt werden.

## Ablauf

```
Prompt
  └─ 1. CarCam.GetCarContext   → Auto-Maße, Boden, benannte Teile
  └─ 2. CarCam.SetEndpoints    → Start/Ende + End-Blickziel (ggf. vom Agent abgeleitet)
  └─ 3. CarCam.BuildPath       → Zwischenpunkte → Spline → Validierung t=0…1
         ↺ bei VALIDATION_FAILED: Agent korrigiert selbst, max. 3 Versuche, dann User fragen
  └─ 4. CarCam.FinalizeShot    → VCam + Dolly + FixedLookBlend + Timeline
```

Fehlen Start- oder Endpunkt im Prompt, bestimmt der Agent sie selbst aus dem Prompt.

### Tool-Signaturen

| Tool-ID | Parameter | Wirkung |
|---|---|---|
| `CarCam.GetCarContext` | `carName = "BlockoutCar"` | Liefert Maße (L/B/H), Bodenhöhe, Forward, Teile mit auto-lokaler Position (als `CamPoint`-artige Werte), Collider-Status |
| `CarCam.SetEndpoints` | `shotName`, `CamPoint start`, `CamPoint end`, `LookTarget endLookTarget`, `LookTarget startLookTarget = null`, `fov = 35`, `carName = "BlockoutCar"` | Legt/überschreibt Session an, berechnet Rotationen inkl. Klemmung |
| `CarCam.BuildPath` | `shotName`, `List<CamPoint> intermediatePoints` | Spline bauen + validieren |
| `CarCam.FinalizeShot` | `shotName`, `duration`, `Easing easing = EaseInOut` | Kamera + Timeline |

`CamPoint`, `LookTarget` und `Easing` sind als Custom-Struct/Enum-Parameter übergeben (von `[AgentTool]` unterstützt).

## 1. Koordinatensystem & Blickrichtung

### Auto-Bezugssystem (`CarFrame`)

- Auto-Objekt per Name, Default `BlockoutCar`.
- Bounds = vereinigte `Renderer.bounds` aller Kinder.
- **Ursprung** = Bounds-Mitte, auf Bodenhöhe projiziert (`bounds.min.y`).
- **Forward** = `car.transform.forward`, auf die Horizontalebene projiziert.

### `CamPoint` (einziges Punktformat für den Agent)

| Feld | Bedeutung |
|---|---|
| `azimuthDeg` | 0 = direkt vorne, 90 = rechte Seite, 180 = Heck, −90 = links |
| `distance` | Meter horizontal von der Auto-Mitte (> 0) |
| `height` | Meter über dem Boden |

Welt-Position = `origin + Rotate(forward, azimuthDeg um Y) * distance + up * height`. Der Agent sieht nie Weltkoordinaten.

### Blickrichtung (Assetto-Corsa-Regel)

- `LookTarget` = Teilname (z. B. `Headlight_L`, `Wheel_0`) oder `"Center"`, plus optional `heightOffset` (m).
  - Teil-Position = Mitte der Renderer-Bounds dieses Teils; `"Center"` = Bounds-Mitte des Autos.
- **End-Rotation** = `LookRotation(endLookTarget − endPos, up)`. Pflicht.
- **Start-Rotation**:
  - ohne `startLookTarget`: = End-Rotation
  - mit `startLookTarget`: gewünschte Rotation zum Start-Ziel, **automatisch auf max. 10° Winkelabstand zur End-Rotation geklemmt** (`RotateTowards(end, desired, 10°)`).
- Roll ist immer 0.
- Während der Fahrt: `Slerp(startRot, endRot, p)` mit `p` = normalisierter Dolly-Fortschritt → die 10° sind konstruktiv garantiert.
- Tool-Rückgabe enthält den tatsächlichen Drehwinkel und ggf. Hinweis „gewünscht X°, geklemmt auf 10°“.

## 2. Spline bauen & validieren (`BuildPath`)

- Knots = Start + Zwischenpunkte (0…n, vom Agent) + Ende, Tangent-Mode `AutoSmooth` (bestehende `CreateSpline`-Logik).
- Erneuter Aufruf **ersetzt** den Spline der Session (keine `_OVERRIDE`-Logik).
- Der Spline bleibt auch bei Validierungsfehler in der Szene sichtbar.

### Validierung

Sampling alle ~0,1 m Spline-Länge, **mindestens 50 Samples**. Pro Sample: Position + interpolierte Rotation (s. o.).
Vor den Abfragen `Physics.SyncTransforms()`.

| Check | Regel (Default) |
|---|---|
| Clipping | `Physics.CheckSphere(pos, 0.15)` ist false; zusätzlich `SphereCast` zwischen aufeinanderfolgenden Samples |
| Boden | `height ≥ floorY + 0.2` |
| Abstand Auto | `Physics.OverlapSphere(pos, 0.25)` enthält keinen Auto-Collider (funktioniert auch mit nicht-konvexen MeshCollidern) |
| Blickziel sichtbar | Winkel zwischen Blickrichtung und Richtung zum aktuellen Blickziel ≤ FOV/2 (FOV der Session, gesetzt in `SetEndpoints`) **und** Raycast zum Blickziel trifft nur Auto-Collider |
| Kein Knick/Loop | Richtungsänderung der Spline-Tangente zwischen benachbarten Samples ≤ 30° |

Das „aktuelle Blickziel“ an Sample `p` = `Lerp(startLookPoint, endLookPoint, p)`.

Alle Grenzwerte liegen als Konstanten in einer `PathValidationSettings`-Struct (für Tests überschreibbar).

### Collider

`GetCarContext` prüft, ob die Autoteile Collider haben. Fehlen sie, fügt der Validator für die Dauer der Validierung **temporäre `MeshCollider`** hinzu und entfernt sie danach wieder (try/finally).

### Rückgabe

- **SUCCESS**: Spline-Länge, minimale Höhe über Boden, Sample-Anzahl.
- **VALIDATION_FAILED**: Liste zusammenhängender Problembereiche, z. B.
  `t 0.42–0.55: CLIPPING mit Pillar_2 (0.05 m) – nächster Zwischenpunkt: #2`
  `t 0.10–0.18: BELOW_MIN_HEIGHT (0.12 m) – nächster Zwischenpunkt: #0`

### Neuer Protokoll-Typ `VALIDATION_FAILED`

`BridgeProtocol.ValidationFailed(report, attempt)`:
- Agent **darf selbst** Zwischenpunkte korrigieren und `BuildPath` erneut aufrufen.
- Die Session zählt Versuche; ab dem 3. fehlgeschlagenen Versuch enthält die Rückgabe die Anweisung, den User zu fragen (wie `AWAITING_INPUT`).
- `FAILURE` und `AWAITING_INPUT` behalten ihr bisheriges Verhalten.

## 3. `FinalizeShot`

Signatur: `FinalizeShot(shotName, duration, easing = EaseInOut)`.
Verweigert (FAILURE), wenn der Session-Pfad nicht `PathValid` ist.

### Kamera

- `CinemachineCamera` (Kind des Session-Objekts), `Lens.FieldOfView` = FOV der Session, **kein** `RotationComposer`.
- `CinemachineSplineDolly` auf den Session-Spline, `PositionUnits = Normalized`, `CameraRotation = Default`, Damping aus.
- **`FixedLookBlend`** (neue `CinemachineExtension`, Runtime-Code):
  - Felder: `Quaternion StartRotation`, `Quaternion EndRotation`.
  - In `PostPipelineStageCallback`, Stage `Aim`: `state.RawOrientation = Slerp(Start, End, dolly.CameraPosition)`.
  - Damit treibt **eine** Kurve (Spline-Position) Position und Rotation synchron.

### Timeline

- Regel bleibt: **ein** Director, **eine** Timeline, **ein** CinemachineTrack pro Szene. Fehlende Teile werden angelegt (`CarCamTimeline`).
- Shot wird **hinter den letzten Clip** des CinemachineTracks gehängt.
- Erneutes Finalisieren desselben `shotName` **ersetzt** dessen Shot-Clip und Animations-Clip (keine Duplikate); Startzeit bleibt erhalten.
- Pro Shot eine AnimationTrack, gebunden an die VCam, Clip mit gleichem Start/Dauer, Kurve `m_SplineSettings.Position` von 0 → 1.
- **Easing** `Linear | EaseIn | EaseOut | EaseInOut` über Keyframe-Tangenten (Default `EaseInOut`).

### Wiederverwendung der Bridges

Die bestehenden Methoden in `AiCameraDirectorBridge` und `TimelineAiBridge` werden zu internen Bausteinen:
- Interne Überladungen, die direkt Objekte statt GID-Strings nehmen (die GID-Varianten bleiben für Abwärtskompatibilität).
- `SetAnimationTrackCurve` erhält eine Variante, die `Keyframe`s inkl. Tangenten übernimmt.

### Rückgabe

Timeline-Name, Startzeit, Dauer, angelegte Objekte, Hinweis „Timeline-Fenster öffnen und Play drücken“.

### Undo

Jeder Tool-Call = eine Undo-Gruppe (`Undo.IncrementCurrentGroup` / `CollapseUndoOperations`).

## 4. Zustand, Dateien, Fehlerbehandlung, Tests

### Session-Zustand

Szenen-Komponente **`CarCamShot`** auf Root-Objekt `CarCamShot_<shotName>` (Spline + VCam als Kinder). Serialisiert:
Auto-Referenz, Start/End-`CamPoint`, Start/End-`LookTarget`, FOV, berechnete Rotationen, Zwischenpunkte, `State`, Validierungsversuche, letzter Report.

Zustandsfolge: `EndpointsSet → PathValid → Finalized`.
- `SetEndpoints` (neu oder erneut) → `EndpointsSet`, Versuche = 0, Pfad-Gültigkeit zurückgesetzt.
- `BuildPath` erfolgreich → `PathValid`.
- `FinalizeShot` erfolgreich → `Finalized`.

Überlebt Domain-Reloads, wird mit der Szene gespeichert, im Inspector sichtbar.

### Dateien

```
Assets/CarCam/Runtime/
  CamPoint.cs            [Serializable] struct: azimuthDeg, distance, height
  LookTarget.cs          [Serializable] struct: part, heightOffset
  CarCamShot.cs          Session-Komponente
  FixedLookBlend.cs      CinemachineExtension
Assets/Editor/CarCam/
  CarCamTools.cs         4 [AgentTool]-Methoden, dünn: Parameter prüfen, Session laden, delegieren
  CarFrame.cs            Auto-Bezugssystem, CamPoint↔Welt, LookTarget-Auflösung, Blick-Klemmung
  PathValidator.cs       Sampling + Checks → Report
  ShotBuilder.cs         Kamera + Timeline über die Bridges
Assets/Editor/           bestehende Bridges: Objekt-Overloads, Keyframe-Kurve, ValidationFailed
Assets/AI_Instructions/Skills/car-showroom-shot/
  SKILL.md
  references/translation-guide.md
Assets/Tests/Editor/CarCam/   EditMode-Tests (+ Test-asmdef)
```

Tool-IDs: `CarCam.GetCarContext`, `CarCam.SetEndpoints`, `CarCam.BuildPath`, `CarCam.FinalizeShot`.

### Assemblies

EditMode-Tests in einer asmdef können `Assembly-CSharp-Editor` nicht referenzieren. Deshalb:
- `Assets/CarCam/Runtime/CarCam.Runtime.asmdef`
- `Assets/Editor/MercedesPrototype.Editor.asmdef` (Editor-only; enthält bestehende Bridges + `CarCam/`), `InternalsVisibleTo("CarCam.Tests")`
- `Assets/Tests/Editor/CarCam/CarCam.Tests.asmdef`

Die Bridges nutzen dafür kein `Unity.VisualScripting`-`AddComponent` mehr (→ `gameObject.AddComponent`). `EnsureBrain` setzt beim Anlegen einer Main Camera den Tag `MainCamera` (bisheriger Bug: sonst wird bei jedem Aufruf eine neue Kamera erzeugt).

### Skill-Inhalt

`SKILL.md`: Ablauf (4 Schritte, strikt in Reihenfolge), Regeln (Blick max. 10°, Blickziel am Ende zuerst festlegen, ruhige cinematische Bewegung, 3 Reparaturversuche), Verhalten bei den vier Rückgabetypen.
`references/translation-guide.md`: Übersetzung abstrakter Beschreibungen in `CamPoint`s mit Beispielen:
- „Auf das Auto zugehen“ (Distanz fällt, Höhe wippt leicht auf Augenhöhe ~1.6 m)
- „Tiefe, langsame Fahrt an der Seite entlang“
- „Vom Heck nach oben steigen, am Ende Blick auf die Front“

### Fehlerbehandlung

- Jeder Tool-Einstieg in try/catch → `FAILURE` mit Exception-Message.
- Falsche Reihenfolge → `FAILURE` mit Handlungsanweisung („Rufe zuerst CarCam.SetEndpoints auf“).
- Parameterprüfung: `distance > 0`, `duration > 0`, `fov` in (1, 179), unbekannter Teilname → `FAILURE` mit Liste gültiger Teilnamen, unbekannter `shotName` → `FAILURE` mit Liste existierender Sessions.

### Tests

EditMode-Tests (`com.unity.test-framework`):
- `CarFrame`: CamPoint→Welt für 0/90/180/−90°, gedrehtes Auto; Blick-Klemmung liefert immer ≤ 10°.
- `PathValidator`: Hindernis-Cube auf dem Pfad → CLIPPING im erwarteten t-Bereich; Punkt unter Mindesthöhe → BELOW_MIN_HEIGHT; scharfer Knick → KINK; freier Pfad → gültig.
- `ShotBuilder`: erneutes Finalisieren ersetzt Clips statt sie zu duplizieren; Easing-Tangenten korrekt.
- `FixedLookBlend`: Orientierung bei p = 0 / 0.5 / 1.

Manuelle Abnahme im Assistant mit den drei Beispiel-Prompts aus dem Translation-Guide.
