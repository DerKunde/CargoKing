using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CargoKing.Streets.Editor
{
    /// <summary>
    /// Paints every lane in a colour per posted limit while street drawings are on, so where a speed
    /// zone starts and ends shows without selecting anything.
    ///
    /// Built by the same baker pass that fills the network asset, so the band shows exactly what the
    /// AI will get. Rebuilt only when the network's hash changes, checked twice a second rather than
    /// on every repaint.
    /// </summary>
    [InitializeOnLoad]
    public static class StreetSpeedBand
    {
        /// <summary>A stretch of one lane with one limit.</summary>
        public struct Run
        {
            public Vector3[] points;
            public Color colour;
        }

        private const double CheckInterval = 0.5;
        private const float Lift = 0.3f;
        private const float Width = 5f;

        private static readonly Color ThirtyColour = new Color(0.25f, 0.55f, 1f);
        private static readonly Color FiftyColour = new Color(0.3f, 0.9f, 0.35f);
        private static readonly Color SeventyColour = new Color(1f, 0.9f, 0.2f);
        private static readonly Color HundredColour = new Color(1f, 0.55f, 0.1f);
        private static readonly Color FasterColour = new Color(1f, 0.2f, 0.2f);

        private static readonly List<Run> runs = new List<Run>();
        private static readonly List<StreetSegment> segments = new List<StreetSegment>();
        private static readonly List<Intersection> intersections = new List<Intersection>();

        private static string builtHash;
        private static double nextCheck;

        static StreetSpeedBand()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        /// <summary>Colour for a limit: up to 30 blue, 50 green, 70 yellow, 100 orange, faster red.</summary>
        public static Color ColourFor(float kilometresPerHour)
        {
            // Half a km/h of slack: limits arrive here converted to m/s and back.
            if (kilometresPerHour <= 30.5f)
            {
                return ThirtyColour;
            }

            if (kilometresPerHour <= 50.5f)
            {
                return FiftyColour;
            }

            if (kilometresPerHour <= 70.5f)
            {
                return SeventyColour;
            }

            return kilometresPerHour <= 100.5f ? HundredColour : FasterColour;
        }

        /// <summary>
        /// Cuts one lane into runs of equal limit. A run ends on the sample where the next limit starts,
        /// because a limit holds from its sample up to the next one - so consecutive runs touch.
        /// </summary>
        public static void BuildRuns(StreetNetworkSample[] samples, in StreetNetworkLane lane, List<Run> results)
        {
            if (lane.sampleCount < 2)
            {
                return;
            }

            int start = 0;

            for (int index = 1; index < lane.sampleCount; index++)
            {
                float startLimit = samples[lane.firstSample + start].speedLimit;
                bool changes = !Mathf.Approximately(samples[lane.firstSample + index].speedLimit, startLimit);
                bool last = index == lane.sampleCount - 1;

                if (!changes && !last)
                {
                    continue;
                }

                Vector3[] points = new Vector3[index - start + 1];
                for (int point = 0; point < points.Length; point++)
                {
                    points[point] = samples[lane.firstSample + start + point].position + Vector3.up * Lift;
                }

                results.Add(new Run { points = points, colour = ColourFor(startLimit * 3.6f) });
                start = index;
            }
        }

        private static void OnSceneGui(SceneView view)
        {
            if (!StreetDrawing.Enabled || Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup >= nextCheck)
            {
                nextCheck = EditorApplication.timeSinceStartup + CheckInterval;
                Refresh();
            }

            for (int index = 0; index < runs.Count; index++)
            {
                Handles.color = runs[index].colour;
                Handles.DrawAAPolyLine(Width, runs[index].points);
            }
        }

        private static void Refresh()
        {
            StreetNetworkAuthoring[] networks =
                Object.FindObjectsByType<StreetNetworkAuthoring>(FindObjectsSortMode.InstanceID);

            StringBuilder hash = new StringBuilder();
            for (int index = 0; index < networks.Length; index++)
            {
                networks[index].CollectSegments(segments);
                networks[index].CollectIntersections(intersections);
                hash.Append(StreetNetworkBakeJob.ComputeHash(segments, intersections));
            }

            string current = hash.ToString();
            if (current == builtHash)
            {
                return;
            }

            builtHash = current;
            runs.Clear();

            for (int index = 0; index < networks.Length; index++)
            {
                networks[index].CollectSegments(segments);
                networks[index].CollectIntersections(intersections);

                StreetNetworkBakeResult result = StreetNetworkBaker.Collect(segments, intersections);
                StreetNetworkSample[] samples = result.samples.ToArray();

                for (int lane = 0; lane < result.lanes.Count; lane++)
                {
                    BuildRuns(samples, result.lanes[lane], runs);
                }
            }
        }
    }
}
