# Straßen verschmelzen, trennen und Kreuzungen einsetzen — Umsetzungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zwei verbundene `StreetSegment` werden ein Segment mit einem Spline, ein Spline lässt sich an einem Knoten trennen, und ein Knoten lässt sich durch eine Kreuzung ersetzen.

**Architecture:** Alle Änderungen liegen im Editor-Assembly. `StreetSurgery` macht die reine Spline- und Verbinder-Chirurgie (verschmelzen, umkehren, trennen) und kennt keine Kreuzungen. `JunctionPlacement` rechnet aus, welches Socket-Paar die Durchfahrt ist und wohin das Prefab gehört — ohne die Szene anzufassen, deshalb prüfbar. `JunctionInsertion` setzt beides zusammen und ist der einzige Teil, der Objekte instanziiert. Die Laufzeit (`StreetSegment`, `Intersection`) bleibt unangetastet.

**Tech Stack:** Unity 6000.3.8f1, `com.unity.splines` 2.8.4, `com.unity.test-framework` 1.6.0 (NUnit, EditMode), C#.

**Spec:** [`docs/superpowers/specs/2026-09-01-street-merge-and-junction-insertion-design.md`](../specs/2026-09-01-street-merge-and-junction-insertion-design.md)

## Global Constraints

- **Code-Kommentare und XML-Doc auf Englisch.** Auch Strings, die im Editor angezeigt werden. Nur dieses Plandokument und die Spec sind deutsch.
- **Alles Neue liegt im Editor-Assembly** `CargoKing.Streets.Editor` (`Assets/Scripts/Streets/Editor/`). `Assets/Scripts/Streets/*.cs` wird in diesem Plan nicht geändert.
- **Kein Löschen von bestehendem Code.** `StreetEndConnector.segment`, `segmentEnd`, `driven` und ihre Zweige bleiben stehen, auch wenn sie nach Task 4 keinen Auslöser mehr haben. Task 11 legt sie dem Nutzer einzeln vor.
- **Kein Editieren von `.unity`- oder `.prefab`-YAML.** Unity kann offen sein und würde die Dateien unter uns zurückschreiben.
- **Segment-Zugriff auf die Spline** immer über `segment.GetComponent<SplineContainer>()`. `StreetSegment.Spline` ist privat und bleibt es.
- **Nichts Ungeprüftes als fertig melden.** Jede Aufgabe endet mit einem Kompilierlauf, und wo Tests dabei sind, mit deren Ergebnis.

## Wie geprüft wird

**Kompilieren ohne Unity** (Zwischendateien ins Scratchpad, damit das Projekt sauber bleibt):

```bash
SP="C:/Users/seanr/AppData/Local/Temp/claude/E--Unity-Projekte-Repos-CargoKing-CargoKing/f24e7ea0-20cc-4758-9518-9fd90514997a/scratchpad"
dotnet build CargoKing.Streets.Editor.csproj -v q -nologo \
  -p:BaseIntermediateOutputPath="$SP/obj/" -p:BaseOutputPath="$SP/bin/"
```

Die `.csproj` erzeugt Unity. Sie kennt eine gerade erst angelegte Datei erst, wenn Unity den Fokus bekommen hat. Ist das noch nicht passiert, die `Compile`-Zeile für den Lauf einfügen und danach zurücknehmen:

```bash
cp CargoKing.Streets.Editor.csproj "$SP/csproj.bak"
sed -i '52a\    <Compile Include="Assets/Scripts/Streets/Editor/NeueDatei.cs" />' CargoKing.Streets.Editor.csproj
# bauen
cp "$SP/csproj.bak" CargoKing.Streets.Editor.csproj
```

**Tests laufen lassen — gilt für diesen Lauf verbindlich.** Unity ist während der gesamten Umsetzung geöffnet und hält `Temp/UnityLockfile`. Headless (`Unity.exe -runTests -batchmode`) ist damit ausgeschlossen, und **kein ausführender Agent kann einen Test starten.**

Daraus folgt für jede Aufgabe:

- Der Implementierer schreibt die Tests wie beschrieben, **führt sie nicht aus** und **behauptet nie, sie seien gelaufen**. Sein Bericht sagt an der Stelle wörtlich: „Tests geschrieben, nicht ausgeführt — Unity hält die Projektsperre."
- Geprüft wird stattdessen: `dotnet build CargoKing.Streets.Editor.csproj` muss durchlaufen. Zusätzlich `dotnet build CargoKing.Streets.Editor.Tests.csproj`, **falls** Unity diese `.csproj` schon erzeugt hat; fehlt sie, ist das kein Fehlschlag, sondern heißt nur, dass Unity den neuen `.asmdef` noch nicht importiert hat.
- Der Reviewer verlangt **keine** Testausführungsbelege. Er prüft die Tests durch Lesen: decken sie ab, was die Aufgabe verspricht, und behaupten sie das Richtige?
- Die Schritte „Run test to verify it fails" entfallen in diesem Lauf. Rot-vor-Grün ist nicht herstellbar; das ist eine bewusste Einbuße, kein Versehen.

**Drei Kontrollpunkte** holen die Ausführung nach. Nach Task 3, Task 5 und Task 8 bittet der Steuernde den Nutzer um `Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All` und wertet das Ergebnis aus, bevor die nächste Aufgabe startet. Erwartete Zahlen stehen bei den jeweiligen Aufgaben.

**Von Hand prüfen:** Aufgaben ohne Tests (Anfasser, Menüs, Instanziieren) tragen eine ausdrückliche Prüfliste. Auch die legt der Agent dem Nutzer vor, statt sie zu behaupten.

## Dateien

| Datei | Verantwortung | Aufgabe |
|---|---|---|
| `Editor/Tests/CargoKing.Streets.Editor.Tests.asmdef` | Test-Assembly | 1 |
| `Editor/Tests/StreetTestFactory.cs` | Segmente für Tests bauen und wieder abräumen | 1 |
| `Editor/Tests/StreetSurgeryTests.cs` | Tests für Vorbedingungen, Umkehren, Verschmelzen, Trennen | 1–3, 5 |
| `Editor/Tests/JunctionPlacementTests.cs` | Tests für die Ausrichtungsrechnung | 8 |
| `Editor/StreetSurgery.cs` | Verschmelzen, Umkehren, Trennen, Vorbedingungen | 1–3, 5 |
| `Editor/StreetSnapping.cs` | `Connect` leitet Segment-an-Segment auf `Merge` um | 4 |
| `Editor/IntersectionSocketDragging.cs` | Selektiert nach dem Verschmelzen den Überlebenden | 4 |
| `Editor/StreetKnotHandles.cs` | Anfasser an inneren Knoten, Menü darauf | 6, 9 |
| `Editor/StreetSegmentEditor.cs` | Ruft die Knoten-Anfasser | 6 |
| `Editor/StreetKit.cs` | Palette der Kreuzungs-Prefabs | 7 |
| `Editor/JunctionPlacement.cs` | Rein rechnend: Durchfahrt-Paar, Position, Drehung | 8 |
| `Editor/JunctionInsertion.cs` | Einsetzen, Umdrehen, Entfernen | 9, 10 |
| `Editor/IntersectionEditor.cs` | Knöpfe für Umdrehen und Entfernen | 10 |

---

### Task 1: Test-Assembly und die Vorbedingungen des Verschmelzens

**Files:**
- Create: `Assets/Scripts/Streets/Editor/Tests/CargoKing.Streets.Editor.Tests.asmdef`
- Create: `Assets/Scripts/Streets/Editor/Tests/StreetTestFactory.cs`
- Create: `Assets/Scripts/Streets/Editor/Tests/StreetSurgeryTests.cs`
- Create: `Assets/Scripts/Streets/Editor/StreetSurgery.cs`

**Interfaces:**
- Produces: `StreetSurgery.CanMerge(StreetSegment dragged, StreetSegment target, out string problem) → bool`, `StreetSurgery.SplineOf(StreetSegment) → Spline`
- Produces: `StreetTestFactory.Create(string name, params Vector3[] localKnots) → StreetSegment`, `StreetTestFactory.DestroyAll()`

- [ ] **Step 1: Test-Assembly anlegen**

`Assets/Scripts/Streets/Editor/Tests/CargoKing.Streets.Editor.Tests.asmdef`:

```json
{
    "name": "CargoKing.Streets.Editor.Tests",
    "rootNamespace": "CargoKing.Streets.Editor.Tests",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "CargoKing.Streets",
        "CargoKing.Streets.Editor",
        "Unity.Splines",
        "Unity.Mathematics"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Das `defineConstraints` sorgt dafür, dass die Tests nicht in einen Build wandern. Ohne `overrideReferences` plus `precompiledReferences` findet der Compiler NUnit nicht.

- [ ] **Step 2: Die Fabrik für Test-Segmente schreiben**

`Assets/Scripts/Streets/Editor/Tests/StreetTestFactory.cs`:

```csharp
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace CargoKing.Streets.Editor.Tests
{
    /// <summary>
    /// Builds throwaway street segments for tests and takes them away again.
    ///
    /// Every object it hands out is remembered, because a leaked StreetSegment keeps its subscription
    /// to the static Spline.Changed event and would go on reacting to splines in later tests.
    /// </summary>
    internal static class StreetTestFactory
    {
        private static readonly List<GameObject> created = new List<GameObject>();

        /// <summary>
        /// A segment whose spline runs through the given points, expressed in its own local space.
        /// </summary>
        public static StreetSegment Create(string name, params Vector3[] localKnots)
        {
            GameObject gameObject = new GameObject(name);
            created.Add(gameObject);

            SplineContainer container = gameObject.AddComponent<SplineContainer>();
            Spline spline = container.Spline;
            spline.Clear();

            for (int index = 0; index < localKnots.Length; index++)
            {
                Vector3 point = localKnots[index];
                spline.Add(new BezierKnot(new float3(point.x, point.y, point.z)), TangentMode.AutoSmooth);
            }

            // Added after the container so StreetSegment.OnEnable finds it.
            StreetSegment segment = gameObject.AddComponent<StreetSegment>();
            segment.roadWidth = 16f;
            segment.tileLength = 0f;
            segment.forwardAxis = StreetMeshAxis.X;

            return segment;
        }

        public static void DestroyAll()
        {
            for (int index = 0; index < created.Count; index++)
            {
                if (created[index] != null)
                {
                    Object.DestroyImmediate(created[index]);
                }
            }

            created.Clear();
        }
    }
}
```

- [ ] **Step 3: Die fehlschlagenden Tests schreiben**

`Assets/Scripts/Streets/Editor/Tests/StreetSurgeryTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetSurgeryTests
    {
        [TearDown]
        public void TearDown()
        {
            StreetTestFactory.DestroyAll();
        }

        [Test]
        public void CanMerge_AcceptsTwoMatchingSegments()
        {
            StreetSegment a = StreetTestFactory.Create("A", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment b = StreetTestFactory.Create("B", Vector3.zero, new Vector3(0f, 0f, 10f));

            Assert.IsTrue(StreetSurgery.CanMerge(b, a, out string problem), problem);
            Assert.IsNull(problem);
        }

        [Test]
        public void CanMerge_RefusesDifferentRoadWidths()
        {
            StreetSegment a = StreetTestFactory.Create("A", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment b = StreetTestFactory.Create("B", Vector3.zero, new Vector3(0f, 0f, 10f));
            b.roadWidth = 7f;

            Assert.IsFalse(StreetSurgery.CanMerge(b, a, out string problem));
            StringAssert.Contains("wide", problem);
        }

        [Test]
        public void CanMerge_RefusesTheSameSegmentTwice()
        {
            StreetSegment a = StreetTestFactory.Create("A", Vector3.zero, new Vector3(0f, 0f, 10f));

            Assert.IsFalse(StreetSurgery.CanMerge(a, a, out string problem));
            Assert.IsNotNull(problem);
        }

        [Test]
        public void CanMerge_RefusesScaledSegments()
        {
            StreetSegment a = StreetTestFactory.Create("A", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment b = StreetTestFactory.Create("B", Vector3.zero, new Vector3(0f, 0f, 10f));
            b.transform.localScale = new Vector3(2f, 1f, 1f);

            Assert.IsFalse(StreetSurgery.CanMerge(b, a, out string problem));
            StringAssert.Contains("scale", problem);
        }
    }
}
```

- [ ] **Step 4: `StreetSurgery` mit den Vorbedingungen schreiben**

`Assets/Scripts/Streets/Editor/StreetSurgery.cs`:

```csharp
using UnityEngine;
using UnityEngine.Splines;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Spline and connector surgery on street segments: joining two into one, turning one around,
    /// cutting one in half.
    ///
    /// Knows nothing about intersections. Inserting a junction is this surgery with a prefab put in
    /// the gap, and keeping the two apart is what makes the surgery testable on its own.
    /// </summary>
    public static class StreetSurgery
    {
        /// <summary>How far a transform's scale may stray from 1 before it is refused.</summary>
        private const float ScaleEpsilon = 0.0001f;

        /// <summary>The spline of a segment. StreetSegment keeps its own reference private.</summary>
        public static Spline SplineOf(StreetSegment segment)
        {
            SplineContainer container = segment != null ? segment.GetComponent<SplineContainer>() : null;
            return container != null ? container.Spline : null;
        }

        /// <summary>
        /// Whether two segments may become one, and why not when they may not.
        ///
        /// Everything that says how the road is built has to agree. Quietly taking one of two road
        /// widths would change geometry nobody asked to have changed.
        /// </summary>
        public static bool CanMerge(StreetSegment dragged, StreetSegment target, out string problem)
        {
            problem = null;

            if (dragged == null || target == null)
            {
                problem = "One of the two streets is gone.";
                return false;
            }

            if (dragged == target)
            {
                problem = "A street cannot be merged with itself.";
                return false;
            }

            Spline draggedSpline = SplineOf(dragged);
            Spline targetSpline = SplineOf(target);

            if (draggedSpline == null || targetSpline == null
                || draggedSpline.Count < 2 || targetSpline.Count < 2)
            {
                problem = "Both streets need a spline with at least two knots.";
                return false;
            }

            if (!Mathf.Approximately(dragged.roadWidth, target.roadWidth))
            {
                problem = $"'{dragged.name}' is {dragged.roadWidth:0.0} m wide and '{target.name}' is "
                    + $"{target.roadWidth:0.0} m. Streets of different width cannot become one.";
                return false;
            }

            if (dragged.sourceMesh != target.sourceMesh
                || dragged.forwardAxis != target.forwardAxis
                || !Mathf.Approximately(dragged.tileLength, target.tileLength))
            {
                problem = $"'{dragged.name}' and '{target.name}' are built from different tiles.";
                return false;
            }

            if (!IsUnscaled(dragged.transform) || !IsUnscaled(target.transform))
            {
                problem = "Both streets need a scale of 1. A scaled transform would distort the "
                    + "tangent lengths when the knots are converted.";
                return false;
            }

            return true;
        }

        private static bool IsUnscaled(Transform transform)
        {
            Vector3 scale = transform.lossyScale;

            return Mathf.Abs(scale.x - 1f) < ScaleEpsilon
                && Mathf.Abs(scale.y - 1f) < ScaleEpsilon
                && Mathf.Abs(scale.z - 1f) < ScaleEpsilon;
        }
    }
}
```

- [ ] **Step 5: Kompilieren**

Lauf: der `dotnet build`-Befehl aus „Wie geprüft wird". Die neuen Dateien sind Unity eventuell noch unbekannt — dann die `Compile`-Zeile für `StreetSurgery.cs` einfügen und danach zurücknehmen. Das Test-Assembly baut `dotnet` nicht mit; dessen Fehler zeigt erst Unity.

Erwartet: `Der Buildvorgang wurde erfolgreich ausgeführt.`

- [ ] **Step 6: Den Nutzer um den Testlauf bitten**

Bitte den Nutzer, in Unity `Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All` zu starten. Erwartet: vier grüne Tests in `StreetSurgeryTests`. Erst danach gilt die Aufgabe als fertig.

- [ ] **Step 7: Commit**

```bash
git add "Assets/Scripts/Streets/Editor/Tests" "Assets/Scripts/Streets/Editor/StreetSurgery.cs"
git commit -m "feat(streets): add edit-mode test assembly and merge preconditions"
```

---

### Task 2: Eine Straße umkehren

**Files:**
- Modify: `Assets/Scripts/Streets/Editor/StreetSurgery.cs`
- Modify: `Assets/Scripts/Streets/Editor/Tests/StreetSurgeryTests.cs`

**Interfaces:**
- Consumes: `StreetSurgery.SplineOf`
- Produces: `StreetSurgery.Reverse(StreetSegment segment)`

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

An `StreetSurgeryTests` anhängen:

```csharp
        [Test]
        public void Reverse_TurnsTheKnotOrderAround()
        {
            StreetSegment segment = StreetTestFactory.Create(
                "A",
                Vector3.zero,
                new Vector3(0f, 0f, 10f),
                new Vector3(0f, 0f, 30f));

            StreetSurgery.Reverse(segment);

            UnityEngine.Splines.Spline spline = StreetSurgery.SplineOf(segment);
            Assert.AreEqual(3, spline.Count);
            Assert.AreEqual(30f, spline[0].Position.z, 0.001f);
            Assert.AreEqual(10f, spline[1].Position.z, 0.001f);
            Assert.AreEqual(0f, spline[2].Position.z, 0.001f);
        }

        [Test]
        public void Reverse_TurnsTheDirectionOfTravelAround()
        {
            StreetSegment segment = StreetTestFactory.Create(
                "A",
                Vector3.zero,
                new Vector3(0f, 0f, 10f),
                new Vector3(0f, 0f, 30f));

            StreetSurgery.Reverse(segment);

            // The spline used to run towards +Z, so after turning it around it has to run towards -Z.
            Assert.Less(segment.EndDirection(StreetEnd.Start).z, 0f);
        }

        [Test]
        public void Reverse_SwapsTheTwoConnectors()
        {
            StreetSegment segment = StreetTestFactory.Create(
                "A",
                Vector3.zero,
                new Vector3(0f, 0f, 10f));

            GameObject socketObject = new GameObject("Socket");
            IntersectionSocket socket = socketObject.AddComponent<IntersectionSocket>();
            segment.startConnection.socket = socket;

            StreetSurgery.Reverse(segment);

            Assert.AreSame(socket, segment.endConnection.socket);
            Assert.IsNull(segment.startConnection.socket);

            Object.DestroyImmediate(socketObject);
        }
```

- [ ] **Step 2: Tests laufen lassen und den Fehlschlag sehen**

Vom Nutzer im Test Runner. Erwartet: die drei neuen Tests kompilieren nicht, weil `StreetSurgery.Reverse` fehlt.

- [ ] **Step 3: `Reverse` schreiben**

In `StreetSurgery` einfügen (`using Unity.Mathematics;` ergänzen):

```csharp
        /// <summary>
        /// Turns a street around: the last knot becomes the first, and the road runs the other way.
        ///
        /// Needed because two of the four ways two streets can meet require one side to be read
        /// backwards, and in one of them that side is the survivor itself.
        /// </summary>
        public static void Reverse(StreetSegment segment)
        {
            Spline spline = SplineOf(segment);
            if (spline == null || spline.Count < 2)
            {
                return;
            }

            int count = spline.Count;
            BezierKnot[] knots = new BezierKnot[count];
            TangentMode[] modes = new TangentMode[count];

            for (int index = 0; index < count; index++)
            {
                knots[index] = spline[index];
                modes[index] = spline.GetTangentMode(index);
            }

            spline.Clear();
            for (int index = count - 1; index >= 0; index--)
            {
                spline.Add(Flip(knots[index]), modes[index]);
            }

            // The connectors describe ends, and the ends have just changed places.
            StreetEndConnector start = segment.startConnection;
            segment.startConnection = segment.endConnection;
            segment.endConnection = start;
        }

        /// <summary>
        /// One knot, turned around. Its frame is spun half a turn about its own up axis so that its
        /// forward points the new way while up stays where it was - a road that is read backwards must
        /// not end up upside down.
        ///
        /// The tangents live in that frame. Spinning the frame flips their sign, and reading the road
        /// backwards swaps which of the two leads in, so they are exchanged and negated together.
        /// </summary>
        private static BezierKnot Flip(BezierKnot knot)
        {
            quaternion rotation = math.mul(knot.Rotation, quaternion.RotateY(math.PI));
            return new BezierKnot(knot.Position, -knot.TangentOut, -knot.TangentIn, rotation);
        }
```

- [ ] **Step 4: Kompilieren und Tests laufen lassen**

`dotnet build` wie oben, dann der Nutzer im Test Runner. Erwartet: sieben grüne Tests.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Streets/Editor/StreetSurgery.cs" "Assets/Scripts/Streets/Editor/Tests/StreetSurgeryTests.cs"
git commit -m "feat(streets): reverse a street segment"
```

---

### Task 3: Zwei Straßen verschmelzen

**Files:**
- Modify: `Assets/Scripts/Streets/Editor/StreetSurgery.cs`
- Modify: `Assets/Scripts/Streets/Editor/Tests/StreetSurgeryTests.cs`

**Interfaces:**
- Consumes: `StreetSurgery.CanMerge`, `StreetSurgery.Reverse`, `StreetSurgery.SplineOf`
- Produces: `StreetSurgery.Merge(StreetSegment dragged, StreetEnd draggedEnd, StreetSegment target, StreetEnd targetEnd) → StreetSegment` (der Überlebende, oder `null` wenn abgelehnt)

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

An `StreetSurgeryTests` anhängen. Die vier Fälle prüfen dasselbe Ergebnis über vier verschiedene Ausgangslagen — dass die zusammengesetzte Straße von `z = 0` nach `z = 30` läuft:

```csharp
        private static float[] MergedPositions(StreetSegment survivor)
        {
            UnityEngine.Splines.Spline spline = StreetSurgery.SplineOf(survivor);
            float[] result = new float[spline.Count];

            for (int index = 0; index < spline.Count; index++)
            {
                result[index] = survivor.transform.TransformPoint(
                    new Vector3(spline[index].Position.x, spline[index].Position.y, spline[index].Position.z)).z;
            }

            return result;
        }

        [Test]
        public void Merge_JoinsEndToStart()
        {
            StreetSegment target = StreetTestFactory.Create("T", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment dragged = StreetTestFactory.Create(
                "D", new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 30f));

            StreetSegment survivor = StreetSurgery.Merge(dragged, StreetEnd.Start, target, StreetEnd.End);

            Assert.AreSame(target, survivor);
            Assert.AreEqual(new[] { 0f, 10f, 30f }, MergedPositions(survivor));
            Assert.IsTrue(dragged == null, "The dragged segment has to be gone.");
        }

        [Test]
        public void Merge_JoinsEndToEnd()
        {
            StreetSegment target = StreetTestFactory.Create("T", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment dragged = StreetTestFactory.Create(
                "D", new Vector3(0f, 0f, 30f), new Vector3(0f, 0f, 10f));

            StreetSegment survivor = StreetSurgery.Merge(dragged, StreetEnd.End, target, StreetEnd.End);

            Assert.AreEqual(new[] { 0f, 10f, 30f }, MergedPositions(survivor));
        }

        [Test]
        public void Merge_JoinsStartToStart()
        {
            StreetSegment target = StreetTestFactory.Create("T", new Vector3(0f, 0f, 10f), Vector3.zero);
            StreetSegment dragged = StreetTestFactory.Create(
                "D", new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 30f));

            StreetSegment survivor = StreetSurgery.Merge(dragged, StreetEnd.Start, target, StreetEnd.Start);

            Assert.AreEqual(new[] { 0f, 10f, 30f }, MergedPositions(survivor));
        }

        [Test]
        public void Merge_JoinsStartToEnd()
        {
            StreetSegment target = StreetTestFactory.Create("T", new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 30f));
            StreetSegment dragged = StreetTestFactory.Create("D", Vector3.zero, new Vector3(0f, 0f, 10f));

            StreetSegment survivor = StreetSurgery.Merge(dragged, StreetEnd.End, target, StreetEnd.Start);

            // Both sides had to be turned around for this one, so the joined road is described from
            // the far end backwards. Same road, read the other way - a merge promises no direction.
            Assert.AreEqual(new[] { 30f, 10f, 0f }, MergedPositions(survivor));
        }

        [Test]
        public void Merge_KeepsBothKnotsWhenTheTwoEndsDoNotMeet()
        {
            StreetSegment target = StreetTestFactory.Create("T", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment dragged = StreetTestFactory.Create(
                "D", new Vector3(0f, 0f, 30f), new Vector3(0f, 0f, 40f));

            StreetSegment survivor = StreetSurgery.Merge(dragged, StreetEnd.Start, target, StreetEnd.End);

            // Nothing is welded here: the gap between 10 and 30 is road that has to stay. This is what
            // taking a junction back out looks like - the two halves stand where its sockets were.
            Assert.AreEqual(new[] { 0f, 10f, 30f, 40f }, MergedPositions(survivor));
        }

        [Test]
        public void Merge_CarriesTheOuterSocketOver()
        {
            StreetSegment target = StreetTestFactory.Create("T", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment dragged = StreetTestFactory.Create(
                "D", new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 30f));

            GameObject socketObject = new GameObject("Socket");
            IntersectionSocket socket = socketObject.AddComponent<IntersectionSocket>();
            dragged.endConnection.socket = socket;

            StreetSegment survivor = StreetSurgery.Merge(dragged, StreetEnd.Start, target, StreetEnd.End);

            Assert.AreSame(socket, survivor.endConnection.socket);

            Object.DestroyImmediate(socketObject);
        }

        [Test]
        public void Merge_RefusesAndChangesNothingWhenTheWidthsDiffer()
        {
            StreetSegment target = StreetTestFactory.Create("T", Vector3.zero, new Vector3(0f, 0f, 10f));
            StreetSegment dragged = StreetTestFactory.Create(
                "D", new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 30f));
            dragged.roadWidth = 7f;

            Assert.IsNull(StreetSurgery.Merge(dragged, StreetEnd.Start, target, StreetEnd.End));
            Assert.IsFalse(dragged == null, "A refused merge must not destroy anything.");
            Assert.AreEqual(2, StreetSurgery.SplineOf(target).Count);
        }
```

`Merge_RefusesAndChangesNothingWhenTheWidthsDiffer` erzeugt eine Warnung im Log. Damit NUnit sie nicht als Fehler wertet, in dieser Testmethode als erste Zeile `LogAssert.ignoreFailingMessages = true;` setzen und `using UnityEngine.TestTools;` ergänzen.

- [ ] **Step 2: Tests laufen lassen und den Fehlschlag sehen**

Erwartet: kompiliert nicht, `StreetSurgery.Merge` fehlt.

- [ ] **Step 3: `Merge` schreiben**

In `StreetSurgery` einfügen (`using UnityEditor;` ergänzen). Zuerst die beiden Helfer:

```csharp
        /// <summary>How close two ends have to be before they count as the same point, in metres.</summary>
        private const float SeamEpsilon = 0.01f;

        private static Vector3 ToVector(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
```

Dann `Merge` selbst:

```csharp
        /// <summary>
        /// Joins two streets into one. The target survives, the dragged one disappears into it.
        /// </summary>
        /// <returns>The surviving segment, or null when the merge was refused.</returns>
        public static StreetSegment Merge(
            StreetSegment dragged,
            StreetEnd draggedEnd,
            StreetSegment target,
            StreetEnd targetEnd)
        {
            if (!CanMerge(dragged, target, out string problem))
            {
                Debug.LogWarning(problem, target);
                return null;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            SplineContainer targetContainer = target.GetComponent<SplineContainer>();
            SplineContainer draggedContainer = dragged.GetComponent<SplineContainer>();

            Undo.RecordObject(target, "Merge Streets");
            Undo.RecordObject(targetContainer, "Merge Streets");
            Undo.RecordObject(dragged, "Merge Streets");
            Undo.RecordObject(draggedContainer, "Merge Streets");

            // Reduced to one case instead of four: the target's end meets the dragged one's start.
            if (targetEnd == StreetEnd.Start)
            {
                Reverse(target);
            }

            if (draggedEnd == StreetEnd.End)
            {
                Reverse(dragged);
            }

            Spline targetSpline = SplineOf(target);
            Spline draggedSpline = SplineOf(dragged);

            int seam = targetSpline.Count - 1;

            // Two ends lying on each other are one knot; two that do not are a stretch of road that has
            // to stay. Taking a junction back out merges halves standing where its sockets were, and
            // welding those would swallow exactly the piece the junction had been standing on.
            Vector3 targetSeam = target.transform.TransformPoint(ToVector(targetSpline[seam].Position));
            Vector3 draggedSeam = dragged.transform.TransformPoint(ToVector(draggedSpline[0].Position));
            bool weld = Vector3.Distance(targetSeam, draggedSeam) < SeamEpsilon;

            // BezierKnot.Transform carries position, rotation and tangents across. The tangents sit in
            // the knot's own rotation space, and it rotates them along - writing that conversion here
            // by hand would only be a second, worse version of it.
            float4x4 matrix = math.mul(
                target.transform.worldToLocalMatrix,
                dragged.transform.localToWorldMatrix);

            for (int index = weld ? 1 : 0; index < draggedSpline.Count; index++)
            {
                targetSpline.Add(draggedSpline[index].Transform(matrix), draggedSpline.GetTangentMode(index));
            }

            if (weld)
            {
                // The one knot standing for both ends has to carry its tangents through, or the road
                // would kink where the two used to meet. Across a gap there is nothing to smooth.
                targetSpline.SetTangentMode(seam, TangentMode.Continuous);
            }

            // The outer end of the dragged street becomes the outer end of the joined one.
            target.endConnection = dragged.endConnection;

            // Anything hung on the dragged street - a sign, a lamp - would be destroyed with it.
            Transform draggedTransform = dragged.transform;
            for (int index = draggedTransform.childCount - 1; index >= 0; index--)
            {
                Undo.SetTransformParent(draggedTransform.GetChild(index), target.transform, "Merge Streets");
            }

            Undo.DestroyObjectImmediate(dragged.gameObject);

            EditorUtility.SetDirty(target);
            target.Rebuild();

            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Merge Streets");

            return target;
        }
```

- [ ] **Step 4: Kompilieren und Tests laufen lassen**

Erwartet: fünfzehn grüne Tests.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Streets/Editor/StreetSurgery.cs" "Assets/Scripts/Streets/Editor/Tests/StreetSurgeryTests.cs"
git commit -m "feat(streets): merge two connected segments into one spline"
```

---

### Task 4: Die Verbindungsgeste löst das Verschmelzen aus

**Files:**
- Modify: `Assets/Scripts/Streets/Editor/StreetSnapping.cs`
- Modify: `Assets/Scripts/Streets/Editor/IntersectionSocketDragging.cs`

**Interfaces:**
- Consumes: `StreetSurgery.Merge`
- Produces: `StreetSnapping.Connect(...)` gibt jetzt `StreetSegment` zurück — das Segment, das die Verbindung danach trägt, oder `null` wenn nichts geschah.

- [ ] **Step 1: `Connect` umbauen**

In `StreetSnapping.Connect` den Rückgabetyp von `void` auf `StreetSegment` ändern und den Segment-Zweig ersetzen. Der Socket-Zweig bleibt, wie er ist:

```csharp
        /// <summary>
        /// Docks one end of a segment to a target. Docking to another segment merges the two.
        /// </summary>
        /// <returns>
        /// The segment that carries the connection afterwards. Merging returns the survivor, which is
        /// not the segment that was passed in. Null when nothing happened.
        /// </returns>
        public static StreetSegment Connect(StreetSegment segment, StreetEnd end, StreetSnapTarget target)
        {
            if (!target.IsValid)
            {
                return null;
            }

            // Two streets meeting no longer stay two objects that reference each other - they become
            // one street with one spline.
            if (target.socket == null)
            {
                return StreetSurgery.Merge(segment, end, target.segment, target.segmentEnd);
            }

            Undo.RecordObject(segment, "Connect Street");

            StreetEndConnector connector = segment.ConnectorAt(end);
            connector.Clear();
            connector.driven = true;
            connector.socket = target.socket;

            EditorUtility.SetDirty(segment);
            segment.Rebuild();

            return segment;
        }
```

Der bisherige Block, der den Gegenpart auf `target.segment` schrieb, entfällt damit. `StreetEndConnector.segment`, `segmentEnd` und `driven` bleiben als Felder stehen — Task 11 legt sie einzeln vor.

- [ ] **Step 2: Den Aufrufer in `IntersectionSocketDragging` nachziehen**

In `CreateStreet` den Block ab `StreetSnapping.Connect(segment, StreetEnd.Start, …)` ersetzen:

```csharp
            StreetSnapping.Connect(
                segment,
                StreetEnd.Start,
                new StreetSnapTarget { socket = socket, position = socket.transform.position });

            // Docking the far end to another street merges the two, and the object that survives is
            // the other one. Everything after this point has to talk about the survivor.
            StreetSegment survivor = segment;
            if (farEnd.IsValid)
            {
                survivor = StreetSnapping.Connect(segment, StreetEnd.End, farEnd) ?? segment;
            }

            survivor.Rebuild();
            Selection.activeGameObject = survivor.gameObject;

            // Hand straight over to Unity's own draw tool so the road can be carried on without
            // switching tools by hand. Only while the far end is still open: past a docked end the new
            // last knot would be the one the connector drives, and the seam would come apart.
            if (!farEnd.IsValid)
            {
                EditorSplineUtility.SetKnotPlacementTool();
            }
```

- [ ] **Step 3: Den End-Anfasser gegen das eigene Verschwinden absichern**

`StreetSegmentEditor.DrawEndHandle` ruft `Connect` und zeichnet danach weiter auf `segment`. Wenn dieser Aufruf jetzt verschmilzt, ist `segment` in diesem Moment zerstört und der Custom Editor arbeitet auf einem toten Objekt weiter. In `DrawEndHandle` den Commit-Block ersetzen:

```csharp
            if (Event.current.type == EventType.MouseUp)
            {
                isDragging = false;

                if (!candidate.IsValid)
                {
                    StreetSnapping.Disconnect(segment, end);
                    return;
                }

                StreetSegment survivor = StreetSnapping.Connect(segment, end, candidate);

                // Docking to another street merges the two, and this editor's own target is the one
                // that goes. Anything drawn after this point would touch a destroyed object.
                if (survivor != null && survivor != segment)
                {
                    Selection.activeGameObject = survivor.gameObject;
                    return;
                }
            }
```

Der Anfasser allein reicht nicht. `OnSceneGUI` ruft `DrawEndHandle` zweimal, und `DrawEndHandle` liest `segment.EndPosition(end)` ganz oben, vor jeder Absicherung. Verschmilzt der Start-Aufruf, läuft der End-Aufruf auf dem zerstörten Objekt. Deshalb zusätzlich in `OnSceneGUI` zwischen die beiden Aufrufe:

```csharp
            DrawEndHandle(segment, StreetEnd.Start);

            // A merge committed by the Start handle destroys this editor's own target, and the End
            // handle would then read a destroyed object. Unity's overloaded == reports that.
            if (segment == null)
            {
                return;
            }

            DrawEndHandle(segment, StreetEnd.End);
```

Jede weitere Zeichnung, die später hier angehängt wird, gehört unter diese Absicherung.

- [ ] **Step 4: Kompilieren**

`dotnet build`. Erwartet: erfolgreich.

- [ ] **Step 5: Von Hand prüfen — dem Nutzer vorlegen**

1. Zwei Straßen mit gleicher Breite und gleicher Kachel nebeneinander legen.
2. Den Anfasser am Ende der einen auf das Ende der anderen ziehen und loslassen.
3. Erwartet: **ein** Objekt in der Hierarchie, ein Spline mit den Knoten beider, keine Knickstelle an der Naht.
4. Ctrl+Z. Erwartet: beide Objekte wieder da, jedes mit seiner alten Spline.
5. Bei einer der beiden `roadWidth` auf 7 stellen und erneut ziehen. Erwartet: eine Warnung in der Konsole, die beide Breiten nennt, und nichts verändert sich.
6. Aus einem Kreuzungs-Socket eine Straße auf eine bestehende Straße ziehen. Erwartet: die bestehende Straße ist verlängert, sie ist selektiert, und das Zeichenwerkzeug ist **nicht** aktiv.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/Streets/Editor/StreetSnapping.cs" \
        "Assets/Scripts/Streets/Editor/IntersectionSocketDragging.cs" \
        "Assets/Scripts/Streets/Editor/StreetSegmentEditor.cs"
git commit -m "feat(streets): connecting two streets merges them"
```

---

### Task 5: Eine Straße an einem Knoten trennen

**Files:**
- Modify: `Assets/Scripts/Streets/Editor/StreetSurgery.cs`
- Modify: `Assets/Scripts/Streets/Editor/Tests/StreetSurgeryTests.cs`

**Interfaces:**
- Consumes: `StreetSurgery.SplineOf`
- Produces: `StreetSurgery.Split(StreetSegment segment, int knotIndex) → StreetSegment` (die zweite Hälfte, oder `null`)
- Produces: `StreetSurgery.CanSplit(StreetSegment segment, int knotIndex, out string problem) → bool`

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

```csharp
        [Test]
        public void Split_GivesEachHalfItsShareOfTheKnots()
        {
            StreetSegment segment = StreetTestFactory.Create(
                "A",
                Vector3.zero,
                new Vector3(0f, 0f, 10f),
                new Vector3(0f, 0f, 20f),
                new Vector3(0f, 0f, 30f));

            StreetSegment second = StreetSurgery.Split(segment, 1);

            Assert.IsNotNull(second);
            Assert.AreEqual(2, StreetSurgery.SplineOf(segment).Count);
            Assert.AreEqual(3, StreetSurgery.SplineOf(second).Count);
            Assert.AreEqual(10f, StreetSurgery.SplineOf(segment)[1].Position.z, 0.001f);
            Assert.AreEqual(10f, StreetSurgery.SplineOf(second)[0].Position.z, 0.001f);
        }

        [Test]
        public void Split_LeavesBothNewEndsOpen()
        {
            StreetSegment segment = StreetTestFactory.Create(
                "A", Vector3.zero, new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 20f));

            StreetSegment second = StreetSurgery.Split(segment, 1);

            Assert.IsFalse(segment.endConnection.IsConnected);
            Assert.IsFalse(second.startConnection.IsConnected);
        }

        [Test]
        public void Split_HandsTheOuterConnectionToTheSecondHalf()
        {
            StreetSegment segment = StreetTestFactory.Create(
                "A", Vector3.zero, new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 20f));

            GameObject socketObject = new GameObject("Socket");
            IntersectionSocket socket = socketObject.AddComponent<IntersectionSocket>();
            segment.endConnection.socket = socket;

            StreetSegment second = StreetSurgery.Split(segment, 1);

            Assert.AreSame(socket, second.endConnection.socket);
            Assert.IsNull(segment.endConnection.socket);

            Object.DestroyImmediate(socketObject);
        }

        [Test]
        public void Split_RefusesTheEndKnots()
        {
            StreetSegment segment = StreetTestFactory.Create(
                "A", Vector3.zero, new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 20f));

            Assert.IsFalse(StreetSurgery.CanSplit(segment, 0, out _));
            Assert.IsFalse(StreetSurgery.CanSplit(segment, 2, out _));
            Assert.IsTrue(StreetSurgery.CanSplit(segment, 1, out _));
        }
```

- [ ] **Step 2: Tests laufen lassen und den Fehlschlag sehen**

- [ ] **Step 3: `CanSplit` und `Split` schreiben**

```csharp
        /// <summary>
        /// Whether a street can be cut at this knot. Only the inner knots qualify - cutting at an end
        /// would produce a half with no length.
        /// </summary>
        public static bool CanSplit(StreetSegment segment, int knotIndex, out string problem)
        {
            problem = null;

            Spline spline = SplineOf(segment);
            if (spline == null || spline.Count < 3)
            {
                problem = "A street needs at least three knots before it can be cut.";
                return false;
            }

            if (knotIndex < 1 || knotIndex > spline.Count - 2)
            {
                problem = "A street can only be cut at one of its inner knots.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Cuts a street in two at one of its knots. The knot itself is duplicated, so each half keeps
        /// an end there. Both new ends are open.
        /// </summary>
        /// <returns>The second half, or null when the cut was refused.</returns>
        public static StreetSegment Split(StreetSegment segment, int knotIndex)
        {
            if (!CanSplit(segment, knotIndex, out string problem))
            {
                Debug.LogWarning(problem, segment);
                return null;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            Spline spline = SplineOf(segment);
            SplineContainer container = segment.GetComponent<SplineContainer>();

            Undo.RecordObject(segment, "Split Street");
            Undo.RecordObject(container, "Split Street");

            GameObject secondObject = new GameObject($"{segment.name} (2)");
            Undo.RegisterCreatedObjectUndo(secondObject, "Split Street");

            secondObject.transform.SetParent(segment.transform.parent, false);

            // Placed exactly on the original, so the knots carry over without any conversion at all.
            secondObject.transform.SetPositionAndRotation(
                segment.transform.position, segment.transform.rotation);

            SplineContainer secondContainer = secondObject.AddComponent<SplineContainer>();
            Spline secondSpline = secondContainer.Spline;
            secondSpline.Clear();

            for (int index = knotIndex; index < spline.Count; index++)
            {
                secondSpline.Add(spline[index], spline.GetTangentMode(index));
            }

            StreetSegment second = secondObject.AddComponent<StreetSegment>();
            second.roadWidth = segment.roadWidth;
            second.sourceMesh = segment.sourceMesh;
            second.forwardAxis = segment.forwardAxis;
            second.tileLength = segment.tileLength;
            second.generateCollider = segment.generateCollider;
            second.curvatureWarningRadius = segment.curvatureWarningRadius;

            MeshRenderer source = segment.GetComponent<MeshRenderer>();
            MeshRenderer destination = second.GetComponent<MeshRenderer>();
            if (source != null && destination != null)
            {
                destination.sharedMaterials = source.sharedMaterials;
            }

            // The far end of the road is now the far end of the second half.
            second.endConnection = segment.endConnection;
            second.startConnection = new StreetEndConnector();
            segment.endConnection = new StreetEndConnector();

            for (int index = spline.Count - 1; index > knotIndex; index--)
            {
                spline.RemoveAt(index);
            }

            EditorUtility.SetDirty(segment);
            segment.Rebuild();
            second.Rebuild();

            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Split Street");

            return second;
        }
```

- [ ] **Step 4: Kompilieren und Tests laufen lassen**

Erwartet: neunzehn grüne Tests.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Streets/Editor/StreetSurgery.cs" "Assets/Scripts/Streets/Editor/Tests/StreetSurgeryTests.cs"
git commit -m "feat(streets): split a street at one of its knots"
```

---

### Task 6: Anfasser an den inneren Knoten mit „Split here"

**Files:**
- Create: `Assets/Scripts/Streets/Editor/StreetKnotHandles.cs`
- Modify: `Assets/Scripts/Streets/Editor/StreetSegmentEditor.cs`

**Interfaces:**
- Consumes: `StreetSurgery.SplineOf`, `StreetSurgery.CanSplit`, `StreetSurgery.Split`, `StreetDrawing.Enabled`
- Produces: `StreetKnotHandles.Draw(StreetSegment segment)`

- [ ] **Step 1: `StreetKnotHandles` schreiben**

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// A small handle on every inner knot of a street, and the menu it opens.
    ///
    /// This is the one place a road is cut or a junction is put into it, so both live on the same
    /// gesture. The end knots get nothing - there is no road on the far side of them to cut off.
    ///
    /// These handles are drawn only while <see cref="StreetDrawing"/> is on, which is what keeps them
    /// from fighting Unity's own knot handles for clicks.
    /// </summary>
    public static class StreetKnotHandles
    {
        private static readonly Color KnotColor = new Color(1f, 0.85f, 0.3f);

        /// <summary>Size of a knot button, as a share of the handle size at that distance.</summary>
        private const float ButtonSize = 0.06f;

        public static void Draw(StreetSegment segment)
        {
            Spline spline = StreetSurgery.SplineOf(segment);
            if (spline == null || spline.Count < 3)
            {
                return;
            }

            Handles.color = KnotColor;

            for (int index = 1; index < spline.Count - 1; index++)
            {
                Vector3 position = segment.transform.TransformPoint(
                    new Vector3(spline[index].Position.x, spline[index].Position.y, spline[index].Position.z));

                float size = HandleUtility.GetHandleSize(position) * ButtonSize;

                if (Handles.Button(position, Quaternion.identity, size, size * 2f, Handles.DotHandleCap))
                {
                    ShowMenu(segment, index);
                }
            }
        }

        private static void ShowMenu(StreetSegment segment, int knotIndex)
        {
            GenericMenu menu = new GenericMenu();

            if (StreetSurgery.CanSplit(segment, knotIndex, out string problem))
            {
                menu.AddItem(new GUIContent("Split here"), false, () => StreetSurgery.Split(segment, knotIndex));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent($"Split here - {problem}"));
            }

            menu.ShowAsContext();
        }
    }
}
```

- [ ] **Step 2: Aus `StreetSegmentEditor` aufrufen**

In `StreetSegmentEditor.OnSceneGUI`, nach `DrawEndHandle(segment, StreetEnd.End);`:

```csharp
            StreetKnotHandles.Draw(segment);
```

- [ ] **Step 3: Kompilieren**

`dotnet build`. Neue Datei — gegebenenfalls die `Compile`-Zeile einfügen und danach zurücknehmen.

- [ ] **Step 4: Von Hand prüfen — dem Nutzer vorlegen**

1. Eine Straße mit mindestens drei Knoten anlegen und selektieren.
2. Erwartet: ein bernsteinfarbener Punkt auf jedem inneren Knoten, keiner auf den beiden Endknoten.
3. Auf einen Punkt klicken. Erwartet: ein Kontextmenü mit „Split here".
4. Auswählen. Erwartet: zwei Objekte in der Hierarchie, die zusammen dieselbe Strecke abdecken, beide neuen Enden offen (rote Kugel im Scene View).
5. Ctrl+Z. Erwartet: wieder eine Straße.
6. `Tools ▸ CargoKing ▸ Street Drawings` ausschalten. Erwartet: die Punkte verschwinden und Unitys Knoten-Handles lassen sich ungestört bedienen.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Streets/Editor/StreetKnotHandles.cs" "Assets/Scripts/Streets/Editor/StreetSegmentEditor.cs"
git commit -m "feat(streets): knot handles with split action"
```

---

### Task 7: `StreetKit` — die Palette der Kreuzungen

**Files:**
- Create: `Assets/Scripts/Streets/Editor/StreetKit.cs`
- Create: `Assets/Scripts/Streets/Editor/Tests/StreetKitTests.cs`

**Interfaces:**
- Produces: `StreetKit.intersections` (`List<GameObject>`), `StreetKit.IsValidEntry(GameObject) → bool`, `StreetKit.Find(out string problem) → StreetKit`, `StreetKit.CreateSeeded() → StreetKit`

- [ ] **Step 1: Den fehlschlagenden Test schreiben**

`Assets/Scripts/Streets/Editor/Tests/StreetKitTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetKitTests
    {
        [Test]
        public void IsValidEntry_AcceptsAnObjectWithAnIntersectionOnItsRoot()
        {
            GameObject candidate = new GameObject("Junction");
            candidate.AddComponent<Intersection>();

            Assert.IsTrue(StreetKit.IsValidEntry(candidate));

            Object.DestroyImmediate(candidate);
        }

        [Test]
        public void IsValidEntry_RefusesAnObjectWithoutOne()
        {
            GameObject candidate = new GameObject("Not a junction");

            Assert.IsFalse(StreetKit.IsValidEntry(candidate));

            Object.DestroyImmediate(candidate);
        }

        [Test]
        public void IsValidEntry_RefusesNull()
        {
            Assert.IsFalse(StreetKit.IsValidEntry(null));
        }
    }
}
```

- [ ] **Step 2: Tests laufen lassen und den Fehlschlag sehen**

- [ ] **Step 3: `StreetKit` schreiben**

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// The list of intersection prefabs the knot menu offers.
    ///
    /// Listed rather than scanned. Scanning a folder every time would put every test leftover in the
    /// palette and would keep doing it. The one scan this asset does happens when it is created, as a
    /// convenience, and never again - after that the list is curated by hand.
    /// </summary>
    [CreateAssetMenu(fileName = "StreetKit", menuName = "CargoKing/Street Kit")]
    public class StreetKit : ScriptableObject
    {
        [Tooltip("Intersection prefabs offered when a knot is replaced by a junction.")]
        public List<GameObject> intersections = new List<GameObject>();

        /// <summary>Whether an object can serve as a junction: an Intersection on its root.</summary>
        public static bool IsValidEntry(GameObject candidate)
        {
            return candidate != null && candidate.GetComponent<Intersection>() != null;
        }

        /// <summary>
        /// The kit this project uses, or null when there is none yet.
        ///
        /// Sorted by GUID so the choice is the same in every session. More than one kit is not an
        /// error we can resolve, so it is reported rather than guessed at.
        /// </summary>
        public static StreetKit Find(out string problem)
        {
            problem = null;

            string[] guids = AssetDatabase.FindAssets("t:StreetKit");
            if (guids.Length == 0)
            {
                return null;
            }

            System.Array.Sort(guids, System.StringComparer.Ordinal);

            if (guids.Length > 1)
            {
                problem = $"There are {guids.Length} StreetKit assets in this project. "
                    + $"'{AssetDatabase.GUIDToAssetPath(guids[0])}' is being used.";
            }

            return AssetDatabase.LoadAssetAtPath<StreetKit>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>
        /// Creates the kit and fills it once from what the project already holds, so the palette is
        /// not empty on the first use.
        /// </summary>
        public static StreetKit CreateSeeded()
        {
            StreetKit kit = CreateInstance<StreetKit>();

            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (IsValidEntry(prefab))
                {
                    kit.intersections.Add(prefab);
                }
            }

            // A unique path, not a fixed one. CreateAsset onto a taken path logs an error and quietly
            // fails to persist, and this method would still hand back the in-memory object as though
            // it had worked - a kit that vanishes on the next reload, after junctions were inserted
            // from it. If the path is taken by something that is not a kit, Find would not have seen
            // it, so stepping aside is the right move.
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/StreetKit.asset");

            AssetDatabase.CreateAsset(kit, path);
            AssetDatabase.SaveAssets();

            return kit;
        }
    }
}
```

- [ ] **Step 4: Kompilieren und Tests laufen lassen**

Erwartet: zweiundzwanzig grüne Tests.

- [ ] **Step 5: Die Einträge im Inspector prüfen lassen**

Die Spec verlangt, dass ein Prefab ohne `Intersection` gemeldet wird. Ans Ende von `StreetKit.cs`:

```csharp
    /// <summary>
    /// Says which entries of a kit cannot serve as a junction. A list of prefab slots gives no hint
    /// by itself that one of them is the wrong kind of prefab.
    /// </summary>
    [CustomEditor(typeof(StreetKit))]
    public class StreetKitEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            StreetKit kit = (StreetKit)target;

            for (int index = 0; index < kit.intersections.Count; index++)
            {
                GameObject entry = kit.intersections[index];

                if (entry == null)
                {
                    EditorGUILayout.HelpBox($"Entry {index} is empty.", MessageType.Warning);
                }
                else if (!StreetKit.IsValidEntry(entry))
                {
                    EditorGUILayout.HelpBox(
                        $"'{entry.name}' has no Intersection component on its root, so no street can "
                        + "dock to it.",
                        MessageType.Warning);
                }
            }
        }
    }
```

- [ ] **Step 6: Von Hand prüfen — dem Nutzer vorlegen**

`Assets ▸ Create ▸ CargoKing ▸ Street Kit`. Erwartet: ein Asset, dessen Liste sich mit `Intersection_T` und `Intersection_full` füllen lässt. Ein beliebiges anderes Prefab hineinziehen — erwartet: eine Warnung im Inspector, die es beim Namen nennt. (Das Vorbefüllen über `CreateSeeded` prüft Task 9, das ruft es auf.)

- [ ] **Step 7: Commit**

```bash
git add "Assets/Scripts/Streets/Editor/StreetKit.cs" "Assets/Scripts/Streets/Editor/Tests/StreetKitTests.cs"
git commit -m "feat(streets): street kit asset listing junction prefabs"
```

---

### Task 8: Die Ausrichtungsrechnung

**Files:**
- Create: `Assets/Scripts/Streets/Editor/JunctionPlacement.cs`
- Create: `Assets/Scripts/Streets/Editor/Tests/JunctionPlacementTests.cs`

**Interfaces:**
- Produces: `struct JunctionAlignment { IntersectionSocket entry; IntersectionSocket exit; Vector3 position; Quaternion rotation; float socketOffset; }`
- Produces: `JunctionPlacement.TryAlign(GameObject junction, Vector3 knotPosition, Vector3 roadDirection, Vector3 roadUp, bool flipped, out JunctionAlignment alignment, out string problem) → bool`

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

`Assets/Scripts/Streets/Editor/Tests/JunctionPlacementTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class JunctionPlacementTests
    {
        private GameObject junction;

        /// <summary>
        /// A crossing with four arms 9.5 m out, the way the project's prefabs are built: each socket
        /// looks away from the middle.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            junction = new GameObject("Junction");
            junction.AddComponent<Intersection>();

            AddSocket(new Vector3(0f, 0f, 9.5f), Vector3.forward);
            AddSocket(new Vector3(9.5f, 0f, 0f), Vector3.right);
            AddSocket(new Vector3(0f, 0f, -9.5f), Vector3.back);
            AddSocket(new Vector3(-9.5f, 0f, 0f), Vector3.left);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(junction);
        }

        private void AddSocket(Vector3 localPosition, Vector3 outward)
        {
            GameObject socket = new GameObject("Socket");
            socket.transform.SetParent(junction.transform, false);
            socket.transform.localPosition = localPosition;
            socket.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);
            socket.AddComponent<IntersectionSocket>().roadWidth = 16f;
        }

        [Test]
        public void TryAlign_PutsTheEntrySocketAgainstTheDirectionOfTravel()
        {
            Assert.IsTrue(JunctionPlacement.TryAlign(
                junction,
                new Vector3(100f, 0f, 50f),
                Vector3.forward,
                Vector3.up,
                false,
                out JunctionAlignment alignment,
                out string problem), problem);

            // The road arrives travelling +Z, so the socket it docks to has to look back down it.
            Vector3 entryOutward = alignment.rotation * alignment.entry.transform.localRotation * Vector3.forward;
            Assert.AreEqual(-1f, Vector3.Dot(entryOutward.normalized, Vector3.forward), 0.001f);
        }

        [Test]
        public void TryAlign_PutsTheMidpointOfThePairOnTheKnot()
        {
            Vector3 knot = new Vector3(100f, 0f, 50f);

            Assert.IsTrue(JunctionPlacement.TryAlign(
                junction, knot, Vector3.forward, Vector3.up, false, out JunctionAlignment alignment, out _));

            Vector3 entry = alignment.position + alignment.rotation * alignment.entry.transform.localPosition;
            Vector3 exit = alignment.position + alignment.rotation * alignment.exit.transform.localPosition;

            Assert.AreEqual(0f, Vector3.Distance((entry + exit) * 0.5f, knot), 0.001f);
        }

        [Test]
        public void TryAlign_ReportsTheDistanceFromTheMiddleToASocket()
        {
            Assert.IsTrue(JunctionPlacement.TryAlign(
                junction, Vector3.zero, Vector3.forward, Vector3.up, false, out JunctionAlignment alignment, out _));

            Assert.AreEqual(9.5f, alignment.socketOffset, 0.001f);
        }

        [Test]
        public void TryAlign_SwapsEntryAndExitWhenFlipped()
        {
            JunctionPlacement.TryAlign(
                junction, Vector3.zero, Vector3.forward, Vector3.up, false, out JunctionAlignment straight, out _);
            JunctionPlacement.TryAlign(
                junction, Vector3.zero, Vector3.forward, Vector3.up, true, out JunctionAlignment flipped, out _);

            Assert.AreSame(straight.entry, flipped.exit);
            Assert.AreSame(straight.exit, flipped.entry);
        }

        [Test]
        public void TryAlign_RefusesAJunctionWithoutAnOpposingPair()
        {
            GameObject bare = new GameObject("Bare");
            bare.AddComponent<Intersection>();

            Assert.IsFalse(JunctionPlacement.TryAlign(
                bare, Vector3.zero, Vector3.forward, Vector3.up, false, out _, out string problem));
            Assert.IsNotNull(problem);

            Object.DestroyImmediate(bare);
        }
    }
}
```

- [ ] **Step 2: Tests laufen lassen und den Fehlschlag sehen**

- [ ] **Step 3: `JunctionPlacement` schreiben**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>Where a junction goes at a knot, and which of its sockets the two halves dock to.</summary>
    public struct JunctionAlignment
    {
        /// <summary>Socket the half arriving at the knot docks to. Looks back down the road.</summary>
        public IntersectionSocket entry;

        /// <summary>Socket the half leaving the knot docks to.</summary>
        public IntersectionSocket exit;

        /// <summary>World position for the junction's root.</summary>
        public Vector3 position;

        /// <summary>World rotation for the junction's root.</summary>
        public Quaternion rotation;

        /// <summary>Distance from the middle of the through pair to either of its sockets, in metres.</summary>
        public float socketOffset;
    }

    /// <summary>
    /// Works out how a junction prefab has to sit to replace a knot on a street.
    ///
    /// Pure arithmetic: it reads the prefab's sockets and returns numbers. Nothing is instantiated,
    /// nothing in the scene is touched, so the part of junction insertion that is easy to get subtly
    /// wrong is also the part that can be tested on its own.
    /// </summary>
    public static class JunctionPlacement
    {
        /// <summary>How nearly opposite two sockets have to look to count as a way through.</summary>
        private const float OpposingThreshold = -0.9f;

        public static bool TryAlign(
            GameObject junction,
            Vector3 knotPosition,
            Vector3 roadDirection,
            Vector3 roadUp,
            bool flipped,
            out JunctionAlignment alignment,
            out string problem)
        {
            alignment = default;
            problem = null;

            if (junction == null)
            {
                problem = "No junction prefab.";
                return false;
            }

            List<IntersectionSocket> sockets = new List<IntersectionSocket>();
            junction.GetComponentsInChildren(false, sockets);

            if (sockets.Count < 2)
            {
                problem = $"'{junction.name}' has fewer than two active sockets.";
                return false;
            }

            Transform root = junction.transform;

            // The way through is the pair that looks most nearly opposite. On a crossing there are two
            // such pairs and they are interchangeable; on a T junction there is exactly one, and the
            // socket left over is the stem.
            IntersectionSocket first = null;
            IntersectionSocket second = null;
            float bestDot = OpposingThreshold;

            for (int a = 0; a < sockets.Count; a++)
            {
                Vector3 outwardA = root.InverseTransformDirection(sockets[a].Outward).normalized;

                for (int b = a + 1; b < sockets.Count; b++)
                {
                    Vector3 outwardB = root.InverseTransformDirection(sockets[b].Outward).normalized;
                    float dot = Vector3.Dot(outwardA, outwardB);

                    if (dot < bestDot)
                    {
                        bestDot = dot;
                        first = sockets[a];
                        second = sockets[b];
                    }
                }
            }

            if (first == null)
            {
                problem = $"'{junction.name}' has no two sockets facing opposite ways, so no street can "
                    + "pass through it.";
                return false;
            }

            IntersectionSocket entry = flipped ? second : first;
            IntersectionSocket exit = flipped ? first : second;

            Vector3 entryPositionLocal = root.InverseTransformPoint(entry.transform.position);
            Vector3 exitPositionLocal = root.InverseTransformPoint(exit.transform.position);
            Vector3 entryOutwardLocal = root.InverseTransformDirection(entry.Outward).normalized;
            Vector3 entryUpLocal = root.InverseTransformDirection(entry.transform.up).normalized;

            // The entry socket has to end up looking back down the road the traffic came from.
            Quaternion rotation = Quaternion.LookRotation(-roadDirection.normalized, roadUp)
                * Quaternion.Inverse(Quaternion.LookRotation(entryOutwardLocal, entryUpLocal));

            Vector3 middleLocal = (entryPositionLocal + exitPositionLocal) * 0.5f;

            alignment = new JunctionAlignment
            {
                entry = entry,
                exit = exit,
                rotation = rotation,
                position = knotPosition - rotation * middleLocal,
                socketOffset = Vector3.Distance(entryPositionLocal, middleLocal),
            };

            return true;
        }
    }
}
```

- [ ] **Step 4: Kompilieren und Tests laufen lassen**

Erwartet: achtundzwanzig grüne Tests.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Streets/Editor/JunctionPlacement.cs" "Assets/Scripts/Streets/Editor/Tests/JunctionPlacementTests.cs"
git commit -m "feat(streets): compute where a junction sits at a knot"
```

---

### Task 9: Die Kreuzung einsetzen

**Files:**
- Create: `Assets/Scripts/Streets/Editor/JunctionInsertion.cs`
- Modify: `Assets/Scripts/Streets/Editor/StreetKnotHandles.cs`

**Interfaces:**
- Consumes: `StreetSurgery.Split`, `JunctionPlacement.TryAlign`, `StreetKit.Find`, `StreetKit.CreateSeeded`, `StreetSnapping.Connect`
- Produces: `JunctionInsertion.CanInsert(StreetSegment, int, GameObject, out string) → bool`, `JunctionInsertion.Insert(StreetSegment, int, GameObject) → Intersection`

- [ ] **Step 1: `JunctionInsertion` schreiben**

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Replaces a knot on a street with a junction: cut the spline there, put the prefab in the gap,
    /// dock both halves to it.
    ///
    /// The halves then retreat onto their sockets by themselves, because a docked end is driven by
    /// what it docks to. That is what makes the junction eat exactly the stretch of road it stands on
    /// without a line of code to move anything.
    /// </summary>
    public static class JunctionInsertion
    {
        /// <summary>Where a knot sits and which way the road runs there, in world space.</summary>
        private struct KnotPose
        {
            public Vector3 position;
            public Vector3 direction;
            public Vector3 up;
        }

        /// <summary>
        /// The pose at one knot. Both the check and the insertion need it, and they have to agree -
        /// a check that measures a different place than the one that gets built is worse than none.
        /// </summary>
        private static KnotPose PoseAt(StreetSegment segment, int knotIndex)
        {
            Spline spline = StreetSurgery.SplineOf(segment);
            Transform transform = segment.transform;

            float3 position = spline[knotIndex].Position;

            // The tangent of the curve leaving this knot, read at its own start. Not
            // EvaluateTangent(knotIndex / (Count - 1)): spline space is normalised by ARC LENGTH, so
            // that quotient only lands on the knot when every curve happens to be equally long. On a
            // real street it would read the tangent somewhere else entirely and aim the junction
            // askew. knotIndex is always an inner knot, so curve knotIndex always exists.
            float3 tangent = CurveUtility.EvaluateTangent(spline.GetCurve(knotIndex), 0f);

            Vector3 direction = transform.TransformDirection(
                new Vector3(tangent.x, tangent.y, tangent.z));

            return new KnotPose
            {
                position = transform.TransformPoint(new Vector3(position.x, position.y, position.z)),
                direction = direction.sqrMagnitude > 0.000001f ? direction.normalized : transform.forward,
                up = transform.up,
            };
        }

        /// <summary>
        /// Whether a junction fits at this knot, and what is in the way when it does not.
        /// </summary>
        public static bool CanInsert(
            StreetSegment segment,
            int knotIndex,
            GameObject junction,
            out string problem)
        {
            if (!StreetSurgery.CanSplit(segment, knotIndex, out problem))
            {
                return false;
            }

            Spline spline = StreetSurgery.SplineOf(segment);
            KnotPose pose = PoseAt(segment, knotIndex);

            if (!JunctionPlacement.TryAlign(
                junction,
                pose.position,
                pose.direction,
                pose.up,
                false,
                out JunctionAlignment alignment,
                out problem))
            {
                return false;
            }

            if (alignment.entry != null
                && !Mathf.Approximately(alignment.entry.roadWidth, segment.roadWidth))
            {
                problem = $"The junction's sockets are {alignment.entry.roadWidth:0.0} m wide and this "
                    + $"street is {segment.roadWidth:0.0} m. The lanes would not line up.";
                return false;
            }

            // Both halves have to be longer than the stretch the junction takes for itself, or the
            // half would be pulled past its own far end and turn inside out.
            float before = LengthBetween(spline, 0, knotIndex);
            float after = LengthBetween(spline, knotIndex, spline.Count - 1);

            if (before <= alignment.socketOffset || after <= alignment.socketOffset)
            {
                problem = $"The junction needs more than {alignment.socketOffset:0.0} m of street on "
                    + $"each side of the knot; there are {before:0.0} m and {after:0.0} m.";
                return false;
            }

            return true;
        }

        /// <summary>Arc length of the spline between two knots, in metres.</summary>
        private static float LengthBetween(Spline spline, int fromKnot, int toKnot)
        {
            float length = 0f;

            for (int index = fromKnot; index < toKnot; index++)
            {
                length += spline.GetCurveLength(index);
            }

            return length;
        }

        /// <summary>
        /// Puts a junction where a knot was.
        /// </summary>
        /// <returns>The junction that was placed, or null when it was refused.</returns>
        public static Intersection Insert(StreetSegment segment, int knotIndex, GameObject junction)
        {
            if (!CanInsert(segment, knotIndex, junction, out string problem))
            {
                Debug.LogWarning(problem, segment);
                return null;
            }

            // Read before the split, because the split is what takes the knot apart.
            KnotPose pose = PoseAt(segment, knotIndex);

            if (!JunctionPlacement.TryAlign(
                junction, pose.position, pose.direction, pose.up, false,
                out JunctionAlignment alignment, out problem))
            {
                Debug.LogWarning(problem, segment);
                return null;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            StreetSegment second = StreetSurgery.Split(segment, knotIndex);
            if (second == null)
            {
                return null;
            }

            GameObject instance = PrefabUtility.IsPartOfPrefabAsset(junction)
                ? (GameObject)PrefabUtility.InstantiatePrefab(junction)
                : Object.Instantiate(junction);

            Undo.RegisterCreatedObjectUndo(instance, "Insert Junction");

            // A sibling of the road, never a child: from the third arm on it belongs to a third street
            // as well.
            instance.transform.SetParent(segment.transform.parent, false);
            instance.transform.SetPositionAndRotation(alignment.position, alignment.rotation);

            Intersection intersection = instance.GetComponent<Intersection>();
            intersection.Rebuild();

            // The alignment named sockets on the prefab; the docking has to use the ones on the copy.
            IntersectionSocket entry = FindSame(junction, instance, alignment.entry);
            IntersectionSocket exit = FindSame(junction, instance, alignment.exit);

            if (entry == null || exit == null)
            {
                // Undoing the whole group is the only honest way out: the road is already cut and the
                // junction already placed, and half a junction is worse than none.
                Debug.LogWarning(
                    $"The sockets of '{junction.name}' could not be found on the copy in the scene.",
                    segment);

                Undo.RevertAllDownToGroup(group);
                return null;
            }

            StreetSnapping.Connect(
                segment, StreetEnd.End, new StreetSnapTarget { socket = entry, position = entry.transform.position });
            StreetSnapping.Connect(
                second, StreetEnd.Start, new StreetSnapTarget { socket = exit, position = exit.transform.position });

            Selection.activeGameObject = instance;

            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Insert Junction");

            return intersection;
        }

        /// <summary>
        /// The socket on the copy that matches one on the prefab, found by the path down the hierarchy
        /// rather than by name - two arms of a junction are often called the same thing.
        /// </summary>
        private static IntersectionSocket FindSame(GameObject prefab, GameObject instance, IntersectionSocket socket)
        {
            string path = AnimationUtility.CalculateTransformPath(socket.transform, prefab.transform);
            Transform found = instance.transform.Find(path);

            return found != null ? found.GetComponent<IntersectionSocket>() : null;
        }
    }
}
```

`using Unity.Mathematics;` für `float3` ergänzen.

- [ ] **Step 2: Das Menü am Knoten erweitern**

In `StreetKnotHandles.ShowMenu`, nach dem „Split here"-Eintrag:

```csharp
            menu.AddSeparator(string.Empty);

            StreetKit kit = StreetKit.Find(out string kitProblem);
            if (kitProblem != null)
            {
                Debug.LogWarning(kitProblem);
            }

            if (kit == null)
            {
                menu.AddItem(
                    new GUIContent("Insert junction/Create a Street Kit first"),
                    false,
                    () => Selection.activeObject = StreetKit.CreateSeeded());
            }
            else if (kit.intersections.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("Insert junction/The Street Kit is empty"));
            }
            else
            {
                for (int index = 0; index < kit.intersections.Count; index++)
                {
                    GameObject junction = kit.intersections[index];
                    if (junction == null)
                    {
                        continue;
                    }

                    menu.AddItem(
                        new GUIContent($"Insert junction/{junction.name}"),
                        false,
                        () => JunctionInsertion.Insert(segment, knotIndex, junction));
                }
            }
```

- [ ] **Step 3: Kompilieren**

- [ ] **Step 4: Von Hand prüfen — dem Nutzer vorlegen**

1. Eine lange Straße mit mindestens drei Knoten anlegen, Kachel und Breite 16 wie die Sockets der Prefabs.
2. Auf einen inneren Knoten klicken, weit genug von beiden Enden. Erwartet: das Menü bietet „Insert junction ▸ Create a Street Kit first" an, wenn noch kein Kit da ist.
3. Anlegen lassen. Erwartet: `Assets/StreetKit.asset`, in der Liste stehen `Intersection_T` und `Intersection_full`.
4. Erneut auf den Knoten klicken und `Intersection_full` einsetzen.
5. Erwartet: die Kreuzung sitzt auf dem Knoten, ihre Durchfahrt liegt in Straßenrichtung, beide Straßenhälften enden an ihren Sockets, und zwischen Straße und Kreuzung ist keine Lücke und keine Stufe.
6. Ctrl+Z. Erwartet: eine Straße, keine Kreuzung.
7. Auf einen Knoten dicht am Straßenende klicken und einsetzen. Erwartet: eine Warnung, die die nötige und die vorhandene Länge nennt, und nichts passiert.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Streets/Editor/JunctionInsertion.cs" "Assets/Scripts/Streets/Editor/StreetKnotHandles.cs"
git commit -m "feat(streets): replace a knot with a junction prefab"
```

---

### Task 10: Umdrehen und Entfernen

**Files:**
- Modify: `Assets/Scripts/Streets/Editor/JunctionInsertion.cs`
- Modify: `Assets/Scripts/Streets/Editor/IntersectionEditor.cs`

**Interfaces:**
- Consumes: `StreetSurgery.Merge`, `StreetSurgery.CanMerge`, `StreetSnapping.Disconnect`, `StreetSegment.ConnectorAt`
- Produces: `JunctionInsertion.TryGetDockedPair(Intersection, out StreetSegment, out StreetEnd, out StreetSegment, out StreetEnd) → bool`, `JunctionInsertion.Flip(Intersection)`, `JunctionInsertion.Remove(Intersection) → StreetSegment`

- [ ] **Step 1: Die drei Methoden schreiben**

In `JunctionInsertion` einfügen:

```csharp
        /// <summary>
        /// The two streets docked to a junction, when there are exactly two.
        /// </summary>
        public static bool TryGetDockedPair(
            Intersection intersection,
            out StreetSegment first,
            out StreetEnd firstEnd,
            out StreetSegment second,
            out StreetEnd secondEnd)
        {
            first = null;
            second = null;
            firstEnd = StreetEnd.Start;
            secondEnd = StreetEnd.Start;

            StreetSegment[] segments = Object.FindObjectsByType<StreetSegment>(FindObjectsSortMode.InstanceID);

            for (int index = 0; index < segments.Length; index++)
            {
                StreetSegment segment = segments[index];

                for (int side = 0; side < 2; side++)
                {
                    StreetEnd end = side == 0 ? StreetEnd.Start : StreetEnd.End;
                    IntersectionSocket socket = segment.ConnectorAt(end).socket;

                    if (socket == null || socket.Owner != intersection)
                    {
                        continue;
                    }

                    if (first == null)
                    {
                        first = segment;
                        firstEnd = end;
                    }
                    else if (second == null)
                    {
                        second = segment;
                        secondEnd = end;
                    }
                    else
                    {
                        // Three or more streets: neither flipping nor removing has a defined meaning.
                        first = null;
                        second = null;
                        return false;
                    }
                }
            }

            return first != null && second != null;
        }

        /// <summary>
        /// Turns a junction half a turn about its up axis, so a T junction's stem changes sides.
        ///
        /// The two docked halves are driven by their sockets, so turning alone would drag each of them
        /// onto the other's place and the two would cross. Their socket references are exchanged along
        /// with the turn, which puts everything back where it was and moves only the stem.
        /// </summary>
        public static void Flip(Intersection intersection)
        {
            if (!TryGetDockedPair(intersection, out StreetSegment first, out StreetEnd firstEnd,
                out StreetSegment second, out StreetEnd secondEnd))
            {
                Debug.LogWarning(
                    "Flipping needs exactly two streets docked to this junction.", intersection);
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            Undo.RecordObject(intersection.transform, "Flip Junction");
            Undo.RecordObject(first, "Flip Junction");
            Undo.RecordObject(second, "Flip Junction");

            intersection.transform.Rotate(intersection.transform.up, 180f, Space.World);

            IntersectionSocket firstSocket = first.ConnectorAt(firstEnd).socket;
            first.ConnectorAt(firstEnd).socket = second.ConnectorAt(secondEnd).socket;
            second.ConnectorAt(secondEnd).socket = firstSocket;

            intersection.Rebuild();
            first.Rebuild();
            second.Rebuild();

            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Flip Junction");
        }

        /// <summary>
        /// Takes a junction out again and closes the road over it.
        /// </summary>
        /// <returns>The joined street, or null when it was refused.</returns>
        public static StreetSegment Remove(Intersection intersection)
        {
            if (!TryGetDockedPair(intersection, out StreetSegment first, out StreetEnd firstEnd,
                out StreetSegment second, out StreetEnd secondEnd))
            {
                Debug.LogWarning(
                    "Removing needs exactly two streets docked to this junction.", intersection);
                return null;
            }

            if (!StreetSurgery.CanMerge(second, first, out string problem))
            {
                Debug.LogWarning(problem, intersection);
                return null;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            StreetSnapping.Disconnect(first, firstEnd);
            StreetSnapping.Disconnect(second, secondEnd);

            Undo.DestroyObjectImmediate(intersection.gameObject);

            StreetSegment survivor = StreetSurgery.Merge(second, secondEnd, first, firstEnd);

            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Remove Junction");

            return survivor;
        }
```

- [ ] **Step 2: Die Knöpfe in `IntersectionEditor` einbauen**

In `OnInspectorGUI`, nach der HelpBox:

```csharp
            Intersection self = intersection;
            bool hasPair = JunctionInsertion.TryGetDockedPair(self, out _, out _, out _, out _);

            using (new EditorGUI.DisabledScope(!hasPair))
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Flip"))
                {
                    JunctionInsertion.Flip(self);
                }

                if (GUILayout.Button("Remove and close the road"))
                {
                    JunctionInsertion.Remove(self);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (!hasPair)
            {
                EditorGUILayout.HelpBox(
                    "Flipping and removing need exactly two streets docked to this junction.",
                    MessageType.None);
            }
```

- [ ] **Step 3: Kompilieren**

- [ ] **Step 4: Von Hand prüfen — dem Nutzer vorlegen**

1. Eine `Intersection_T` in eine Straße einsetzen (Task 9).
2. Die Kreuzung selektieren, „Flip" drücken. Erwartet: der freie dritte Arm zeigt zur anderen Seite, **beide Straßenhälften liegen unverändert an ihrem Platz** und kreuzen sich nicht.
3. Nochmal „Flip". Erwartet: wieder wie am Anfang.
4. „Remove and close the road". Erwartet: die Kreuzung ist weg, aus den beiden Hälften ist eine Straße geworden.
5. Ctrl+Z. Erwartet: Kreuzung und zwei Hälften wieder da.
6. Eine dritte Straße an den freien Arm docken und die Kreuzung selektieren. Erwartet: beide Knöpfe sind grau und der Hinweis steht darunter.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Streets/Editor/JunctionInsertion.cs" "Assets/Scripts/Streets/Editor/IntersectionEditor.cs"
git commit -m "feat(streets): flip and remove an inserted junction"
```

---

### Task 11: Was vom Segment-an-Segment-Zustand weg soll

Kein Code, bis der Nutzer entschieden hat. Diese Aufgabe legt ihm die Kandidaten vor.

- [ ] **Step 1: Die Kandidaten belegen**

Für jeden Kandidaten mit `grep` zeigen, dass ihn nach Task 4 nichts mehr setzt:

```bash
grep -rn "\.segment\b\|segmentEnd\|\.driven" Assets/Scripts/Streets --include=*.cs
```

Kandidaten:

| Ort | Was |
|---|---|
| `StreetEndConnector.cs` | Felder `segment`, `segmentEnd`, `driven` |
| `StreetSegment.TryGetTarget` | Der Zweig für `connector.segment` |
| `StreetSegment.CollectContinuations` | Der Zweig für `connector.segment` |
| `StreetSegment.ApplyConnection` | Die Abfrage auf `connector.driven` |
| `StreetSnapping.Validate` | Alle Zweige über `connector.segment` |
| `StreetSnapping.Disconnect` | Das Löschen beim Gegenpart |
| `StreetSnapTarget` | Felder `segment`, `segmentEnd` — **bleiben**, `FindNearest` braucht sie fürs Verschmelzen |
| `StreetSegmentEditor.DrawEndReadout` | Die Anzeige „nicht getrieben" |

- [ ] **Step 2: Einzeln vorlegen**

Dem Nutzer die Liste zeigen und **jeden Punkt einzeln** entscheiden lassen. Nichts löschen, was nicht ausdrücklich freigegeben wurde. Beachten: `driven` wird auch von Socket-Verbindungen gelesen (`ApplyConnection` prüft es), steht dort aber immer auf `true` — das ist eine eigene Entscheidung, keine Beifang-Löschung.

- [ ] **Step 3: Freigegebenes löschen, kompilieren, Tests laufen lassen, committen**

Nur die freigegebenen Punkte. Danach `dotnet build` auf beide `.csproj` und ein Testlauf durch den Nutzer.

---

## Nach dem Plan

Die Spec fortschreiben: in `2026-09-01-street-merge-and-junction-insertion-design.md` unter „Umsetzung in Schritten" festhalten, was gebaut ist, und in `2026-08-31-street-network-editor-design.md` den Eintrag „Segment an Segment" auf den neuen Stand bringen.

Abschnitt C — Auf- und Abfahrten als Y-Prefab — läuft danach ohne Codeänderung über dieselbe Palette und dieselbe Einsetz-Geste. Offen bleibt dort allein, ob `IntersectionLaneBuilder` die Abbiegerichtung bei sehr spitzen Winkeln noch sinnvoll bestimmt.
