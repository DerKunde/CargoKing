using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Runs a bake end to end: validate, collect, index, hash, save.
    ///
    /// Split from <see cref="StreetNetworkBaker"/> on purpose - the baker is pure and testable with
    /// nothing but scene objects, while this touches the asset database.
    /// </summary>
    public static class StreetNetworkBakeJob
    {
        private static readonly List<StreetSegment> segments = new List<StreetSegment>();
        private static readonly List<Intersection> intersections = new List<Intersection>();
        private static readonly List<StreetSpeedSign> signs = new List<StreetSpeedSign>();
        private static readonly List<StreetNetworkIssue> issues = new List<StreetNetworkIssue>();

        /// <summary>
        /// Goes into every hash. Raised whenever baked data gains a field, so assets baked before read as
        /// stale and the next save bakes them again instead of leaving the new field at zero.
        /// </summary>
        private const string FormatVersion = "3";

        /// <summary>
        /// Bakes a network into its asset.
        /// </summary>
        /// <returns>False when there is no asset to bake into or validation found an error.</returns>
        public static bool Bake(StreetNetworkAuthoring authoring)
        {
            if (authoring == null || authoring.asset == null)
            {
                Debug.LogWarning("Street network has no asset to bake into.", authoring);
                return false;
            }

            authoring.CollectSegments(segments);
            authoring.CollectIntersections(intersections);
            authoring.CollectSpeedSigns(signs);

            if (!StreetNetworkValidation.Run(segments, intersections, signs, issues))
            {
                for (int index = 0; index < issues.Count; index++)
                {
                    if (issues[index].severity == StreetNetworkIssueSeverity.Error)
                    {
                        Debug.LogError(issues[index].message, issues[index].target);
                    }
                }

                Debug.LogError("Street network not baked: fix the errors above first.", authoring);
                return false;
            }

            // Lanes are rebuilt from the spline rather than trusted: nothing derived is serialised, so
            // a segment loaded but never ticked has none yet.
            for (int index = 0; index < segments.Count; index++)
            {
                segments[index].Rebuild();
            }

            for (int index = 0; index < intersections.Count; index++)
            {
                intersections[index].Rebuild();
            }

            StreetNetworkBakeResult result = StreetNetworkBaker.Collect(segments, intersections);

            StreetNetworkSample[] samples = result.samples.ToArray();
            StreetNetworkLane[] lanes = result.lanes.ToArray();

            StreetNetworkGridData grid = StreetNetworkGrid.Build(
                samples, lanes, StreetNetworkGrid.DefaultCellSize);

            Undo.RecordObject(authoring.asset, "Bake Street Network");

            authoring.asset.Write(
                samples,
                lanes,
                result.exits.ToArray(),
                grid,
                ComputeHash(segments, intersections));

            EditorUtility.SetDirty(authoring.asset);
            AssetDatabase.SaveAssetIfDirty(authoring.asset);

            Debug.Log(
                $"Baked street network '{authoring.asset.name}': {lanes.Length} lanes, {samples.Length} samples.",
                authoring.asset);

            return true;
        }

        /// <summary>Whether the asset was baked from something other than what the scene holds now.</summary>
        public static bool IsStale(StreetNetworkAuthoring authoring)
        {
            if (authoring == null || authoring.asset == null)
            {
                return false;
            }

            authoring.CollectSegments(segments);
            authoring.CollectIntersections(intersections);

            return authoring.asset.ContentHash != ComputeHash(segments, intersections);
        }

        /// <summary>
        /// A hash of everything a bake reads. Two scenes with the same hash bake to the same network,
        /// which is what makes "is this asset stale" answerable without baking to find out.
        /// </summary>
        public static string ComputeHash(
            IReadOnlyList<StreetSegment> bakedSegments,
            IReadOnlyList<Intersection> bakedIntersections)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("format ").Append(FormatVersion).Append('\n');

            for (int index = 0; index < bakedSegments.Count; index++)
            {
                StreetSegment segment = bakedSegments[index];
                if (segment == null)
                {
                    continue;
                }

                builder.Append(segment.GetInstanceID()).Append('|');
                Append(builder, segment.transform.position);
                Append(builder, segment.transform.eulerAngles);
                builder.Append(segment.profile != null ? segment.profile.GetInstanceID() : 0).Append('|');
                Append(builder, segment.RoadWidth);
                Append(builder, segment.SpeedLimit);

                SplineContainer container = segment.GetComponent<SplineContainer>();
                if (container != null && container.Spline != null)
                {
                    Spline spline = container.Spline;

                    for (int knot = 0; knot < spline.Count; knot++)
                    {
                        BezierKnot value = spline[knot];
                        Append(builder, value.Position.x);
                        Append(builder, value.Position.y);
                        Append(builder, value.Position.z);
                        Append(builder, value.TangentIn.x);
                        Append(builder, value.TangentIn.z);
                        Append(builder, value.TangentOut.x);
                        Append(builder, value.TangentOut.z);
                    }
                }

                AppendConnector(builder, segment.startConnection);
                AppendConnector(builder, segment.endConnection);
                AppendSigns(builder, segment);
                builder.Append('\n');
            }

            for (int index = 0; index < bakedIntersections.Count; index++)
            {
                Intersection intersection = bakedIntersections[index];
                if (intersection == null)
                {
                    continue;
                }

                builder.Append(intersection.GetInstanceID()).Append('|');
                Append(builder, intersection.transform.position);
                Append(builder, intersection.transform.eulerAngles);

                for (int socket = 0; socket < intersection.Sockets.Count; socket++)
                {
                    IntersectionSocket entry = intersection.Sockets[socket];
                    Append(builder, entry.transform.localPosition);
                    Append(builder, entry.transform.localEulerAngles);
                    Append(builder, entry.roadWidth);
                }

                builder.Append('\n');
            }

            using (MD5 md5 = MD5.Create())
            {
                byte[] digest = md5.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                return System.BitConverter.ToString(digest).Replace("-", string.Empty);
            }
        }

        private static void AppendSigns(StringBuilder builder, StreetSegment segment)
        {
            Transform transform = segment.transform;

            for (int index = 0; index < transform.childCount; index++)
            {
                StreetSpeedSign sign = transform.GetChild(index).GetComponent<StreetSpeedSign>();
                if (sign == null)
                {
                    continue;
                }

                builder.Append("sign|").Append((int)sign.side).Append('|');
                Append(builder, sign.distance);
                Append(builder, sign.limitKmh);
            }
        }

        private static void AppendConnector(StringBuilder builder, StreetEndConnector connector)
        {
            if (connector == null)
            {
                builder.Append("none|");
                return;
            }

            builder.Append(connector.socket != null ? connector.socket.GetInstanceID() : 0).Append('|');
            builder.Append(connector.segment != null ? connector.segment.GetInstanceID() : 0).Append('|');
            builder.Append((int)connector.segmentEnd).Append('|');
        }

        private static void Append(StringBuilder builder, Vector3 value)
        {
            Append(builder, value.x);
            Append(builder, value.y);
            Append(builder, value.z);
        }

        /// <summary>
        /// Rounded to a millimetre before hashing. Floats wobble in their last bits when a transform is
        /// touched and put back; without rounding the network would read as stale after every click.
        /// </summary>
        private static void Append(StringBuilder builder, float value)
        {
            builder.Append(Mathf.Round(value * 1000f).ToString(CultureInfo.InvariantCulture)).Append('|');
        }
    }
}
