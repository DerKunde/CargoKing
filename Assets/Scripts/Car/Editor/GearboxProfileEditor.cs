using System.Collections.Generic;
using CargoKing.Diagnostics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoKing.Car.Editor
{
    /// <summary>
    /// Inspector for <see cref="GearboxProfile"/>: the fields, then road speed over engine rpm, one
    /// line per gear. In Play Mode a one-second trail of the car being driven: on the engaged
    /// gear's line while the clutch holds, off it while it slips.
    /// </summary>
    [CustomEditor(typeof(GearboxProfile))]
    public class GearboxProfileEditor : UnityEditor.Editor
    {
        private const int RefreshIntervalMs = 50;
        private const float FallbackWheelRadius = 0.31f;
        private const float FallbackMaxRpm = 6000f;
        private const float MsToKmh = 3.6f;

        private LineGraph speedGraph;
        private Label caption;
        private Label live;
        private VisualElement warnings;
        private string shownWarnings;

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
            preview.Add(ProfileGraphs.Header("Road speed over engine rpm, one line per gear"));
            speedGraph = new LineGraph();
            preview.Add(speedGraph);
            caption = ProfileGraphs.Caption();
            preview.Add(caption);
            live = ProfileGraphs.LiveLabel();
            preview.Add(live);
            warnings = new VisualElement();
            preview.Add(warnings);
            root.Add(preview);

            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            root.schedule.Execute(Refresh).Every(RefreshIntervalMs);
            Refresh();
            return root;
        }

        private void Refresh()
        {
            var profile = (GearboxProfile)target;
            CarController car = TuningContext.Car;
            EngineProfile engine = car != null && car.carEngine != null ? car.carEngine.engineProfile : null;
            float? carRadius = TuningContext.WheelRadiusOf(car);
            float radius = carRadius ?? FallbackWheelRadius;
            float maxRpm = engine != null ? Mathf.Max(engine.maxRevolutions, 1f) : FallbackMaxRpm;
            int? engaged = TuningContext.EngagedGear(car);

            var topSpeeds = new List<float>();
            float yMax = 1f;
            for (int gear = 0; gear <= profile.GearCount; gear++)
            {
                float kmh = DriveTrainMath.EngineRpmToRoadSpeed(maxRpm, profile.Ratio(gear) * profile.axleRatio, radius) * MsToKmh;
                topSpeeds.Add(kmh);
                yMax = Mathf.Max(yMax, kmh);
            }

            yMax = ProfileGraphs.RoundUp(yMax * 1.1f, ProfileGraphs.NiceStep(yMax / 4f));
            speedGraph.ClearCurves();
            speedGraph.SetRange(Vector2.zero, new Vector2(maxRpm, yMax), ProfileGraphs.NiceStep(yMax / 5f));

            // Speed is proportional to rpm in a gear, so each line is straight from the origin.
            for (int gear = 0; gear < topSpeeds.Count; gear++)
            {
                if (gear != engaged)
                {
                    speedGraph.AddCurve(new[] { Vector2.zero, new Vector2(maxRpm, topSpeeds[gear]) }, 1f, 0.35f);
                }
            }
            if (engaged.HasValue && engaged.Value >= 0 && engaged.Value < topSpeeds.Count)
            {
                speedGraph.AddCurve(new[] { Vector2.zero, new Vector2(maxRpm, topSpeeds[engaged.Value]) }, 2f);
            }

            var parts = new List<string>();
            for (int gear = 1; gear < topSpeeds.Count; gear++)
            {
                parts.Add($"{gear}: {topSpeeds[gear]:F0}");
            }
            parts.Add($"R: {topSpeeds[0]:F0}");

            string source = carRadius.HasValue
                ? $"{radius:F3} m wheel of {car.name}"
                : $"no car in the open scene, assuming a {FallbackWheelRadius} m wheel";
            string rpmSource = engine != null ? string.Empty : $", {FallbackMaxRpm:F0} rpm max assumed";
            caption.text = $"At {maxRpm:F0} rpm, km/h - {string.Join(" · ", parts)} ({source}{rpmSource})";

            RefreshLive(profile);
            ShowWarnings(profile);
        }

        private void RefreshLive(GearboxProfile profile)
        {
            CarController car = VehicleLiveSampler.CurrentCar;
            if (car == null || car.carEngine == null || car.carEngine.gearboxProfile != profile)
            {
                speedGraph.SetTrails(System.Array.Empty<ProfileGraphs.Trail>());
                live.text = VehicleLiveSampler.Status ?? $"{car.name} drives with a different gearbox profile.";
                return;
            }

            RingBuffer<EngineSample> samples = VehicleLiveSampler.Engine;
            var points = new Vector2[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                points[i] = new Vector2(samples[i].Rpm, samples[i].SpeedKmh);
            }

            speedGraph.SetTrails(new[] { new ProfileGraphs.Trail(points, ProfileGraphs.LiveColor, false) });
            live.text = $"<color={ProfileGraphs.ColorTag(ProfileGraphs.LiveColor)}>●</color> {car.name}, last second - "
                + $"gear {ProfileGraphs.GearName(car.carEngine.currentGear)}";
        }

        private void ShowWarnings(GearboxProfile profile)
        {
            var messages = new List<string>();

            if (profile.GearCount == 0)
            {
                messages.Add("No forward gears - the car can only reverse.");
            }

            for (int gear = 1; gear <= profile.GearCount; gear++)
            {
                if (profile.Ratio(gear) <= 0f)
                {
                    messages.Add($"Gear {gear} has no positive ratio - it passes no torque.");
                }
            }

            if (profile.reverseRatio <= 0f)
            {
                messages.Add("The reverse ratio has to be positive.");
            }

            if (profile.axleRatio <= 0f)
            {
                messages.Add("The final drive ratio has to be positive.");
            }

            ProfileGraphs.ShowWarnings(warnings, messages, ref shownWarnings);
        }
    }
}
