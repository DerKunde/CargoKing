using System.Collections.Generic;
using CargoKing.Diagnostics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoKing.Car.Editor
{
    /// <summary>
    /// Inspector for <see cref="EngineProfile"/>: the fields, then torque and power over rpm, and the
    /// torque each gear brings to the axle. In Play Mode both carry a one-second trail of the car
    /// being driven - on the full-load curve at full throttle, below it at part throttle.
    /// </summary>
    [CustomEditor(typeof(EngineProfile))]
    public class EngineProfileEditor : UnityEditor.Editor
    {
        private const int RefreshIntervalMs = 50;

        // Rpm step the peaks are searched with - finer than the drawn curve.
        private const float PeakSearchStep = 5f;

        private LineGraph torqueGraph;
        private Label torqueCaption;
        private Label torqueLive;
        private LineGraph driveGraph;
        private Label driveCaption;
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

            preview.Add(ProfileGraphs.Header("Torque (bold) and power (faint, scaled to the same height) over rpm"));
            torqueGraph = new LineGraph();
            preview.Add(torqueGraph);
            torqueCaption = ProfileGraphs.Caption();
            preview.Add(torqueCaption);
            torqueLive = ProfileGraphs.LiveLabel();
            preview.Add(torqueLive);

            preview.Add(ProfileGraphs.Header("Drive torque at the axle, one line per gear"));
            driveGraph = new LineGraph();
            preview.Add(driveGraph);
            driveCaption = ProfileGraphs.Caption();
            preview.Add(driveCaption);

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
            var profile = (EngineProfile)target;
            CarController car = TuningContext.Car;

            RefreshTorque(profile);
            RefreshDrive(profile, car);
            RefreshLive(profile);
            ShowWarnings(profile);
        }

        private void RefreshTorque(EngineProfile profile)
        {
            float maxRpm = Mathf.Max(profile.maxRevolutions, 1f);

            float peakTorque = 0f, peakTorqueRpm = 0f, peakPower = 0f, peakPowerRpm = 0f;
            for (float rpm = 0f; rpm <= maxRpm; rpm += PeakSearchStep)
            {
                float torque = profile.TorqueAt(rpm);
                float power = EngineMath.PowerWatts(torque, rpm);
                if (torque > peakTorque)
                {
                    peakTorque = torque;
                    peakTorqueRpm = rpm;
                }
                if (power > peakPower)
                {
                    peakPower = power;
                    peakPowerRpm = rpm;
                }
            }

            // Power shares the torque axis, scaled so both peaks reach the same height: here the
            // shape is what matters, the caption carries the numbers.
            float powerScale = peakPower > 0f ? peakTorque / peakPower : 0f;

            var torqueCurve = new Vector2[ProfileGraphs.Samples + 1];
            var powerCurve = new Vector2[ProfileGraphs.Samples + 1];
            for (int i = 0; i <= ProfileGraphs.Samples; i++)
            {
                float rpm = maxRpm * i / ProfileGraphs.Samples;
                float torque = profile.TorqueAt(rpm);
                torqueCurve[i] = new Vector2(rpm, torque);
                powerCurve[i] = new Vector2(rpm, EngineMath.PowerWatts(torque, rpm) * powerScale);
            }

            float yMax = ProfileGraphs.RoundUp(Mathf.Max(peakTorque, 1f) * 1.1f, ProfileGraphs.NiceStep(Mathf.Max(peakTorque, 1f) / 4f));
            torqueGraph.ClearCurves();
            torqueGraph.SetRange(Vector2.zero, new Vector2(maxRpm, yMax), ProfileGraphs.NiceStep(yMax / 5f));
            torqueGraph.AddCurve(powerCurve, 1f, 0.45f);
            torqueGraph.AddCurve(torqueCurve, 2f);
            torqueGraph.AddMark(new Vector2(peakTorqueRpm, peakTorque));
            torqueGraph.AddMark(new Vector2(peakPowerRpm, peakPower * powerScale));

            float kilowatts = peakPower / 1000f;
            torqueCaption.text = $"Peak torque {peakTorque:F1} N·m at {peakTorqueRpm:F0} rpm - peak power {kilowatts:F1} kW "
                + $"({kilowatts * EngineMath.KilowattsToPs:F0} PS) at {peakPowerRpm:F0} rpm";
        }

        private void RefreshDrive(EngineProfile profile, CarController car)
        {
            GearboxProfile gearbox = TuningContext.GearboxOf(car);
            driveGraph.ClearCurves();

            if (gearbox == null || gearbox.GearCount == 0)
            {
                driveGraph.style.display = DisplayStyle.None;
                driveCaption.text = car == null
                    ? "No car in the open scene - the drive torque needs its gearbox."
                    : $"{car.name} has no Gearbox Profile with gears assigned.";
                return;
            }

            driveGraph.style.display = DisplayStyle.Flex;

            float idle = profile.idleRevolutions;
            float maxRpm = Mathf.Max(profile.maxRevolutions, idle + 1f);
            int? engaged = TuningContext.EngagedGear(car);

            var lines = new List<Vector2[]>();
            var linePeaks = new List<float>();
            float yMax = 1f;
            for (int gear = 0; gear <= gearbox.GearCount; gear++)
            {
                float factor = gearbox.Ratio(gear) * gearbox.axleRatio * gearbox.efficiency;
                var points = new Vector2[ProfileGraphs.Samples + 1];
                float linePeak = 0f;
                for (int i = 0; i <= ProfileGraphs.Samples; i++)
                {
                    float rpm = Mathf.Lerp(idle, maxRpm, (float)i / ProfileGraphs.Samples);
                    float axleTorque = profile.TorqueAt(rpm) * factor;
                    points[i] = new Vector2(rpm, axleTorque);
                    linePeak = Mathf.Max(linePeak, axleTorque);
                }

                lines.Add(points);
                linePeaks.Add(linePeak);
                yMax = Mathf.Max(yMax, linePeak);
            }

            yMax = ProfileGraphs.RoundUp(yMax * 1.1f, ProfileGraphs.NiceStep(yMax / 4f));
            driveGraph.SetRange(Vector2.zero, new Vector2(maxRpm, yMax), ProfileGraphs.NiceStep(yMax / 5f));

            // Faint ones first so the engaged gear draws on top.
            for (int gear = 0; gear < lines.Count; gear++)
            {
                if (gear != engaged)
                {
                    driveGraph.AddCurve(lines[gear], 1f, 0.35f);
                }
            }
            if (engaged.HasValue && engaged.Value >= 0 && engaged.Value < lines.Count)
            {
                driveGraph.AddCurve(lines[engaged.Value], 2f);
            }

            driveCaption.text = engaged.HasValue && engaged.Value < linePeaks.Count
                ? $"Engaged: {ProfileGraphs.GearName(engaged.Value)} - up to {linePeaks[engaged.Value]:F0} N·m at the axle (gearbox of {car.name})"
                : $"1st up to {linePeaks[1]:F0} N·m, top gear up to {linePeaks[gearbox.GearCount]:F0} N·m at the axle (gearbox of {car.name}). "
                  + "In Play Mode the engaged gear is bold.";
        }

        private void RefreshLive(EngineProfile profile)
        {
            CarController car = VehicleLiveSampler.CurrentCar;
            if (car == null || car.carEngine == null || car.carEngine.engineProfile != profile)
            {
                torqueGraph.SetTrails(System.Array.Empty<ProfileGraphs.Trail>());
                driveGraph.SetTrails(System.Array.Empty<ProfileGraphs.Trail>());
                torqueLive.text = VehicleLiveSampler.Status ?? $"{car.name} drives with a different engine profile.";
                return;
            }

            RingBuffer<EngineSample> samples = VehicleLiveSampler.Engine;
            var torque = new Vector2[samples.Count];
            var drive = new Vector2[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                EngineSample sample = samples[i];
                torque[i] = new Vector2(sample.Rpm, sample.NetTorque);
                drive[i] = new Vector2(sample.Rpm, sample.DriveTorque);
            }

            torqueGraph.SetTrails(new[] { new ProfileGraphs.Trail(torque, ProfileGraphs.LiveColor, false) });
            driveGraph.SetTrails(new[] { new ProfileGraphs.Trail(drive, ProfileGraphs.LiveColor, false) });
            torqueLive.text = $"<color={ProfileGraphs.ColorTag(ProfileGraphs.LiveColor)}>●</color> {car.name}, last second";
        }

        private void ShowWarnings(EngineProfile profile)
        {
            TorqueCurve curve = profile.torque;
            var messages = new List<string>();

            if (curve.riseShape <= 1f || curve.fallShape <= 1f)
            {
                messages.Add("A shape at or below 1 puts a kink at the peak - above 1 both halves leave it flat.");
            }

            if (curve.torqueAtIdle < 0f || curve.peakTorque < 0f || curve.torqueAtMaxRpm < 0f)
            {
                messages.Add("Torque values have to be positive - the curve is clamped at 0.");
            }

            if (profile.maxRevolutions <= profile.idleRevolutions + 2f)
            {
                messages.Add("Max rpm has to lie above idle rpm - until it does, the torque is flat at the peak value.");
            }
            else if (curve.peakRpm <= profile.idleRevolutions || curve.peakRpm >= profile.maxRevolutions)
            {
                messages.Add("Peak rpm lies outside idle to max rpm and is held just inside it.");
            }

            if (profile.stallRpm >= profile.idleRevolutions)
            {
                messages.Add("Stall rpm at or above idle rpm - the engine stalls as soon as it idles.");
            }

            if (profile.revLimiterFadeRange <= 0f)
            {
                messages.Add("The rev limiter fade range has to be positive.");
            }

            ProfileGraphs.ShowWarnings(warnings, messages, ref shownWarnings);
        }
    }
}
