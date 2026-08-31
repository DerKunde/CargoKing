using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace CargoKing.Streets
{
    /// <summary>
    /// The local axis of a source mesh that points along the direction of travel.
    /// </summary>
    public enum StreetMeshAxis
    {
        X,
        Y,
        Z,
    }

    /// <summary>
    /// Repeats a source mesh along a spline, bending every copy to follow the curve.
    ///
    /// Static and free of any Unity lifecycle on purpose: mesh and spline in, geometry out. This
    /// is the part of the street tool that can silently produce wrong geometry, so it has to be
    /// testable without opening a scene.
    /// </summary>
    public static class StreetMeshBuilder
    {
        /// <summary>Shortest length this can work with. Below it a tile count is meaningless.</summary>
        private const float MinimumLength = 0.001f;

        /// <summary>Cap on generated tiles, so a mistyped tile length cannot lock the editor up.</summary>
        private const int MaximumTileCount = 4096;

        // Reused across builds. A rebuild runs on every spline edit - main thread only - so keeping
        // the buffers avoids re-allocating all of them on every frame of a drag.
        private static readonly List<Vector3> sourcePositions = new List<Vector3>();
        private static readonly List<Vector3> sourceNormals = new List<Vector3>();
        private static readonly List<Vector4> sourceTangents = new List<Vector4>();
        private static readonly List<Vector2> sourceUvs = new List<Vector2>();
        private static readonly List<int> sourceIndices = new List<int>();

        private static readonly List<Vector3> positions = new List<Vector3>();
        private static readonly List<Vector3> normals = new List<Vector3>();
        private static readonly List<Vector4> tangents = new List<Vector4>();
        private static readonly List<Vector2> uvs = new List<Vector2>();
        private static readonly List<int> indices = new List<int>();

        /// <summary>
        /// Fills <paramref name="target"/> with <paramref name="source"/> repeated along the spline.
        ///
        /// The tile count is rounded to a whole number and the tile length stretched to match, so the
        /// road always ends on a tile boundary. A partial tile at the end would cut lane markings in
        /// half, which is far more visible than the sub-percent length change the stretch costs.
        /// </summary>
        /// <param name="target">Mesh to overwrite. Not modified when this returns false.</param>
        /// <param name="source">Tile mesh. Needs Read/Write enabled on its import settings.</param>
        /// <param name="spline">Path to bend along, in the same local space as the target.</param>
        /// <param name="forwardAxis">Axis of the source mesh that points along the direction of travel.</param>
        /// <param name="tileLengthOverride">Nominal tile length in metres, or 0 to measure it from the mesh.</param>
        /// <returns>True when the target was rebuilt.</returns>
        public static bool TryBuild(
            Mesh target,
            Mesh source,
            ISpline spline,
            StreetMeshAxis forwardAxis,
            float tileLengthOverride = 0f)
        {
            if (target == null || source == null || !source.isReadable || spline == null || spline.Count < 2)
            {
                return false;
            }

            float splineLength = spline.GetLength();
            if (splineLength < MinimumLength)
            {
                return false;
            }

            source.GetVertices(sourcePositions);
            if (sourcePositions.Count == 0)
            {
                return false;
            }

            source.GetNormals(sourceNormals);
            source.GetTangents(sourceTangents);
            source.GetUVs(0, sourceUvs);

            int vertexCountPerTile = sourcePositions.Count;
            bool hasNormals = sourceNormals.Count == vertexCountPerTile;
            bool hasTangents = sourceTangents.Count == vertexCountPerTile;
            bool hasUvs = sourceUvs.Count == vertexCountPerTile;

            float measuredTileLength = ToCanonicalSpace(forwardAxis, hasNormals, hasTangents, out float minAlong);
            if (measuredTileLength < MinimumLength)
            {
                return false;
            }

            float nominalTileLength = tileLengthOverride > MinimumLength ? tileLengthOverride : measuredTileLength;
            int tileCount = Mathf.Clamp(Mathf.RoundToInt(splineLength / nominalTileLength), 1, MaximumTileCount);
            float effectiveTileLength = splineLength / tileCount;

            positions.Clear();
            normals.Clear();
            tangents.Clear();
            uvs.Clear();

            for (int tile = 0; tile < tileCount; tile++)
            {
                for (int vertex = 0; vertex < vertexCountPerTile; vertex++)
                {
                    Vector3 local = sourcePositions[vertex];

                    // Where this vertex sits along its own tile, and from that where the tile sits
                    // along the spline. Unity's normalized t is arc length parameterised, so dividing
                    // by the total length lands on an evenly spaced point.
                    float along = (local.z - minAlong) / measuredTileLength;
                    float t = Mathf.Clamp01((tile + along) * effectiveTileLength / splineLength);

                    spline.Evaluate(t, out float3 splinePosition, out float3 splineTangent, out float3 splineUp);
                    Quaternion frame = StreetFrame.At(splineTangent, splineUp);

                    Vector3 origin = new Vector3(splinePosition.x, splinePosition.y, splinePosition.z);
                    positions.Add(origin + frame * new Vector3(local.x, local.y, 0f));

                    if (hasNormals)
                    {
                        normals.Add(frame * sourceNormals[vertex]);
                    }

                    if (hasTangents)
                    {
                        Vector4 sourceTangent = sourceTangents[vertex];
                        Vector3 rotated = frame * new Vector3(sourceTangent.x, sourceTangent.y, sourceTangent.z);
                        tangents.Add(new Vector4(rotated.x, rotated.y, rotated.z, sourceTangent.w));
                    }

                    if (hasUvs)
                    {
                        // Copied unchanged, so the tile's texture repeats once per tile by itself.
                        uvs.Add(sourceUvs[vertex]);
                    }
                }
            }

            target.Clear();
            target.indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            target.SetVertices(positions);

            if (hasNormals)
            {
                target.SetNormals(normals);
            }

            if (hasTangents)
            {
                target.SetTangents(tangents);
            }

            if (hasUvs)
            {
                target.SetUVs(0, uvs);
            }

            // Submeshes are kept apart so a tile split into asphalt and markings keeps both materials.
            target.subMeshCount = source.subMeshCount;
            for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
            {
                source.GetTriangles(sourceIndices, subMesh);

                indices.Clear();
                for (int tile = 0; tile < tileCount; tile++)
                {
                    int offset = tile * vertexCountPerTile;
                    for (int index = 0; index < sourceIndices.Count; index++)
                    {
                        indices.Add(sourceIndices[index] + offset);
                    }
                }

                target.SetTriangles(indices, subMesh, false);
            }

            if (!hasNormals)
            {
                target.RecalculateNormals();
            }

            target.RecalculateBounds();
            return true;
        }

        /// <summary>
        /// The tile's extent in canonical space: x is how wide the road is, y how tall, z how long.
        ///
        /// Read from the mesh bounds rather than the vertices, so it works on a mesh without read
        /// access and costs nothing. That is exact here because the canonical rotation only ever
        /// turns by a multiple of 90 degrees, which maps a bounding box onto another bounding box.
        /// </summary>
        public static Vector3 MeasureTile(Mesh source, StreetMeshAxis forwardAxis)
        {
            if (source == null)
            {
                return Vector3.zero;
            }

            Vector3 rotated = CanonicalRotation(forwardAxis) * source.bounds.size;
            return new Vector3(Mathf.Abs(rotated.x), Mathf.Abs(rotated.y), Mathf.Abs(rotated.z));
        }

        /// <summary>
        /// Rotates the buffered source data into canonical space, where +Z runs along the road, +Y is
        /// up and +X is lateral, and reports how long the tile is along that axis.
        /// </summary>
        /// <returns>The tile's extent along the road, in metres.</returns>
        private static float ToCanonicalSpace(StreetMeshAxis forwardAxis, bool hasNormals, bool hasTangents, out float minAlong)
        {
            Quaternion toCanonical = CanonicalRotation(forwardAxis);

            minAlong = float.MaxValue;
            float maxAlong = float.MinValue;

            for (int vertex = 0; vertex < sourcePositions.Count; vertex++)
            {
                Vector3 rotated = toCanonical * sourcePositions[vertex];
                sourcePositions[vertex] = rotated;

                if (rotated.z < minAlong)
                {
                    minAlong = rotated.z;
                }

                if (rotated.z > maxAlong)
                {
                    maxAlong = rotated.z;
                }
            }

            if (hasNormals)
            {
                for (int vertex = 0; vertex < sourceNormals.Count; vertex++)
                {
                    sourceNormals[vertex] = toCanonical * sourceNormals[vertex];
                }
            }

            if (hasTangents)
            {
                for (int vertex = 0; vertex < sourceTangents.Count; vertex++)
                {
                    Vector4 tangent = sourceTangents[vertex];
                    Vector3 rotated = toCanonical * new Vector3(tangent.x, tangent.y, tangent.z);
                    sourceTangents[vertex] = new Vector4(rotated.x, rotated.y, rotated.z, tangent.w);
                }
            }

            return maxAlong - minAlong;
        }

        /// <summary>
        /// Rotation that brings a source mesh into canonical space. Deliberately a rotation rather
        /// than an axis swap: swapping two axes mirrors the mesh, which flips the winding of every
        /// triangle and turns the road inside out.
        /// </summary>
        private static Quaternion CanonicalRotation(StreetMeshAxis forwardAxis)
        {
            switch (forwardAxis)
            {
                case StreetMeshAxis.X:
                    return Quaternion.Euler(0f, -90f, 0f);
                case StreetMeshAxis.Y:
                    return Quaternion.Euler(90f, 0f, 0f);
                default:
                    return Quaternion.identity;
            }
        }

    }
}
