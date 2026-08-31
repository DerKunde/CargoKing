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

        /// <summary>
        /// Multiple of the road's width below which a swept mesh starts to look wrong. A ribbon bent
        /// to radius R compresses its inner edge by 1 - halfWidth/R, so at three times the full width
        /// the inside is already about a sixth shorter than the centre line. Tighter than that belongs
        /// in an intersection prefab, whose corner geometry is modelled rather than swept.
        /// </summary>
        private const float WarningRadiusPerWidth = 3f;

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
            if (isDirty)
            {
                Rebuild();
            }
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
