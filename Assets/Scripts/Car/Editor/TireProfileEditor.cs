using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoKing.Car.Editor
{
    /// <summary>
    /// Inspector for <see cref="TireProfile"/>: the usual fields, then both Pacejka curves drawn
    /// out with their peaks marked. B, C and E do not read as numbers - this shows what they do.
    /// Redraws on every change, Play Mode included, so a curve can be tuned while driving.
    /// </summary>
    [CustomEditor(typeof(TireProfile))]
    public class TireProfileEditor : UnityEditor.Editor
    {
        private TireCurveGraph longitudinalGraph;
        private TireCurveGraph lateralGraph;
        private Label longitudinalPeakLabel;
        private Label lateralPeakLabel;
        private Label liveLegend;
        private VisualElement warnings;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            StyleSheet styleSheet = ProfileGraphs.LoadStyleSheet(this);
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            var preview = new VisualElement();
            preview.AddToClassList("profile-preview");

            preview.Add(CreateLabel("Longitudinal: force per load over slip ratio, 0 - 100 %", "profile-preview-header"));
            longitudinalGraph = new TireCurveGraph();
            preview.Add(longitudinalGraph);
            longitudinalPeakLabel = CreateLabel(string.Empty, "profile-preview-caption");
            preview.Add(longitudinalPeakLabel);

            preview.Add(CreateLabel("Lateral: force per load over slip angle, 0 - 30°", "profile-preview-header"));
            lateralGraph = new TireCurveGraph();
            preview.Add(lateralGraph);
            lateralPeakLabel = CreateLabel(string.Empty, "profile-preview-caption");
            preview.Add(lateralPeakLabel);

            preview.Add(CreateLabel("Bold: nominal load. Faint: 0.5 x and 1.5 x nominal load. Marker: the peak combined slip is normalised with.", "profile-preview-legend"));

            liveLegend = ProfileGraphs.LiveLabel();
            preview.Add(liveLegend);

            warnings = new VisualElement();
            preview.Add(warnings);

            root.Add(preview);

            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            root.schedule.Execute(RefreshLive).Every(50);
            Refresh();
            RefreshLive();
            return root;
        }

        /// <summary>
        /// One trail per wheel of the car being driven that uses this profile. x is the slip, y the
        /// friction coefficient actually used - force over load - so where grip is shared between
        /// driving and cornering the point sits below the curve. Braking slip shows a hollow head.
        /// </summary>
        private void RefreshLive()
        {
            var profile = (TireProfile)target;
            var longitudinal = new List<ProfileGraphs.Trail>();
            var lateral = new List<ProfileGraphs.Trail>();
            var legend = new StringBuilder();

            foreach (WheelTrace trace in VehicleLiveSampler.Wheels)
            {
                if (trace.Wheel == null || trace.Wheel.tireProfile != profile)
                {
                    continue;
                }

                var slipPoints = new List<Vector2>();
                var anglePoints = new List<Vector2>();
                for (int i = 0; i < trace.Samples.Count; i++)
                {
                    WheelSample sample = trace.Samples[i];
                    if (!sample.Grounded)
                    {
                        continue;
                    }

                    slipPoints.Add(new Vector2(Mathf.Abs(sample.SlipRatio), sample.LongitudinalMu));
                    anglePoints.Add(new Vector2(Mathf.Abs(sample.SlipAngle), sample.LateralMu));
                }

                if (slipPoints.Count == 0)
                {
                    continue;
                }

                bool braking = trace.Samples[trace.Samples.Count - 1].SlipRatio < 0f;
                longitudinal.Add(new ProfileGraphs.Trail(slipPoints.ToArray(), trace.Color, braking));
                lateral.Add(new ProfileGraphs.Trail(anglePoints.ToArray(), trace.Color, false));
                legend.Append($"<color={ProfileGraphs.ColorTag(trace.Color)}>●</color> {trace.Label}   ");
            }

            longitudinalGraph.SetTrails(longitudinal);
            lateralGraph.SetTrails(lateral);
            liveLegend.text = legend.Length > 0
                ? legend + "- last second, hollow: braking slip"
                : VehicleLiveSampler.Status ?? "No wheel of the car being driven uses this profile.";
        }

        private void Refresh()
        {
            var profile = (TireProfile)target;

            // OnValidate would do this too, but is not guaranteed to have run before this callback.
            profile.RecalculatePeaks();

            longitudinalGraph.SetCurve(profile.longitudinal, TireProfile.SlipRatioRange,
                profile.nominalLoad, profile.loadSensitivity, profile.PeakSlipRatio, profile.HasLongitudinalPeak);
            lateralGraph.SetCurve(profile.lateral, TireProfile.SlipAngleRange,
                profile.nominalLoad, profile.loadSensitivity, profile.PeakSlipAngle, profile.HasLateralPeak);

            longitudinalPeakLabel.text = PeakText(profile.longitudinal, profile.PeakSlipRatio, profile.HasLongitudinalPeak,
                TireProfile.SlipRatioRange, x => $"{x * 100f:F1} % slip", "100 % slip");
            lateralPeakLabel.text = PeakText(profile.lateral, profile.PeakSlipAngle, profile.HasLateralPeak,
                TireProfile.SlipAngleRange, x => $"{x:F1}°", "30°");

            RebuildWarnings(profile);
        }

        private static string PeakText(PacejkaCurve curve, float peak, bool hasPeak, float range, Func<float, string> format, string rangeEnd)
        {
            float atRangeEnd = TireMath.MagicFormula(range, curve);
            if (!hasPeak)
            {
                return $"No peak up to {rangeEnd} - still rising, μ {atRangeEnd:F2} there";
            }

            float atPeak = TireMath.MagicFormula(peak, curve);
            string share = atPeak > 0f ? $"{atRangeEnd / atPeak * 100f:F0} %" : "-";
            return $"Peak at {format(peak)}, μ {atPeak:F2} - {share} of it left at {rangeEnd}";
        }

        private void RebuildWarnings(TireProfile profile)
        {
            warnings.Clear();

            if (!profile.HasLongitudinalPeak)
            {
                AddWarning("The longitudinal curve has no peak up to 100 % slip (C has to be above 1, or B is too small). Combined slip uses 100 % as its peak until it has one.");
            }

            if (!profile.HasLateralPeak)
            {
                AddWarning("The lateral curve has no peak up to 30° (C has to be above 1, or B is too small). Combined slip uses 30° as its peak until it has one.");
            }

            if (profile.longitudinal.e > 1f || profile.lateral.e > 1f)
            {
                AddWarning("E above 1 makes the curve wavy before its peak.");
            }

            if (profile.nominalLoad <= 0f)
            {
                AddWarning("Nominal load has to be positive - load sensitivity is ignored until it is.");
            }
        }

        private void AddWarning(string message)
        {
            warnings.Add(new HelpBox(message, HelpBoxMessageType.Warning));
        }

        private static Label CreateLabel(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }
    }

    /// <summary>
    /// One Pacejka curve drawn as friction coefficient over its input, at 0.5, 1 and 1.5 times the
    /// nominal load, with the peak marked.
    /// </summary>
    internal class TireCurveGraph : VisualElement
    {
        private const int Samples = 200;

        // Faint ones first so the nominal curve draws on top.
        private static readonly float[] LoadMultipliers = { 0.5f, 1.5f, 1f };

        private PacejkaCurve curve;
        private float range = 1f;
        private float nominalLoad;
        private float sensitivity;
        private float peak;
        private bool hasPeak;

        private readonly List<ProfileGraphs.Trail> trails = new List<ProfileGraphs.Trail>();

        public TireCurveGraph()
        {
            AddToClassList("profile-graph");
            generateVisualContent += Draw;
        }

        public void SetCurve(PacejkaCurve curve, float range, float nominalLoad, float sensitivity, float peak, bool hasPeak)
        {
            this.curve = curve;
            this.range = range;
            this.nominalLoad = nominalLoad;
            this.sensitivity = sensitivity;
            this.peak = peak;
            this.hasPeak = hasPeak;
            MarkDirtyRepaint();
        }

        public void SetTrails(IEnumerable<ProfileGraphs.Trail> newTrails)
        {
            trails.Clear();
            trails.AddRange(newTrails);
            MarkDirtyRepaint();
        }

        private float LoadFactor(float multiplier)
        {
            return nominalLoad > 0f ? TireMath.LoadFactor(multiplier * nominalLoad, nominalLoad, sensitivity) : 1f;
        }

        private void Draw(MeshGenerationContext context)
        {
            float width = contentRect.width;
            float height = contentRect.height;
            if (width < 1f || height < 1f || range <= 0f)
            {
                return;
            }

            // Rounded up to the next 0.1 above the highest curve, so the plot keeps some headroom.
            float yMax = 0.1f;
            foreach (float multiplier in LoadMultipliers)
            {
                float factor = LoadFactor(multiplier);
                for (int i = 0; i <= Samples; i++)
                {
                    yMax = Mathf.Max(yMax, factor * TireMath.MagicFormula(range * i / Samples, curve));
                }
            }
            yMax = Mathf.Ceil(yMax * 1.1f * 10f) / 10f;

            Vector2 ToPoint(float x, float mu) => new Vector2(x / range * width, height - mu / yMax * height);

            Painter2D painter = context.painter2D;
            Color ink = resolvedStyle.color;

            painter.lineWidth = 1f;
            painter.strokeColor = ProfileGraphs.WithAlpha(ink, 0.15f);
            for (float mu = 0.2f; mu < yMax; mu += 0.2f)
            {
                painter.BeginPath();
                painter.MoveTo(ToPoint(0f, mu));
                painter.LineTo(ToPoint(range, mu));
                painter.Stroke();
            }

            foreach (float multiplier in LoadMultipliers)
            {
                bool nominal = Mathf.Approximately(multiplier, 1f);
                float factor = LoadFactor(multiplier);

                painter.lineWidth = nominal ? 2f : 1f;
                painter.strokeColor = nominal ? ink : ProfileGraphs.WithAlpha(ink, 0.35f);
                painter.lineJoin = LineJoin.Round;
                painter.BeginPath();
                painter.MoveTo(ToPoint(0f, 0f));
                for (int i = 1; i <= Samples; i++)
                {
                    float x = range * i / Samples;
                    painter.LineTo(ToPoint(x, factor * TireMath.MagicFormula(x, curve)));
                }
                painter.Stroke();
            }

            if (hasPeak)
            {
                Vector2 top = ToPoint(peak, TireMath.MagicFormula(peak, curve));

                painter.lineWidth = 1f;
                painter.strokeColor = ProfileGraphs.PeakColor;
                painter.BeginPath();
                painter.MoveTo(new Vector2(top.x, height));
                painter.LineTo(top);
                painter.Stroke();

                painter.fillColor = ProfileGraphs.PeakColor;
                painter.BeginPath();
                painter.Arc(top, 3.5f, Angle.Degrees(0f), Angle.Degrees(360f));
                painter.Fill();
            }

            // Live trails, held at the chart's edge when a wheel slips past the plotted range.
            foreach (ProfileGraphs.Trail trail in trails)
            {
                var points = new Vector2[trail.Points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = new Vector2(
                        Mathf.Clamp01(trail.Points[i].x / range) * width,
                        height - Mathf.Clamp01(trail.Points[i].y / yMax) * height);
                }

                ProfileGraphs.DrawTrail(painter, points, trail.Color, trail.HollowHead);
            }
        }
    }
}
