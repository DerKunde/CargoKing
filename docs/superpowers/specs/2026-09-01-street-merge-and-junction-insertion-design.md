# Straßen verschmelzen, trennen und Kreuzungen einsetzen — Design

Datum: 2026-09-01
Status: Abschnitt A und B abgenommen, noch nicht gebaut.
Baut auf: [`2026-08-31-street-network-editor-design.md`](2026-08-31-street-network-editor-design.md)

## Anlass

Straßen entstehen bisher als einzelne `StreetSegment`-Objekte, die über `StreetEndConnector`
aneinanderhängen. Eine in vier Zügen gebaute Gerade besteht damit aus vier Objekten mit vier
Splines. Das ist beim Nachbearbeiten unhandlich, und es macht die Angabe „setze hier eine Kreuzung
ein" mehrdeutig — *hier* ist keine eindeutige Stelle, wenn nicht klar ist, auf welchem der vier
Splines gezählt wird.

Zwei Änderungen daraus:

- **A** — Werden zwei Segmente verbunden, entsteht **ein** Segment mit **einem** Spline. Dazu die
  Umkehrung: einen Spline an einem Knoten trennen.
- **B** — Einen Knoten durch eine Kreuzung ersetzen: trennen, Prefab dazwischensetzen, beide Hälften
  andocken.

Auf- und Abfahrten (Abschnitt C der Vorüberlegung) sind bewusst **nicht** Teil dieses Entwurfs. Sie
laufen später über denselben Mechanismus: ein Y-Prefab mit drei Sockets, an einem Knoten eingesetzt.
Der Verzögerungsstreifen daneben ist dann eine gewöhnliche Straße aus dem dritten Socket.

## Voraussetzungen, die bereits stehen

| Baustein | Rolle in diesem Entwurf |
|---|---|
| `StreetEndConnector` | Hält weiterhin die Socket-Verbindung an den *äußeren* Enden. |
| `StreetSnapping.Connect` / `Disconnect` / `Validate` | Einstiegspunkt: Segment-an-Segment wird ab jetzt zu Verschmelzen umgeleitet. |
| `IntersectionSocketDragging` | Zieht Straßen aus Sockets; landet die neue Straße auf einer bestehenden, greift Verschmelzen. |
| `StreetDrawing` | Schaltet alle eigenen Zeichnungen und Anfasser ab. Die Knoten-Anfasser aus A hängen daran und streiten deshalb nicht mit Unitys Spline-Werkzeug um Klicks. |

## Verworfene Alternative

**Die Kreuzung als Markierung auf dem Spline** — die Straße bliebe ein Objekt, die Kreuzung ein
Eintrag „Knoten 4, Prefab X" mit einem verwalteten Kind darunter. Das hielte Straßen über Kreuzungen
hinweg durchgehend, scheitert aber an der Eigentumsfrage: Wem gehört eine X-Kreuzung, an der sich
zwei Straßen kreuzen? Bei einer T-Einmündung müsste die einmündende Straße auf einen Eintrag in
einem fremden Segment zeigen.

Dahinter liegt eine Unterscheidung, die den ganzen Entwurf trägt: es gibt **zwei Sorten
Zersplitterung.** Die willkürliche — vier Objekte, weil in vier Zügen gebaut — räumt A weg. Die
bedeutungstragende — an einer Kreuzung hört eine Straße auf und eine andere fängt an — bleibt, weil
das Fahrnetz genau dort verzweigt ist. Sie wegzumodellieren hieße, die Karte gegen das Gelände zu
richten.

## Getroffene Entscheidungen

| Frage | Entscheidung |
|---|---|
| Auslöser fürs Verschmelzen | Die Verbindungsgeste, nicht die Nähe. Zwei aufeinanderliegende offene Enden bleiben getrennt. |
| Wer überlebt | Das Segment, auf das fallengelassen wird. Das gezogene verschwindet darin. |
| Ungleiche Segmente | Wird nicht verschmolzen, sondern gemeldet. Keine stillschweigende Übernahme einer der beiden Breiten. |
| Naht | Ein Knoten, auf `TangentMode.Continuous`. |
| Kreuzung im Verhältnis zur Straße | Eigenes Objekt zwischen zwei Straßen, Geschwister im Hierarchiebaum. |
| Ausrichtung der Kreuzung | Abgeleitet aus Knoten und Tangente. Übrig bleibt allein die Seite des Stamms. |
| Palette | `StreetKit`-ScriptableObject, beim Anlegen einmalig aus dem Projekt vorbefüllt. |

---

## Abschnitt A — Verschmelzen und Trennen

### Verschmelzen

Ausgelöst in `StreetSnapping.Connect`, wenn das Ziel kein Socket ist. Statt zwei Verbinder zu
schreiben, wird verschmolzen.

**Vorbedingungen.** Alle müssen erfüllt sein, sonst passiert nichts und die Meldung nennt den Grund:

1. `roadWidth`, `sourceMesh`, `forwardAxis` und `tileLength` stimmen überein.
2. Beide Splines haben mindestens zwei Knoten.
3. Beide Transforms haben Skalierung 1. Ungleiche Skalierung würde die Tangentenlängen beim Umrechnen
   verzerren; das ist ein Fall, der es nicht wert ist, still falsch zu rechnen.
4. Es sind zwei verschiedene Objekte.

**Zusammensetzen.** Vier Fälle — Ende-an-Anfang, Ende-an-Ende, Anfang-an-Anfang, Anfang-an-Ende —
aber kein Fall bekommt einen eigenen Weg. Stattdessen wird eine Knotenliste in Straßenrichtung
aufgebaut, wobei die jeweils benötigte Seite rückwärts gelesen wird, und geschlossen in den
Überlebenden geschrieben. Die Knoten des Verschluckten kommen dabei über die relative Transformation
der beiden Container in dessen Raum.

Die Umrechnung schreiben wir nicht selbst: `BezierKnot.Transform(float4x4)` aus dem Splines-Paket
macht sie samt Tangenten korrekt. Die Matrix ist `survivor.worldToLocalMatrix *
absorbed.localToWorldMatrix`. Damit ist die Frage, ob Tangenten mitgedreht werden müssen, keine
Frage mehr — sie liegen im Rotationsraum des Knotens, und `Transform` rechnet das mit.

**Umkehren als eigener Baustein.** Zwei der vier Fälle brauchen eine Seite rückwärts, einer davon
den Überlebenden selbst. Umkehren heißt: Knotenreihenfolge drehen, je Knoten die Rotation um 180°
um die eigene Up-Achse drehen und `TangentIn`/`TangentOut` vertauscht **und negiert** übernehmen —
weil die Tangenten im Rotationsraum liegen, der sich gerade mitgedreht hat. Die Up-Achse bleibt
dabei erhalten, das Mesh kippt also nicht. Beim Umkehren eines Segments tauschen zusätzlich seine
beiden Verbinder die Plätze.

**Die Naht — nur wo wirklich eine ist.** Liegen beide Enden auf demselben Punkt, bleibt einer der
beiden Knoten, auf `TangentMode.Continuous`. Liegen sie **auseinander**, bleiben beide stehen und
das Stück dazwischen ist Straße.

Das ist keine Spitzfindigkeit, sondern der Fall *Kreuzung entfernen*: die beiden Hälften stehen dann
an den Sockets, also den Socket-Abstand auseinander. Würden sie verschweißt, verschluckte das
Zusammenfügen genau das Stück Straße, auf dem die Kreuzung stand — die Straße würde beim Entfernen
kürzer. Damit ist die „erzwungene Tangenten-Stetigkeit" der Ausgangs-Spec keine
Regel mehr, die zwei Objekte einhalten müssen, sondern eine Eigenschaft eines einzelnen Knotens.

**Die äußeren Enden.** Die beiden Verbinder des Überlebenden übernehmen, was an den äußeren Enden der
zusammengesetzten Straße hing. Hing das ferne Ende des Verschluckten an einem Socket, wandert diese
Verbindung mit.

**Das verschluckte Objekt.** Kinder darunter — ein Schild, eine Laterne — werden an den Überlebenden
umgehängt, dann wird das GameObject gelöscht. Der ganze Vorgang ist eine Undo-Gruppe, die den alten
Zustand beider Seiten wiederherstellt.

**Danach** `Rebuild()` auf dem Überlebenden; Mesh und Spuren entstehen ohnehin aus dem Spline neu.
Die Krümmungswarnung greift wie bisher, falls die stetige Naht eine zu enge Kurve erzeugt.

### Trennen

**Geste.** Jeder Knoten im *Inneren* einer Straße bekommt einen Anfasser — die beiden Endknoten
nicht, dort gäbe es nichts zu trennen. Ein Klick öffnet ein kleines Menü: *Hier trennen* oder
*Kreuzung einsetzen ▸ …*. Dieselbe Geste bedient also A und B.

Die Anfasser hängen an `StreetDrawing.Enabled` und streiten deshalb nicht mit Unitys Knoten-Handles
um Klicks.

**Ergebnis.** Der Überlebende behält die Knoten bis zur Schnittstelle, ein neues Segment bekommt die
dahinter; der Schnittknoten wird verdoppelt, jede Hälfte bekommt eine Kopie. Das neue Segment
übernimmt Kachel, Breite, Achse und Materialien vom Überlebenden und trägt dessen Namen mit einem
Zusatz, der es in der Hierarchie unterscheidbar macht. Beide neuen Enden sind **offen**;
sie verschmelzen nicht sofort wieder, obwohl sie aufeinanderliegen — Verschmelzen hängt an der
Geste, nicht an der Nähe.

**Vorbedingung.** Der Knotenindex liegt zwischen 1 und `Count - 2`, sodass beide Hälften mindestens
zwei Knoten behalten.

### Was danach gegenstandslos wird

`StreetEndConnector.segment`, `segmentEnd` und `driven` haben nach A keinen Fall mehr, ebenso die
zugehörigen Zweige in `StreetSegment.TryGetTarget`, `StreetSegment.CollectContinuations`,
`StreetSnapping.Validate` und `StreetSnapping.Disconnect`.

**Dieser Entwurf löscht davon nichts.** Was weg soll, wird nach dem Bauen einzeln entschieden.

---

## Abschnitt B — Kreuzung an einem Knoten einsetzen

Einsetzen ist Trennen aus A plus zwei Andockvorgänge — dieselbe Chirurgie mit einem Prefab
dazwischen.

### Ausrichtung

Am Knoten liegen Position, Tangente und Up fest. Das Prefab wird so gedreht, dass **zwei
gegenüberliegende Sockets auf der Straßenachse liegen.** Welches Paar das ist, muss niemand angeben:
es ist das Paar, dessen `Outward`-Richtungen einander am nächsten entgegenstehen. Eine X-Kreuzung hat
zwei gleichwertige solche Paare, eine T-Einmündung genau eines — die Durchfahrt —, und der dritte
Socket ist der Stamm.

Positioniert wird so, dass der **Mittelpunkt dieses Paares** auf dem Knoten sitzt. Bei den
vorhandenen Prefabs ist das der Ursprung, aber die Regel gilt auch für ein Modell, dessen Sockets
nicht symmetrisch um den Ursprung liegen.

Damit bleibt genau eine Entscheidung übrig, die sich nicht ableiten lässt: **auf welcher Seite der
Stamm steht.**

### Umdrehen

Ein Anfasser an der selektierten Kreuzung: 180° um ihre Up-Achse.

Das ist **nicht** bloß ein Drehen des Transforms. Die beiden Straßenhälften werden von ihren Sockets
getrieben; drehte man nur, würde jede Hälfte auf den Platz der anderen gezogen und die beiden
kreuzten sich. Umdrehen heißt deshalb: drehen **und** die beiden Socket-Referenzen der andockenden
Hälften tauschen. Danach steht alles wie vorher, nur der Stamm zeigt zur anderen Seite.

Freies Drehen mit `R` bleibt erlaubt und funktioniert schon heute richtig — die Hälften folgen der
Kreuzung, weil die Verbinder Referenzen halten und keine Positionen. Die Durchfahrt steht dann schräg
zur Straße; das ist dann Absicht, kein Fehler.

### Was mit der Straße passiert

Die Sockets der vorhandenen Prefabs sitzen 9,5 m von der Mitte. Beide Hälften ziehen sich um dieses
Stück zurück, sobald die Verbinder greifen: **die Kreuzung frisst das Stück Straße, auf dem sie
steht**, und die Straße bleibt insgesamt gleich lang. Dafür ist kein eigener Code nötig, das ergibt
sich aus `StreetSegment.ApplyConnection`.

**Vorbedingungen.**

1. Das Prefab trägt eine `Intersection` mit mindestens zwei aktiven Sockets und einem
   gegenüberliegenden Paar.
2. `roadWidth` der Straße stimmt mit der `roadWidth` der beiden Durchfahrt-Sockets überein.
3. Der Knoten liegt weiter als der Socket-Abstand von beiden Straßenenden entfernt — sonst stülpte
   sich die Hälfte um. Die Meldung nennt den nötigen Abstand in Metern.
4. Die Bedingung aus A: beide Hälften behalten mindestens zwei Knoten.

**Hierarchie.** Das Prefab landet als Geschwister der Straße, nicht als Kind. Es gehört beiden
Hälften, und ab dem dritten Arm auch einer dritten Straße.

**Undo.** Trennen, Instanziieren und beide Verbindungen sind eine Gruppe.

### Kreuzung wieder entfernen

Die Umkehrung, mit A fast geschenkt: docken genau zwei Straßen an und sind die übrigen Arme frei,
verschmilzt *Kreuzung entfernen* die beiden Hälften wieder zu einer und löscht das Prefab. Die
Vorbedingungen des Verschmelzens gelten dabei unverändert.

Ohne diesen Weg wäre eine falsch gesetzte Kreuzung nur per Undo loszuwerden.

### Die Palette — `StreetKit`

Ein ScriptableObject mit einer ausdrücklich aufgezählten Liste von Kreuzungs-Prefabs. Gefunden wird
es über `AssetDatabase.FindAssets("t:StreetKit")`; benutzt wird das erste Ergebnis nach GUID
sortiert, damit die Wahl über Sitzungen hinweg dieselbe bleibt. Gibt es mehr als eines, meldet das
Menü das einmal — zwei Paletten im Projekt sind kein Zustand, der still bleiben darf.

Existiert keines, bietet das Knoten-Menü an, eines anzulegen — und füllt es dabei **einmalig**,
indem es das Projekt nach Prefabs mit `Intersection` durchsucht. Die zwei vorhandenen Kreuzungen
stehen damit ohne Einrichtungsschritt zur Verfügung. Gescannt wird nur bei diesem einen Anlass;
danach ist die Liste kuratiert und wird von Hand gepflegt. Das ist der Unterschied zum
Ordner-Scannen, das die Ausgangs-Spec ablehnt: eine Testleiche landet nicht dauerhaft in der
Palette, weil die Palette nicht dauerhaft aus dem Ordner gelesen wird.

Das `StreetKit` validiert seine Einträge: ein Prefab ohne `Intersection` wird im Inspector gemeldet.

---

## Dateien

| Datei | Rolle |
|---|---|
| `Editor/StreetSurgery.cs` | Neu. Verschmelzen, Umkehren, Trennen, samt Vorbedingungsprüfungen. Kennt keine Kreuzungen. |
| `Editor/StreetKnotHandles.cs` | Neu. Anfasser an den inneren Knoten und das Menü darauf. |
| `Editor/JunctionPlacement.cs` | Neu. Rein rechnend: welches Socket-Paar die Durchfahrt ist, und wohin und wie gedreht das Prefab gehört. Fasst nichts in der Szene an — deshalb ohne Prefab und ohne Editor-Zustand prüfbar. |
| `Editor/JunctionInsertion.cs` | Neu. Einsetzen, Umdrehen, Entfernen. Benutzt `StreetSurgery` und `JunctionPlacement`. |
| `Editor/CargoKing.Streets.Editor.Tests.asmdef` + Tests | Neu. EditMode-Tests für die Chirurgie und die Ausrichtungsrechnung. |
| `Editor/StreetKit.cs` | Neu. Die Palette samt Anlegen und Vorbefüllen. |
| `Editor/StreetSnapping.cs` | Segment-an-Segment in `Connect` leitet auf `StreetSurgery.Merge` um. |
| `Editor/StreetSegmentEditor.cs` | Ruft `StreetKnotHandles`. |
| `Editor/IntersectionEditor.cs` | Anfasser fürs Umdrehen, Eintrag fürs Entfernen. |

`StreetSegment` und die übrige Laufzeit bleiben unangetastet. Die ganze Chirurgie ist
Autoren-Werkzeug und gehört ins Editor-Assembly.

## Bewusste Schnitte

1. **Kein Zusammenführen dreier Straßen ohne Kreuzung.** Eine Gabelung ohne Prefab bleibt
   unausdrückbar, wie bisher.
2. **Kein Einsetzen zwischen zwei Knoten.** Eingesetzt wird an einem Knoten. Wer einen an der
   richtigen Stelle braucht, setzt ihn mit Unitys Spline-Werkzeug — dafür ist es da.
3. **Kein Verschmelzen über eine Kreuzung hinweg.** Zwei Straßen an gegenüberliegenden Sockets
   bleiben zwei Straßen; das ist die bedeutungstragende Trennung von oben.

## Umsetzung in Schritten

Jeder Schritt ist für sich benutzbar.

1. **`StreetSurgery.Merge`** samt Vorbedingungen, angeschlossen an `StreetSnapping.Connect`. Ab hier
   entstehen beim Verbinden keine Ketten mehr.
2. **`StreetSurgery.Split`** und die Knoten-Anfasser mit *Hier trennen*. Ab hier ist Verschmelzen
   umkehrbar.
3. **`StreetKit`** samt Anlegen und Vorbefüllen.
4. **`JunctionInsertion.Insert`** und der Menüeintrag. Der eigentliche Zweck.
5. **Umdrehen und Entfernen** an der Kreuzung.
6. **Aufräumen:** einzeln entscheiden, was vom Segment-an-Segment-Zustand weg soll.

## Noch offen

- Was vom Segment-an-Segment-Zustand gelöscht wird (Schritt 6).
- Abschnitt C — Auf- und Abfahrten als Y-Prefab auf diesem Mechanismus.
- Unverändert offen aus der Ausgangs-Spec: `StreetProfile`, Netz-Objekt, Bake, Routing,
  `RouteFollower`.
