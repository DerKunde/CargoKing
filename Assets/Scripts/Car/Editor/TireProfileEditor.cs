using System;
using System.IO;
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
        private VisualElement warnings;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            StyleSheet styleSheet = LoadStyleSheet();
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            var preview = new VisualElement();
            preview.AddToClassList("tire-preview");

            preview.Add(CreateLabel("Longitudinal: force per load over slip ratio, 0 - 100 %", "tire-preview-header"));
            longitudinalGraph = new TireCurveGraph();
            preview.Add(longitudinalGraph);
            longitudinalPeakLabel = CreateLabel(string.Empty, "tire-preview-caption");
            preview.Add(longitudinalPeakLabel);

            preview.Add(CreateLabel("Lateral: force per load over slip angle, 0 - 30°", "tire-preview-header"));
            lateralGraph = new TireCurveGraph();
            preview.Add(lateralGraph);
            lateralPeakLabel = CreateLabel(string.Empty, "tire-preview-caption");
            preview.Add(lateralPeakLabel);

            preview.Add(CreateLabel("Bold: nominal load. Faint: 0.5 x and 1.5 x nominal load. Marker: the peak combined slip is normalised with.", "tire-preview-legend"));

            warnings = new VisualElement();
            preview.Add(warnings);

            root.Add(preview);

            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            Refresh();
            return root;
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

        /// <summary>The stylesheet next to this script, found through the script's own asset path so a move keeps it working.</summary>
        private StyleSheet LoadStyleSheet()
        {
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            return string.IsNullOrEmpty(scriptPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<StyleSheet>(Path.ChangeExtension(scriptPath, ".uss"));
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
        private static readonly Color PeakColor = new Color(0.91f, 0.64f, 0.24f);

        private PacejkaCurve curve;
        private float range = 1f;
        private float nominalLoad;
        private float sensitivity;
        private float peak;
        private bool hasPeak;

        public TireCurveGraph()
        {
            AddToClassList("tire-graph");
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
            painter.strokeColor = WithAlpha(ink, 0.15f);
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
                painter.strokeColor = nominal ? ink : WithAlpha(ink, 0.35f);
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
                painter.strokeColor = PeakColor;
                painter.BeginPath();
                painter.MoveTo(new Vector2(top.x, height));
                painter.LineTo(top);
                painter.Stroke();

                painter.fillColor = PeakColor;
                painter.BeginPath();
                painter.Arc(top, 3.5f, Angle.Degrees(0f), Angle.Degrees(360f));
                painter.Fill();
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }
    }
}
