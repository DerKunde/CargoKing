# Straßen-Editor & AI-Fahrnetz — Design

Datum: 2026-08-31
Status: Abschnitt 1 und 2 abgenommen, Abschnitt 3 und 4 offen. Gebaut sind Mesh, Spuren,
Kreuzungen und Endverbinder — siehe „Umsetzung in Schritten" am Ende.

## Für die nächste Sitzung zuerst

**Nichts vom Straßen-Editor ist bisher unter Beobachtung in Unity gelaufen.** Geprüft ist
ausschließlich die Kompilierung (`CargoKing.Streets.csproj` und `CargoKing.Streets.Editor.csproj`
per `dotnet build`, Zwischendateien ins Scratchpad umgeleitet). Erste Frage beim Wiederaufnehmen:
was hat tatsächlich funktioniert?

Drei Punkte lassen sich nur am laufenden Editor klären:

1. **Socket-Höhe.** Die Sockets der Kreuzungs-Prefabs stehen auf `y = 0,5`. Der Socket ist als
   **Spline-Anker** definiert, nicht als Oberflächenmarkierung. Zeigt die Naht zwischen Straße und
   Kreuzung eine Stufe, gehört die Socket-Höhe korrigiert — nicht der Code, und schon gar kein
   Höhenversatz im Verbinder, der einen Modellfehler dauerhaft festschriebe.
2. **`roadWidth` steht auf 7 m und ist geraten.** Die echte Fahrbahnbreite der Kachel ist nicht
   bekannt; `StreetSegment.fbx` misst 20 m in der Breite, vermutlich inklusive Bankett oder
   Bodenplatte. Der Wert muss an `StreetSegment` **und** an jedem `IntersectionSocket` gleich sein,
   sonst verfehlen sich die Spuren an der Naht.
3. **Alle Neuaufbauten laufen über `[ExecuteAlways] Update`.** Das tickt im Edit-Modus zuverlässig,
   aber nicht in festem Takt. Fühlt sich das Nachziehen beim Ziehen der Spline zäh an, gehört ein
   `EditorApplication.update`-Haken ins Editor-Assembly.

## Dateien

| Datei | Rolle |
|---|---|
| `StreetSegment.cs` | Das Straßenstück: Spline, Kachel, Collider, Verbinder, Neuaufbau. |
| `StreetMeshBuilder.cs` | Kachel entlang der Spline wiederholen und biegen. |
| `StreetLaneBuilder.cs` / `StreetLane.cs` | Zwei Spuren je Segment, adaptiv abgetastet. |
| `StreetCurvature.cs` | Kleinster Krümmungsradius einer Spline. |
| `StreetFrame.cs` | Gemeinsame Orientierung, auf der Mesh und Spuren beide sitzen. |
| `StreetEndConnector.cs` | Ein Ende und woran es dockt. |
| `Intersection.cs` / `IntersectionSocket.cs` / `IntersectionLaneBuilder.cs` / `IntersectionConnection.cs` | Kreuzung, Anschlüsse, abgeleitete Wege. |
| `Editor/StreetSegmentEditor.cs` | Anzeigen, Spuren, Endanfasser. |
| `Editor/StreetSnapping.cs` | Kandidatensuche, Verbinden, Trennen, Validierung. |
| `Editor/IntersectionEditor.cs` | Sockets und Wege zeichnen. |

## Ziel

Ein Unity-Editor-Tool, mit dem Straßen für CargoKing angelegt werden. Die Straßen liefern
gleichzeitig das Pfadnetz, auf dem der AI-Fahrer sein Ziel erreicht. V1: zweispurige Straßen
(eine Spur je Richtung, Gegenverkehr). Größere Straßen später über den Querschnitt-Datentyp,
ohne Umbau des Netzwerks.

## Ausgangslage im Projekt

- Unity 6000.3.8f1, `com.unity.splines` 2.8.4 vorhanden, `com.unity.ai.navigation` vorhanden (ungenutzt).
- `AIDriver.cs` ist ein reiner Punkt-Anfahrer: Ziel rein, Lenk-/Gas-/Bremsbefehl raus,
  inklusive Wendekreis-Erkennung und Rückwärts-Manöver. Kein Begriff von Route oder
  Kurvengeschwindigkeit (fährt konstant `DrivingThrottle = 0.2f`).
- Keine `.asmdef` im Projekt, kein `Editor`-Ordner.

## Getroffene Entscheidungen

| Frage | Entscheidung |
|---|---|
| Umfang | Spline erzeugt **Mesh und Lanes aus derselben Quelle**. Kreuzungen sind **Prefabs mit Sockets**. |
| Verkehrslogik V1 | Geometrie **plus statische Regeln**: Tempolimit je Lane, Abbiegerichtung und Priorität je Kreuzungsverbindung. Regeln liegen im Graph, werden in V1 aber nur für die Sollgeschwindigkeit gelesen. |
| Spurbedeutung | „Zweispurig" = **eine Spur je Richtung**, Gegenverkehr. |
| Datenhaltung | **Bake in ein ScriptableObject-Asset.** Szene autoren, Asset ist die Laufzeitwahrheit. Mehrere Netz-Assets nebeneinander möglich. |
| Stale-Bake-Absicherung | Dirty-Tracking über `OnValidate` und `Spline.changed`, Content-Hash im Asset, Scene-View-Warnung, Auto-Bake beim Speichern und beim Play-Start. |
| Verbindungen | **Snap erzeugt eine serialisierte Referenz.** Die Verbindung hängt an der Identität, nicht an der Position. Keine Nachbarschaftssuche beim Bake. |
| Segment an Segment | **Erlaubt**, ohne Kreuzung, mit erzwungener Tangenten-Stetigkeit. Jedes Ende ist entweder *offen*, *an Socket* oder *an Segmentende*. |
| AI-Anbindung | Dünne Schicht **plus Sollgeschwindigkeit**. `RouteFollower` liefert `(Zielpunkt, Sollgeschwindigkeit)`. `AIDriver` bekommt einen einfachen Längsregler, die Manöver-Statemachine bleibt unverändert. |

### Krümmungsgrenze für gezogene Segmente

Ein Band der Halbbreite `w`, gebogen mit Radius `R`, staucht seine Innenkante um den Faktor
`1 - w/R`. Das ist die Geometrie eines gebogenen Bandes, kein Fehler im Mesh-Generator, und
lässt sich nicht wegrechnen. Unterhalb von `R = w` faltet sich die Innenkante in sich selbst.

Daraus folgt eine Arbeitsteilung, die den ganzen Aufbau trägt: **Splines machen Streckenverläufe,
Prefabs machen Ecken.** Ein enger Abbieger ist keine gebogene Straße, sondern eine Einmündung.

`StreetSegment` misst deshalb den kleinsten Krümmungsradius seiner Spline und warnt, sobald er
unter das Dreifache der Fahrbahnbreite fällt. Beim Bake in Schritt 5 wird daraus eine
Validierungsregel.

### Bewusste Schnitte für V1

1. Die letzten Meter abseits der Straße (Depot, Rampe) bekommen keine eigene Lösung. Die
   Route endet am nächstgelegenen Lane-Punkt, danach übernimmt das bestehende Punkt-Anfahren
   des `AIDriver`.
2. Kein Wissen über andere Fahrzeuge. Zwei Autos an derselben Kreuzung fahren ineinander.

## Abschnitt 1 — Schichten und Datenmodell

Vier Schichten. Die wichtigste Grenze liegt zwischen Editor und Laufzeit: zur Laufzeit
existiert kein einziges Autoren-Objekt.

### Schicht 1 — Authoring (Editor, in der Szene)

| Typ | Rolle |
|---|---|
| `StreetProfile` (ScriptableObject) | Querschnitt: Spuranzahl je Richtung, Spurbreite, Markierungen, Bordstein, Materialien, Standard-Tempolimit. **Erweiterungspunkt für größere Straßen.** |
| `StreetSegment` (MonoBehaviour) | Ein Straßenstück: `SplineContainer` + `StreetProfile` + Tempolimit-Override + zwei Endverbinder. |
| `StreetEndConnector` (serialisierbare Klasse) | Ein Ende. Zustand: offen, an Socket, an anderes Segmentende. Hält die Referenz, nicht die Position. |
| `Intersection` (MonoBehaviour, auf dem Prefab) | Liste von Sockets und Liste von Verbindungen. |
| `IntersectionSocket` | Transform (Position und Blickrichtung nach außen) + erwartetes `StreetProfile`. |
| `IntersectionConnection` | Befahrbare Verbindung innerhalb der Kreuzung: von Socket A nach Socket B, mit Abbiegerichtung und Priorität. Kurve per Bézier aus den Socket-Tangenten, im Prefab überschreibbar. |
| `StreetNetworkAuthoring` (MonoBehaviour) | Ein Objekt je Netz. Sammelt Segmente und Kreuzungen, hält die Referenz auf das Ziel-Asset, trägt Bake, Validierung und Hash. |

Kreuzungen bringen ihre Topologie im Prefab mit: T-Kreuzung = 3 Sockets, 6 Verbindungen;
X-Kreuzung = 4 Sockets, 12 Verbindungen. Neue Kreuzungsformen sind neue Prefabs, keine
Codeänderung.

### Schicht 2 — Bake (Editor)

1. Validieren als Gate: offene Enden, Profil-Mismatch am Socket, doppelt belegter Socket,
   unerreichbare Teilnetze. Schlägt es fehl, wird nicht gebacken.
2. Splines samplen, adaptiv nach Krümmung.
3. Lanes erzeugen: Mittellinie mit seitlichem Offset, Rückrichtung umgedreht, Krümmung je
   Sample vorberechnet.
4. Kreuzungen einweben: jede Verbindung wird eine eigene kurze Lane.
5. Graph bilden: Lanes sind Knoten, Übergänge sind Kanten. Kosten = Länge / Tempolimit plus
   Aufschlag fürs Abbiegen.
6. Räumlichen Index bauen (Uniform Grid) für `Project(Weltpunkt)`.
7. Content-Hash schreiben.

### Schicht 3 — Runtime

`StreetNetworkAsset` ist bewusst dumm: flache Arrays aus Structs, keine GameObject-Referenzen,
keine Splines. Ein Fahrplan, kein Szenengraph.

`StreetNetworkRuntime` bietet drei Operationen: `Project(Vector3)`, `FindRoute(from, to)`,
`SampleAhead(routePosition, distance)`.

Lanes werden als gesampelte Punktfolgen gespeichert, nicht als Splines. Begründung: der
`RouteFollower` fragt je `FixedUpdate` nach Position, Richtung und Krümmung — auf einer
Polyline ist das eine Array-Suche statt einer Bézier-Auswertung.

### Schicht 4 — Mesh

`StreetMeshBuilder` erzeugt aus derselben Spline und demselben Profil das sichtbare Mesh.
Fahrbahn und Lane entstehen aus einer gemeinsamen Quelle und können deshalb nicht
auseinanderdriften. Schreibt in ein separates Mesh-Asset.

### Kette bis zum Fahrzeug

```
Zielort (Weltpunkt)
  └─ StreetNetworkRuntime.FindRoute()   A* über den Lane-Graph
       └─ Route (Lanes + Kreuzungs-Verbindungskurven)
            └─ RouteFollower            Vorausschau + Sollgeschwindigkeit, je FixedUpdate
                 └─ AIDriver            Lenken / Gas / Bremse / Gang
                      └─ CarController
```

## Abschnitt 2 — Das Editor-Tool

### Was nicht gebaut wird

Kein eigener Spline-Editor. `com.unity.splines` bringt Knoten-Handles, Tangenten-Modi,
Einfügen und Löschen von Knoten, Undo und Multi-Select mit. Die Form der Straße gehört
Unity; Verbindungen, Profil, Kreuzungen und Bake gehören uns. Auch kein großes eigenes
Fenster mit Graph-Ansicht.

### Vier Einstiegspunkte

1. **Scene-View-Overlay `Street Builder`** (`Overlay`, UI Toolkit): aktives Profil, Palette
   der Kreuzungs-Prefabs, Darstellungsschalter, Live-Validierungsliste, Bake-Knopf mit
   Stale-Anzeige.
2. **`EditorTool` „Draw Street"**: exklusives Klick-Handling im Scene View, solange aktiv.
   Bewusst ein `EditorTool` statt `SceneView.duringSceneGui`, damit Klicks nicht global
   abgefangen werden.
3. **Custom Inspectors** für `StreetSegment`, `Intersection`, `StreetNetworkAuthoring`.
4. **Menüeinträge**: `GameObject ▸ CargoKing ▸ Street Network`,
   `Assets ▸ Create ▸ CargoKing ▸ Street Profile`.

### Arbeitsablauf

- **Netz anlegen** → `StreetNetworkAuthoring`; Inspector legt bei Bedarf das Asset an. Zwei
  Netz-Objekte = zwei Assets = zwei unabhängige Straßensysteme.
- **Straße zeichnen** → Tool aktivieren, Profil wählen, Klicks setzen Knoten (Raycast gegen
  Boden, wie `MouseToFloorPositioning` es fürs AI-Ziel tut). Vorschau als Band mit
  Mittellinie und Richtungspfeilen. Enter schließt ab, Escape verwirft. `Strg` beim ersten
  oder letzten Klick fängt am nächsten freien Ende oder Socket und schreibt die Verbindung.
- **Nachbearbeiten** → Unitys Spline-Handles, dazu je ein eigener Handle pro Ende, der den
  Verbindungszustand zeigt (offen rot, verbunden grün) und auf ein Ziel gezogen werden kann.
- **Kreuzung setzen** → Prefab aus der Palette wählen (gespeist aus einem `StreetKit`-
  ScriptableObject, explizit gelistet, kein Ordner-Scannen), auf ein offenes Ende klicken.
  Die Kreuzung wird so gedreht und positioniert, dass ihr nächster kompatibler Socket auf
  dem Ende sitzt. Referenz wird beidseitig geschrieben. Umgekehrter Weg ebenso möglich.
- **Prüfen** → Validierungsliste im Overlay, Klick auf eine Zeile selektiert und rahmt das
  betroffene Objekt.
- **Backen** → Knopf, oder automatisch beim Speichern und beim Play-Start bei falschem Hash.

### Unity-APIs

| Aufgabe | API |
|---|---|
| Zeichenmodus mit exklusivem Klick-Handling | `EditorTool`, `ToolManager` |
| Cockpit im Scene View | `Overlay` (UI Toolkit) |
| Anfasser, Vorschaulinien, Highlights | `Handles`, `Handles.DrawAAPolyLine`, `HandleUtility.DistanceToCircle` |
| Boden treffen | `HandleUtility.GUIPointToWorldRay` + `Physics.Raycast` |
| Kreuzung instanziieren | `PrefabUtility.InstantiatePrefab` |
| Undo | `Undo.RecordObject`, `Undo.RegisterCreatedObjectUndo` |
| Auto-Bake | `EditorSceneManager.sceneSaving`, `EditorApplication.playModeStateChanged` |
| Spline-Änderung erkennen | `Spline.changed` |
| Gizmos außerhalb der Selektion | `DrawGizmo` |

### Assembly-Struktur

```
Assets/Scripts/Streets/            → CargoKing.Streets.asmdef        (Runtime)
Assets/Scripts/Streets/Editor/     → CargoKing.Streets.Editor.asmdef (Editor-only)
```

`Assembly-CSharp` referenziert Auto-Referenced-Assemblies automatisch, der `AIDriver` kommt
also ohne Umbau an die Laufzeit-API. Umgekehrt kann `CargoKing.Streets` nicht auf
`Assembly-CSharp` zugreifen — für ein Netzwerk, das nur Geometrie kennt, die richtige
Richtung.

### Zwei Haltungen

- Die Overlay-Palette bleibt klein: Profil, Kreuzungs-Prefabs, Darstellungsschalter,
  Validierung, Bake. Alles andere gehört in Kontextmenü oder Inspector.
- Die Mesh-Vorschau beim Zeichnen bleibt grob (einfaches Band). Das volle Mesh entsteht erst
  beim Abschluss, sonst ruckelt das Zeichnen.

## Noch offen

- **Fortschreibung**: [`2026-09-01-street-merge-and-junction-insertion-design.md`](2026-09-01-street-merge-and-junction-insertion-design.md)
  ersetzt die Entscheidung „Segment an Segment mit erzwungener Tangenten-Stetigkeit" durch
  Verschmelzen zu einem Spline und beschreibt das Einsetzen von Kreuzungen an einem Knoten.
- **Abschnitt 3**: Bake-Details, Routing (A*), `RouteFollower`, Längsregler im `AIDriver`.
- **Abschnitt 4**: Fehlerfälle, Testbarkeit, Bauabschnitte.

## Umsetzung in Schritten

Kleinschrittig, nach dem, was jeweils allein nutzbar ist. Stand nach der zweiten Sitzung:

- **Fertig — Mesh-Erzeugung.** `StreetMeshBuilder` biegt eine Kachel entlang der Spline,
  `StreetSegment` hält Spline, Kachel und `MeshCollider` zusammen. `StreetFrame` liefert die
  gemeinsame Orientierung, auf der Mesh und Spuren beide sitzen.
- **Fertig — Krümmungswarnung.** `StreetCurvature` misst den kleinsten Radius,
  `StreetSegmentEditor` meldet ihn.
- **Fertig — Spuren.** `StreetLaneBuilder` erzeugt zwei Spuren je Segment, adaptiv abgetastet,
  mit eigener Länge und eigenem Kurvenradius je Spur.
- **Fertig — Kreuzungen.** `IntersectionSocket` als Kind-Transform, `Intersection` sammelt die
  aktiven Sockets, `IntersectionLaneBuilder` leitet daraus jede Fahrmöglichkeit ab. Die Topologie
  liegt in den Sockets, nicht im Modell: vier aktive Sockets ergeben eine Kreuzung, drei eine
  Einmündung, aus demselben `IntersectionSegment.fbx`. Verbindungen werden abgeleitet, nicht
  getippt; die Abbiegerichtung kommt aus dem vorzeichenbehafteten Winkel.
- **Fertig — Endverbinder, Snapping, Fortsetzungen.** `StreetEndConnector` an beiden Enden eines
  Segments, drei Zustände (offen / an Socket / an Segmentende). **Der Socket gewinnt:** ein
  verbundener Spline-Knoten wird aus dem Ziel überschrieben und wechselt dabei auf
  `TangentMode.Continuous`, sonst zieht AutoSmooth die Richtung wieder zum Nachbarknoten. Bei
  Segment an Segment folgt genau eine Seite (`driven`). Sockets bleiben passiv — nur Segmente
  speichern Verbindungen. Nodes existieren als **Abfrage** (`CollectContinuations`), nicht als
  Objekt; die flache Graph-Struktur entsteht erst im Bake.
- **Fertig — Straße aus einem Socket ziehen.** `Editor/IntersectionSocketDragging.cs` hängt an jeden
  freien Socket einen Zieh-Anfasser. Ziehen legt ein `StreetSegment` an, dessen Spline am Socket
  beginnt, und verbindet es über `StreetSnapping.Connect`. Die Breite kommt aus dem Socket, die
  Kachel von einer Straße, die schon an derselben Kreuzung hängt — sonst von irgendeiner in der
  Szene. Belegte Sockets bekommen keinen Anfasser; die Doppelbelegung ist damit nicht mehr
  herstellbar statt nur hinterher gemeldet. Bleibt das ferne Ende offen, übergibt die Geste an
  Unitys Zeichenwerkzeug (`EditorSplineUtility.SetKnotPlacementTool`), sodass die Straße ohne
  Werkzeugwechsel weitergezogen wird — kein eigenes Zeichenwerkzeug, wie Abschnitt 2 es vorgibt.
- **Fertig — Darstellungsschalter.** `Editor/StreetDrawing.cs`, Menüeintrag mit Häkchen und
  umlegbarem Kürzel, Zustand in `EditorPrefs`. Aus heißt: keine Spuren, Fortsetzungen, End- oder
  Sockel-Anfasser und keine Kreuzungswege, sodass Unitys Spline-Editor den Scene View allein hat.
  Der erste der im Overlay vorgesehenen Darstellungsschalter; er zieht dorthin um, sobald das
  Overlay existiert.
- **Offen — Zeichen-Tool.** Der `EditorTool`, der eine Straße auf der freien Fläche per Klickfolge
  anlegt, fehlt weiterhin. Segmente ohne Kreuzung entstehen von Hand über `Add Component`.
- **Offen — `StreetProfile`.** Spurbreite und Tempolimit liegen noch als Felder am Segment
  (`roadWidth`), nicht in einem eigenen Asset. Der Schritt zu mehr als zwei Spuren geht darüber.
- **Als Nächstes — Endverbinder, Snapping, Nodes.** Ein Node ist ein Verbindungspunkt und
  entsteht erst, wo zwei Segmente oder ein Segment und ein Socket aneinanderstoßen. Deshalb
  gehören Verbinder und Graph in denselben Schritt.
- Danach: Kreuzungs-Prefabs mit Sockets; Netz-Objekt, Validierung, Bake in das Asset; Routing
  und `RouteFollower` samt Längsregler im `AIDriver`.
