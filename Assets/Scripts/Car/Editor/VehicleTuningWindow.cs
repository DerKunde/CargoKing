using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoKing.Car.Editor
{
    /// <summary>
    /// Engine, gearbox and tire profile in one place, each with its inspector and live curves, for
    /// tuning while driving. Picks which asset to edit - never which one a car uses; that stays in
    /// the prefab. In Play Mode it takes the profiles of the car being driven and follows it when
    /// the player changes cars; a manual pick holds until then.
    /// </summary>
    public class VehicleTuningWindow : EditorWindow
    {
        [SerializeField] private EngineProfile engineProfile;
        [SerializeField] private GearboxProfile gearboxProfile;
        [SerializeField] private TireProfile tireProfile;

        private Section engine;
        private Section gearbox;
        private Section tire;
        private Label status;
        private Label tireNote;

        private sealed class Section
        {
            public readonly Foldout Foldout;
            public readonly ObjectField Field;

            public Section(Foldout foldout, ObjectField field)
            {
                Foldout = foldout;
                Field = field;
            }
        }

        [MenuItem("Window/CargoKing/Vehicle Tuning")]
        public static void Open()
        {
            GetWindow<VehicleTuningWindow>("Vehicle Tuning");
        }

        private void OnEnable()
        {
            VehicleLiveSampler.CurrentCarChanged += FollowCar;
        }

        private void OnDisable()
        {
            VehicleLiveSampler.CurrentCarChanged -= FollowCar;
        }

        public void CreateGUI()
        {
            var scroll = new ScrollView();
            rootVisualElement.Add(scroll);

            status = new Label();
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.marginBottom = 6;
            scroll.Add(status);

            engine = AddSection(scroll, "Engine", typeof(EngineProfile), engineProfile, asset => engineProfile = (EngineProfile)asset);
            gearbox = AddSection(scroll, "Gearbox", typeof(GearboxProfile), gearboxProfile, asset => gearboxProfile = (GearboxProfile)asset);
            tire = AddSection(scroll, "Tire", typeof(TireProfile), tireProfile, asset => tireProfile = (TireProfile)asset);

            tireNote = new Label();
            tireNote.style.whiteSpace = WhiteSpace.Normal;
            tire.Foldout.Insert(1, tireNote);

            FollowCar();
        }

        private static Section AddSection(VisualElement parent, string title, System.Type type, Object asset, System.Action<Object> store)
        {
            var foldout = new Foldout { text = title, value = true };
            var field = new ObjectField($"{title} profile") { objectType = type, allowSceneObjects = false };
            var body = new VisualElement();

            field.SetValueWithoutNotify(asset);
            field.RegisterValueChangedCallback(change =>
            {
                store(change.newValue);
                ShowInspector(body, change.newValue);
            });

            foldout.Add(field);
            foldout.Add(body);
            parent.Add(foldout);

            ShowInspector(body, asset);
            return new Section(foldout, field);
        }

        private static void ShowInspector(VisualElement body, Object asset)
        {
            body.Clear();
            if (asset != null)
            {
                body.Add(new InspectorElement(asset));
            }
        }

        private void FollowCar()
        {
            // Raised before CreateGUI has run, e.g. right after a domain reload.
            if (engine == null)
            {
                return;
            }

            tireNote.text = string.Empty;
            CarController car = VehicleLiveSampler.CurrentCar;
            if (car == null)
            {
                status.text = Application.isPlaying
                    ? VehicleLiveSampler.Status ?? "Waiting for the player's car."
                    : "Pick the profiles to edit. In Play Mode the window takes them from the car being driven.";
                return;
            }

            EngineProfile carEngineProfile = car.carEngine != null ? car.carEngine.engineProfile : null;
            GearboxProfile carGearboxProfile = car.carEngine != null ? car.carEngine.gearboxProfile : null;
            List<(string Label, TireProfile Profile)> tires = VehicleLiveSampler.Wheels
                .Where(trace => trace.Wheel != null)
                .Select(trace => (trace.Label, trace.Wheel.tireProfile))
                .ToList();

            engine.Field.value = carEngineProfile;
            gearbox.Field.value = carGearboxProfile;
            tire.Field.value = tires.Count > 0 ? tires[0].Profile : null;

            if (tires.Select(entry => entry.Profile).Distinct().Count() > 1)
            {
                tireNote.text = "The wheels use different tire profiles: "
                    + string.Join(", ", tires.Select(entry => $"{entry.Label} {(entry.Profile != null ? entry.Profile.name : "none")}"))
                    + $". Showing {tires[0].Label}'s.";
            }

            bool builtInDefaults = !IsAsset(carEngineProfile) || !IsAsset(carGearboxProfile)
                || tires.Any(entry => !IsAsset(entry.Profile));
            status.text = $"Following {car.name}. Edits apply on the next physics step and stay in the asset after Play Mode."
                + (builtInDefaults ? " Some profiles are built-in defaults - create and assign assets to keep what you tune." : string.Empty);
        }

        private static bool IsAsset(Object profile) => profile != null && AssetDatabase.Contains(profile);
    }
}
