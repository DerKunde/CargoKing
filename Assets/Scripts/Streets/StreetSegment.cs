using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace CargoKing.Streets
{
    /// <summary>
    /// One stretch of road. Holds the spline that defines its course and rebuilds its mesh from a
    /// tile that is repeated along that spline.
    ///
    /// The generated mesh is never serialised - it is rebuilt from the tile and the spline whenever
    /// the component wakes up or the spline changes. That keeps the scene file small and means the
    /// mesh cannot go stale against the spline it belongs to.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("CargoKing/Street Segment")]
    [RequireComponent(typeof(SplineContainer), typeof(MeshFilter), typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class StreetSegment : MonoBehaviour
    {
        [Tooltip("Tile that is repeated along the spline. Needs Read/Write enabled in its import settings.")]
        public Mesh sourceMesh;

        [Tooltip("Local axis of the tile that points along the direction of travel.")]
        public StreetMeshAxis forwardAxis = StreetMeshAxis.X;

        [Tooltip("Nominal length of one tile in metres. 0 measures it from the mesh itself.")]
        [Min(0f)]
        public float tileLength;

        [Tooltip("Feed the generated mesh to a MeshCollider so vehicles can drive on it.")]
        public bool generateCollider = true;

        [Tooltip("Width of the carriageway in metres - the driveable part, without verges. Two lanes share it.")]
        [Min(0f)]
        public float roadWidth = 7f;

        [Tooltip("Warn below this curve radius in metres. 0 derives it from the width of the carriageway.")]
        [Min(0f)]
        public float curvatureWarningRadius;

        [Tooltip("What the first knot of the spline docks to.")]
        public StreetEndConnector startConnection = new StreetEndConnector();

        [Tooltip("What the last knot of the spline docks to.")]
        public StreetEndConnector endConnection = new StreetEndConnector();

        /// <summary>
        /// Multiple of the road's width below which a swept mesh starts to look wrong. A ribbon bent
        /// to radius R compresses its inner edge by 1 - halfWidth/R, so at three times the full width
        /// the inside is already about a sixth shorter than the centre line. Tighter than that belongs
        /// in an intersection prefab, whose corner geometry is modelled rather than swept.
        /// </summary>
        private const float WarningRadiusPerWidth = 3f;

        /// <summary>How far a driven knot may already be from its target before it is rewritten.</summary>
        private const float PositionEpsilon = 0.0005f;

        /// <summary>How far a driven knot may already be turned from its target, in degrees.</summary>
        private const float RotationEpsilon = 0.05f;

        private SplineContainer splineContainer;
        private MeshFilter meshFilter;
        private MeshCollider meshCollider;
        private Mesh generatedMesh;
        private bool isDirty = true;

        private float minimumRadius = float.PositiveInfinity;
        private Vector3 tightestLocalPosition;
        private Vector3 tileSize;
        private StreetLane[] lanes = System.Array.Empty<StreetLane>();

        /// <summary>Smallest curve radius along this segment in metres, or infinity when it is straight.</summary>
        public float MinimumRadius => minimumRadius;

        /// <summary>Radius below which this segment reports a curve as too tight, in metres.</summary>
        public float WarningRadius =>
            curvatureWarningRadius > 0f ? curvatureWarningRadius : roadWidth * WarningRadiusPerWidth;

        /// <summary>
        /// The two lanes of this segment, one each way. Rebuilt with the mesh and never serialised.
        /// </summary>
        public IReadOnlyList<StreetLane> Lanes => lanes;

        /// <summary>Measured size of the tile: x across, y tall, z along the road.</summary>
        public Vector3 TileSize => tileSize;

        /// <summary>True when the spline bends tighter than this road is wide enough for.</summary>
        public bool HasTightCurve => WarningRadius > 0f && minimumRadius < WarningRadius;

        /// <summary>World position of the sharpest point on the spline.</summary>
        public Vector3 TightestPoint => transform.TransformPoint(tightestLocalPosition);

        private void OnEnable()
        {
            splineContainer = GetComponent<SplineContainer>();
            meshFilter = GetComponent<MeshFilter>();
            meshCollider = GetComponent<MeshCollider>();

            Spline.Changed += OnSplineChanged;
            isDirty = true;
        }

        private void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
        }

        private void OnDestroy()
        {
            if (generatedMesh != null)
            {
                // Not serialised, so nothing else will ever clean it up.
                DestroyImmediate(generatedMesh);
                generatedMesh = null;
            }
        }

        private void OnValidate()
        {
            // Only flagged here. Rebuilding inside OnValidate is not allowed to touch other objects,
            // and a rebuild is far too heavy to run on every keystroke in the inspector.
            isDirty = true;
        }

        private void Update()
        {
            // Checked every tick rather than driven by an event: a moved intersection raises no
            // callback of its own. The check costs two transform lookups and writes nothing unless the
            // knot has actually drifted from its socket.
            if (ApplyConnections())
            {
                isDirty = true;
            }

            if (isDirty)
            {
                Rebuild();
            }
        }

        /// <summary>The connector for one end of this segment.</summary>
        public StreetEndConnector ConnectorAt(StreetEnd end)
        {
            return end == StreetEnd.Start ? startConnection : endConnection;
        }

        /// <summary>World position of the first or last knot of the spline.</summary>
        public Vector3 EndPosition(StreetEnd end)
        {
            Spline spline = Spline;
            if (spline == null || spline.Count == 0)
            {
                return transform.position;
            }

            float3 position = spline[end == StreetEnd.Start ? 0 : spline.Count - 1].Position;
            return transform.TransformPoint(new Vector3(position.x, position.y, position.z));
        }

        /// <summary>
        /// World direction the spline runs in at that end, along its own parameter direction - so at
        /// the start it points into the segment, at the end out of it.
        /// </summary>
        public Vector3 EndDirection(StreetEnd end)
        {
            Spline spline = Spline;
            if (spline == null || spline.Count < 2)
            {
                return transform.forward;
            }

            float3 tangent = spline.EvaluateTangent(end == StreetEnd.Start ? 0f : 1f);
            Vector3 direction = transform.TransformDirection(new Vector3(tangent.x, tangent.y, tangent.z));

            return direction.sqrMagnitude > 0.000001f ? direction.normalized : transform.forward;
        }

        /// <summary>
        /// Collects the lanes that carry on from the lane leaving this segment at the given end.
        ///
        /// This is what a node is, expressed as a question rather than as an object: the connector
        /// already knows the counterpart, and the counterpart already knows its lanes. Writing the
        /// same relation down a second time as a graph would only create a second truth to keep in
        /// step - the flat graph belongs in the baked asset, not here.
        /// </summary>
        public void CollectContinuations(StreetEnd end, List<StreetContinuation> results)
        {
            results.Clear();

            StreetEndConnector connector = ConnectorAt(end);
            if (connector == null || !connector.IsConnected)
            {
                return;
            }

            if (connector.socket != null)
            {
                CollectIntersectionContinuations(connector.socket, results);
                return;
            }

            StreetSegment other = connector.segment;
            if (other == null || other.Lanes.Count < 2)
            {
                return;
            }

            // Leaving at the other segment's start means carrying on along its spline, which is its
            // forward lane; leaving at its end means running against the spline, so the backward one.
            StreetLane lane = connector.segmentEnd == StreetEnd.Start ? other.Lanes[0] : other.Lanes[1];
            results.Add(new StreetContinuation { space = other.transform, lane = lane });
        }

        private static void CollectIntersectionContinuations(IntersectionSocket socket, List<StreetContinuation> results)
        {
            Intersection intersection = socket.Owner;
            if (intersection == null)
            {
                return;
            }

            int socketIndex = intersection.IndexOf(socket);
            if (socketIndex < 0)
            {
                return;
            }

            for (int index = 0; index < intersection.Connections.Count; index++)
            {
                IntersectionConnection connection = intersection.Connections[index];
                if (connection.FromSocket == socketIndex)
                {
                    results.Add(new StreetContinuation { space = intersection.transform, lane = connection.Lane });
                }
            }
        }

        private Spline Spline => splineContainer != null ? splineContainer.Spline : null;

        /// <summary>
        /// Pulls both driven ends onto whatever they dock to.
        /// </summary>
        /// <returns>True when a knot was actually moved.</returns>
        private bool ApplyConnections()
        {
            if (splineContainer == null)
            {
                return false;
            }

            bool moved = ApplyConnection(startConnection, StreetEnd.Start);
            moved |= ApplyConnection(endConnection, StreetEnd.End);
            return moved;
        }

        private bool ApplyConnection(StreetEndConnector connector, StreetEnd end)
        {
            if (connector == null || !connector.IsConnected || !connector.driven)
            {
                return false;
            }

            Spline spline = Spline;
            if (spline == null || spline.Count < 2)
            {
                return false;
            }

            if (!TryGetTarget(connector, end, out Vector3 worldPosition, out Vector3 worldDirection, out Vector3 worldUp))
            {
                return false;
            }

            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
            Vector3 localDirection = transform.InverseTransformDirection(worldDirection);
            Vector3 localUp = transform.InverseTransformDirection(worldUp);

            if (localDirection.sqrMagnitude < 0.000001f)
            {
                return false;
            }

            Quaternion rotation = StreetFrame.At(
                new float3(localDirection.x, localDirection.y, localDirection.z),
                new float3(localUp.x, localUp.y, localUp.z));

            int index = end == StreetEnd.Start ? 0 : spline.Count - 1;

            // A driven knot must not stay on AutoSmooth: Unity would pull its tangent back towards the
            // neighbouring knot and the street would leave the intersection at an angle.
            if (spline.GetTangentMode(index) != TangentMode.Continuous)
            {
                spline.SetTangentMode(index, TangentMode.Continuous);
            }

            BezierKnot knot = spline[index];
            Vector3 currentPosition = new Vector3(knot.Position.x, knot.Position.y, knot.Position.z);
            Quaternion currentRotation = new Quaternion(
                knot.Rotation.value.x, knot.Rotation.value.y, knot.Rotation.value.z, knot.Rotation.value.w);

            // Written only when it really differs. Otherwise every single tick would count as an edit
            // and the scene would never stop being dirty.
            if (Vector3.Distance(currentPosition, localPosition) < PositionEpsilon
                && Quaternion.Angle(currentRotation, rotation) < RotationEpsilon)
            {
                return false;
            }

            knot.Position = new float3(localPosition.x, localPosition.y, localPosition.z);
            knot.Rotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w);

            // The notifying setter, not SetKnotNoNotify. The quiet one writes the knot but never marks
            // the spline dirty, so its cached length and per-curve arc length tables keep describing
            // the old shape - the knot would follow the socket while everything derived from it, mesh
            // and lanes included, stayed where it was.
            //
            // The change event does come back as one more rebuild. That settles by itself: the knot
            // then already matches its target, the comparison above returns early, and nothing is
            // written a second time.
            spline.SetKnot(index, knot);
            return true;
        }

        /// <summary>
        /// Where this end has to sit and which way the spline has to run there.
        /// </summary>
        private bool TryGetTarget(
            StreetEndConnector connector,
            StreetEnd end,
            out Vector3 position,
            out Vector3 direction,
            out Vector3 up)
        {
            position = Vector3.zero;
            direction = Vector3.zero;
            up = Vector3.up;

            if (connector.socket != null)
            {
                Transform socket = connector.socket.transform;
                position = socket.position;
                up = socket.up;

                // The socket points away from the intersection. A spline that ends there runs into it,
                // so its tangent is the other way round; one that starts there runs along.
                direction = end == StreetEnd.Start ? connector.socket.Outward : -connector.socket.Outward;
                return true;
            }

            StreetSegment other = connector.segment;
            if (other == null || other == this)
            {
                return false;
            }

            position = other.EndPosition(connector.segmentEnd);
            up = other.transform.up;

            // Two ends of the same kind meet head on, so one of the two tangents has to be flipped for
            // the seam to stay smooth. Different kinds already run the same way.
            Vector3 otherDirection = other.EndDirection(connector.segmentEnd);
            direction = end == connector.segmentEnd ? -otherDirection : otherDirection;
            return true;
        }

        /// <summary>
        /// Regenerates the mesh from the current tile and spline. Safe to call at any time; the
        /// component calls it by itself whenever something it depends on changed.
        /// </summary>
        public void Rebuild()
        {
            // Cleared first, so a build that fails reports once per change instead of once per frame.
            isDirty = false;

            if (splineContainer == null || meshFilter == null)
            {
                return;
            }

            // Before anything is measured: the connections move the spline itself, so lanes and mesh
            // have to be derived from the shape it has after docking, not before.
            ApplyConnections();
            MeasureCurve();

            if (sourceMesh == null)
            {
                Clear();
                return;
            }

            if (!sourceMesh.isReadable)
            {
                Debug.LogWarning(
                    $"'{sourceMesh.name}' cannot be read. Enable Read/Write in its model import settings.",
                    this);
                Clear();
                return;
            }

            if (generatedMesh == null)
            {
                generatedMesh = new Mesh
                {
                    name = $"{name} (Street)",

                    // Belongs to this component alone and is rebuilt on load, so it must never be
                    // written into the scene or the prefab.
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            if (!StreetMeshBuilder.TryBuild(generatedMesh, sourceMesh, splineContainer.Spline, forwardAxis, tileLength))
            {
                Clear();
                return;
            }

            meshFilter.sharedMesh = generatedMesh;
            ApplyCollider();
        }

        /// <summary>
        /// Records how sharply the spline bends and how wide the tile is, so the warning can be drawn
        /// without re-scanning the spline on every repaint.
        /// </summary>
        private void MeasureCurve()
        {
            tileSize = StreetMeshBuilder.MeasureTile(sourceMesh, forwardAxis);
            lanes = StreetLaneBuilder.Build(splineContainer.Spline, roadWidth);
            minimumRadius = StreetCurvature.MinimumRadius(splineContainer.Spline, out float tightestT);

            if (splineContainer.Spline != null && splineContainer.Spline.Count >= 2)
            {
                float3 position = splineContainer.Spline.EvaluatePosition(tightestT);
                tightestLocalPosition = new Vector3(position.x, position.y, position.z);
            }
        }

        private void OnDrawGizmos()
        {
            // Drawn unselected on purpose, but only when something is actually wrong - a road within
            // its limits stays invisible, so the marker means something wherever it shows up.
            if (!HasTightCurve)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.55f, 0.1f);
            Gizmos.DrawWireSphere(TightestPoint, Mathf.Max(0.5f, roadWidth * 0.5f));
        }

        private void Clear()
        {
            meshFilter.sharedMesh = null;

            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
            }
        }

        private void ApplyCollider()
        {
            if (meshCollider == null)
            {
                return;
            }

            meshCollider.enabled = generateCollider;
            if (!generateCollider)
            {
                meshCollider.sharedMesh = null;
                return;
            }

            // Reassigned via null on purpose: handing a MeshCollider the same Mesh reference again
            // does not make it re-cook the changed geometry.
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = generatedMesh;
        }

        private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            // The event is static and fires for every spline in the project, so it has to be filtered.
            if (splineContainer == null)
            {
                return;
            }

            for (int index = 0; index < splineContainer.Splines.Count; index++)
            {
                if (splineContainer.Splines[index] == spline)
                {
                    isDirty = true;
                    return;
                }
            }
        }
    }
}
