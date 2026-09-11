using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoKing.Car.Editor
{
    /// <summary>
    /// What the profile inspectors share: stylesheet, labels, colours, axis steps, warnings, and the
    /// Painter2D helpers that draw curves, markers and live trails - so every graph looks alike.
    /// </summary>
    internal static class ProfileGraphs
    {
        /// <summary>Points per drawn curve.</summary>
        public const int Samples = 200;

        public static readonly Color PeakColor = new Color(0.91f, 0.64f, 0.24f);
        public static readonly Color LiveColor = new Color(0.30f, 0.69f, 0.94f);

        /// <summary>One colour per wheel slot: FL, FR, RL, RR.</summary>
        public static readonly Color[] WheelColors =
        {
            new Color(0.30f, 0.69f, 0.94f),
            new Color(0.45f, 0.82f, 0.45f),
            new Color(0.94f, 0.42f, 0.40f),
            new Color(0.78f, 0.52f, 0.94f),
        };

        /// <summary>A live trail in data units, oldest point first; the newest gets the dot.</summary>
        public readonly struct Trail
        {
            public readonly Vector2[] Points;
            public readonly Color Color;
            public readonly bool HollowHead;

            public Trail(Vector2[] points, Color color, bool hollowHead)
            {
                Points = points;
                Color = color;
                HollowHead = hollowHead;
            }
        }

        /// <summary>ProfileGraphs.uss, found next to the calling editor's script so a folder move keeps it working.</summary>
        public static StyleSheet LoadStyleSheet(UnityEditor.Editor editor)
        {
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(editor));
            if (string.IsNullOrEmpty(scriptPath))
            {
                return null;
            }

            string folder = Path.GetDirectoryName(scriptPath)?.Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<StyleSheet>($"{folder}/ProfileGraphs.uss");
        }

        public static Label Header(string text) => WithClass(new Label(text), "profile-preview-header");
        public static Label Caption() => WithClass(new Label(string.Empty), "profile-preview-caption");
        public static Label Legend(string text) => WithClass(new Label(text), "profile-preview-legend");

        /// <summary>Line under a graph naming what the live trails show, or why there are none.</summary>
        public static Label LiveLabel() => WithClass(new Label(string.Empty), "profile-live");

        /// <summary>A round axis step near <paramref name="roughStep"/>: 1, 2 or 5 times a power of ten.</summary>
        public static float NiceStep(float roughStep)
        {
            if (roughStep <= 0f)
            {
                return 1f;
            }

            float magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(roughStep)));
            float normalized = roughStep / magnitude;
            float nice = normalized <= 1f ? 1f : normalized <= 2f ? 2f : normalized <= 5f ? 5f : 10f;
            return nice * magnitude;
        }

        public static float RoundUp(float value, float step) => Mathf.Ceil(value / step) * step;

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }

        /// <summary>Hex colour for a Label's rich-text &lt;color&gt; tag, e.g. "#4DB0F0".</summary>
        public static string ColorTag(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

        public static string GearName(int gear) => gear == 0 ? "R" : gear.ToString();

        /// <summary>
        /// Replaces the HelpBoxes in <paramref name="container"/> only when the messages changed -
        /// the graphs refresh many times a second and rebuilding every time would flicker.
        /// </summary>
        public static void ShowWarnings(VisualElement container, List<string> messages, ref string shown)
        {
            string joined = string.Join("\n", messages);
            if (joined == shown)
            {
                return;
            }

            shown = joined;
            container.Clear();
            foreach (string message in messages)
            {
                container.Add(new HelpBox(message, HelpBoxMessageType.Warning));
            }
        }

        public static void DrawPolyline(Painter2D painter, IReadOnlyList<Vector2> points, Color color, float width)
        {
            if (points.Count < 2)
            {
                return;
            }

            painter.lineWidth = width;
            painter.strokeColor = color;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (int i = 1; i < points.Count; i++)
            {
                painter.LineTo(points[i]);
            }
            painter.Stroke();
        }

        public static void DrawDot(Painter2D painter, Vector2 center, Color color, float radius, bool hollow)
        {
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            if (hollow)
            {
                painter.lineWidth = 1.5f;
                painter.strokeColor = color;
                painter.Stroke();
            }
            else
            {
                painter.fillColor = color;
                painter.Fill();
            }
        }

        /// <summary>Segments fade in from the oldest (transparent) to the newest; the newest point gets the dot.</summary>
        public static void DrawTrail(Painter2D painter, IReadOnlyList<Vector2> points, Color color, bool hollowHead)
        {
            for (int i = 1; i < points.Count; i++)
            {
                float age = (float)i / (points.Count - 1);
                painter.lineWidth = 1.5f;
                painter.strokeColor = WithAlpha(color, 0.8f * age);
                painter.BeginPath();
                painter.MoveTo(points[i - 1]);
                painter.LineTo(points[i]);
                painter.Stroke();
            }

            if (points.Count > 0)
            {
                DrawDot(painter, points[points.Count - 1], color, 4f, hollowHead);
            }
        }

        private static Label WithClass(Label label, string className)
        {
            label.AddToClassList(className);
            return label;
        }
    }
}
