# CargoKing

**A Unity 6 driving prototype with a vehicle physics model and a road network editor, both built from scratch.**

![Unity](https://img.shields.io/badge/Unity-6000.3.8f1-000?logo=unity)
![Pipeline](https://img.shields.io/badge/render-URP%2017.3-blue)
![Language](https://img.shields.io/badge/C%23-.NET%20Standard%202.1-512BD4)
![Tests](https://img.shields.io/badge/EditMode%20tests-28-success)

This is a technical prototype, not a finished game. It exists to build three systems properly
rather than to ship content: a car driven by forces I compute myself instead of by `WheelCollider`, an in-editor tool that authors roads which double as the path network for AI traffic and a competitive racing AI.

All code under `Assets/Scripts/` is my own. I used AI (Claude Code) to assist with the documentation and with the implementation of the road-creation tool. 
Third-party packages are listed at the bottom.

---

## Vehicle physics

No `WheelCollider`. Each of the four wheels is a `Suspension` component that raycasts to the ground
and applies its own forces to the car body, so every force has a visible magnitude and a named point of application.

- **Spring and damper**: `F = offset · k − v · d`, applied at the suspension mount.
- **Lateral tire force**: the slip velocity along the wheel's lateral axis is countered by the grip
  factor. Turning that velocity change into a force uses the body's *effective mass at the contact
  point*, derived from the inertia tensor, rather than the total mass — a wheel off the center of
  mass rotates the car as well as translating it.
- **Longitudinal forces**: braking and rolling resistance are summed *before* clamping, then limited
  twice: by available grip (`μ · N`) and by the impulse it would take to reach a standstill this
  step. Both clamps exist for a reason documented in place.
- **Engine and drivetrain** (`CarEngine.cs`): torque curve, five gears plus reverse, axle ratio,
  drivetrain efficiency. RPM is computed backwards from wheel speed through the gearbox. A simplified clutch keeps the engine off the floor below launch revs, and the rev limiter fades torque out over a band instead of cutting it dead. All values can be changed to simulate different engines and gearboxes.
- **Aerodynamic drag** (`AeroDrag.cs`): `F = ½ · ρ · Cd · A · v²` against the actual direction of travel. It replaces Unity's `linearDamping`, which is linear in `v` and therefore brakes too hard
  at low speed and too weakly at high speed.

### Checked against a real car

The reference vehicle is a Fiat Punto 1.2: roughly 60 hp and a top speed near 155 km/h. Torque curve, gear ratios, mass, frontal area and drag coefficient are taken from that car, and the model is judged by whether it reproduces its top speed and acceleration.

### Seeing it work

Physics you cannot see is physics you cannot debug, so the project carries its own instruments:

- `VisualizeWheelForces.cs` draws suspension, slip, longitudinal and total tire force per wheel in
  the scene view, held in the pose they were computed in.
- A UI Toolkit overlay (`Assets/Scripts/UI/`) plots live telemetry: throttle, RPM, speed as
  scrolling graphs with ring-buffered series and auto-ranging axes.

---

## Road network editor

An editor tool for authoring roads. A road is a spline; its mesh is a tile swept along that spline;
its two lanes are sampled from the same frame the mesh sits on, so the surface a car drives on and
the path an AI follows can never drift apart. **Generated geometry is never serialized**. It is
rebuilt from the tile and the spline, which keeps scene files small.

- **Segments and meshing**: `StreetSegment` owns the spline, `StreetMeshBuilder` repeats and bends the tile along it, and `StreetCurvature` reports the tightest radius and warns if it's too low. 
- **Junctions**: prefabs carrying `IntersectionSocket` anchors. Sockets are spline anchors, and the
  lanes through a junction are derived from the connections between them.
- **Snapping**: dragging an end searches for candidates, validates the pair, and connects or
  disconnects it.
- **Spline surgery** (`StreetSurgery.cs`): reverse a street, merge two into one, split one at an
  inner knot. Merge handles all four ways two ends can meet, converts knots correctly through rotated and offset transforms, and refuses mismatched road widths rather than silently adopting one of them.
- **Junction insertion** (`JunctionPlacement.cs`): replaces a knot with a junction prefab. The
  placement math is a pure function: it reads the prefab's sockets, finds an opposing pair, and
  returns a pose. Nothing is instantiated and nothing in the scene is touched, which is what makes the easiest-to-get-subtly-wrong part of the feature testable on its own.
- **Junction palette** (`StreetKit.cs`): a `ScriptableObject` listing the prefabs the knot menu
  offers. Curated by hand rather than folder-scanned, so test leftovers never appear in the palette.

---

## Code map

| File | Role |
|---|---|
| `Assets/Scripts/Suspension.cs` | Raycast suspension, tire slip, braking, rolling resistance — the heart of the physics |
| `Assets/Scripts/CarEngine.cs` | Torque curve, gearbox, clutch, rev limiter |
| `Assets/Scripts/CarController.cs` | Turns one `DrivingInput` into drive forces, steering and brake commands |
| `Assets/Scripts/AeroDrag.cs` | Quadratic air resistance |
| `Assets/Scripts/EditorTesting/AIDriver.cs` | Point-seeking driver with turning-circle detection and a reversing manoeuvre |
| `Assets/Scripts/Streets/StreetSegment.cs` | One stretch of road: spline, mesh, lanes, connectors |
| `Assets/Scripts/Streets/StreetMeshBuilder.cs` | Sweeps the tile along the spline |
| `Assets/Scripts/Streets/Editor/StreetSurgery.cs` | Reverse, merge and split on splines |
| `Assets/Scripts/Streets/Editor/JunctionPlacement.cs` | Where a junction prefab has to sit at a knot |

Player and AI both drive through the same `DrivingInput` struct, so the AI genuinely drives the car
instead of steering the transform.

---

## Tests

28 EditMode tests cover the parts of the road editor that are pure logic: spline surgery, junction
placement, palette validation. They are named as behaviour rather than as methods:

```
Merge_ConvertsKnotsThroughARotatedTransform
Merge_RefusesAndChangesNothingWhenTheWidthsDiffer
Split_HandsTheOuterConnectionToTheSecondHalf
TryAlign_PutsTheEntrySocketAgainstTheDirectionOfTravel
```

Run them in Unity via **Window → General → Test Runner → EditMode**.

The road code is split across three assembly definitions — `CargoKing.Streets` (runtime),
`CargoKing.Streets.Editor` (tooling) and `CargoKing.Streets.Editor.Tests` which keeps editor-only code out of builds and lets the test assembly reach the tooling without the tooling reaching back.

---

## Running it

Unity **6000.3.8f1** (Unity 6), Universal Render Pipeline. Open the project and pick a scene:

| Scene                                | What it is                                      |
| ------------------------------------ | ----------------------------------------------- |
| `Assets/Scenes/SampleScene.unity`    | The car, the debug overlay and the force gizmos |
| `Assets/Scenes/FirstTestTrack.unity` | A track to drive                                |
| `Assets/Scenes/Car_AI_Test.unity`    | The AI driver seeking a target                  |

Input runs through the Input System (`InputSystem_Actions`). Currently available input devices are keyboard and gamepad. 
Keyboard input is ramped and shaped by a response curve; analogue devices are passed through untouched, because a gamepad stick already *is* the axis a keyboard has to fake.

---
## Third-party assets

| What                                                         | Where                | Notes                                      |
| ------------------------------------------------------------ | -------------------- | ------------------------------------------ |
| [R3](https://github.com/Cysharp/R3) (Cysharp)                | Package Manager      | reactive extensions, used by the AI driver |
| [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) | Package Manager      |                                            |
| SplineMesh (not in use)                                      | `Assets/SplineMesh/` | third-party spline library                 |

