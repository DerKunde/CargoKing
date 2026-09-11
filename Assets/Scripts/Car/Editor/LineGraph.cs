using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoKing.Car.Editor
{
    /// <summary>
    /// A plot in data units: curves, marked points and live trails over horizontal grid lines.
    /// Points outside the range are held at its edge, so a trail that runs off the chart stays
    /// visible there instead of vanishing.
    /// </summary>
    internal sealed class LineGraph : VisualElement
    {
        private readonly struct Curve
        {
            public readonly Vector2[] Points;
            public readonly float Width;
            public readonly float Alpha;
            public readonly Color? Color;

            public Curve(Vector2[] points, float width, float alpha, Color? color)
            {
                Points = points;
                Width = width;
                Alpha = alpha;
                Color = color;
            }
        }

        private readonly List<Curve> curves = new List<Curve>();
        private readonly List<Vector2> marks = new List<Vector2>();
        private readonly List<ProfileGraphs.Trail> trails = new List<ProfileGraphs.Trail>();
        private Vector2 min = Vector2.zero;
        private Vector2 max = Vector2.one;
        private float gridStep = 0.2f;

        public LineGraph()
        {
            AddToClassList("profile-graph");
            generateVisualContent += Draw;
        }

        public void SetRange(Vector2 min, Vector2 max, float gridStep)
        {
            this.min = min;
            this.max = max;
            this.gridStep = gridStep;
            MarkDirtyRepaint();
        }

        /// <summary>Removes curves and marks. Trails are replaced through <see cref="SetTrails"/>.</summary>
        public void ClearCurves()
        {
            curves.Clear();
            marks.Clear();
            MarkDirtyRepaint();
        }

        /// <summary>Adds a curve. Without a colour it is drawn in the text colour, so it follows the editor theme.</summary>
        public void AddCurve(Vector2[] points, float width, float alpha = 1f, Color? color = null)
        {
            curves.Add(new Curve(points, width, alpha, color));
            MarkDirtyRepaint();
        }

        /// <summary>A point marked with a dot and a line down to the x axis, like the tire curves' peaks.</summary>
        public void AddMark(Vector2 point)
        {
            marks.Add(point);
            MarkDirtyRepaint();
        }

        public void SetTrails(IEnumerable<ProfileGraphs.Trail> newTrails)
        {
            trails.Clear();
            trails.AddRange(newTrails);
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            float width = contentRect.width;
            float height = contentRect.height;
            if (width < 1f || height < 1f || max.x <= min.x || max.y <= min.y)
            {
                return;
            }

            Vector2 ToPoint(Vector2 value) => new Vector2(
                Mathf.Clamp01((value.x - min.x) / (max.x - min.x)) * width,
                height - Mathf.Clamp01((value.y - min.y) / (max.y - min.y)) * height);

            Vector2[] ToPoints(Vector2[] values)
            {
                var points = new Vector2[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    points[i] = ToPoint(values[i]);
                }
                return points;
            }

            Painter2D painter = context.painter2D;
            Color ink = resolvedStyle.color;

            if (gridStep > 0f)
            {
                for (float y = ProfileGraphs.RoundUp(min.y, gridStep); y < max.y; y += gridStep)
                {
                    if (y <= min.y)
                    {
                        continue;
                    }

                    ProfileGraphs.DrawPolyline(painter,
                        new[] { ToPoint(new Vector2(min.x, y)), ToPoint(new Vector2(max.x, y)) },
                        ProfileGraphs.WithAlpha(ink, 0.15f), 1f);
                }
            }

            foreach (Curve curve in curves)
            {
                ProfileGraphs.DrawPolyline(painter, ToPoints(curve.Points),
                    ProfileGraphs.WithAlpha(curve.Color ?? ink, curve.Alpha), curve.Width);
            }

            foreach (Vector2 mark in marks)
            {
                Vector2 top = ToPoint(mark);
                ProfileGraphs.DrawPolyline(painter, new[] { new Vector2(top.x, height), top }, ProfileGraphs.PeakColor, 1f);
                ProfileGraphs.DrawDot(painter, top, ProfileGraphs.PeakColor, 3.5f, false);
            }

            foreach (ProfileGraphs.Trail trail in trails)
            {
                ProfileGraphs.DrawTrail(painter, ToPoints(trail.Points), trail.Color, trail.HollowHead);
            }
        }
    }
}
