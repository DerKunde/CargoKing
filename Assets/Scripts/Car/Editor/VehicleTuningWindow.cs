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
    /// tuning while driving. The inspectors edit working copies (<see cref="ProfileWorkingCopies"/>):
    /// an asset changes only when its section's Save is pressed, and Revert takes the copy back to
    /// the asset. In Play Mode the car being driven is handed the copies, so it drives with the edits
    /// at once; unsaved copies outlive Play Mode.
    ///
    /// Picks which asset to edit - never which one a car uses; that stays in the prefab. In Play
    /// Mode it follows the car being driven; a manual pick holds until the next car change.
    /// </summary>
    public class VehicleTuningWindow : EditorWindow
    {
        private const int StateRefreshIntervalMs = 200;
        private const string UnsavedMark = "● unsaved";

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
            public readonly Label State;
            public readonly Button Save;
            public readonly Button Revert;

            public Section(Foldout foldout, ObjectField field, Label state, Button save, Button revert)
            {
                Foldout = foldout;
                Field = field;
                State = state;
                Save = save;
                Revert = revert;
            }

            /// <summary>The asset picked in this section; its working copy is what the inspector edits.</summary>
            public ScriptableObject Source => Field.value as ScriptableObject;
        }

        private static ProfileWorkingCopies Copies => ProfileWorkingCopies.instance;

        [MenuItem("Window/CargoKing/Vehicle Tuning")]
        public static void Open()
        {
            GetWindow<VehicleTuningWindow>("Vehicle Tuning");
        }

        private void OnEnable()
        {
            saveChangesMessage = "Some tuned profiles have not been saved into their assets yet.";
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

            rootVisualElement.schedule.Execute(RefreshState).Every(StateRefreshIntervalMs);
            FollowCar();
            RefreshState();
        }

        /// <summary>Unity's answer to "save" when the window or the editor is closed with unsaved copies.</summary>
        public override void SaveChanges()
        {
            Copies.SaveAll();
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            Copies.RevertAll();
            base.DiscardChanges();
        }

        private Section AddSection(VisualElement parent, string title, System.Type type, Object asset, System.Action<Object> store)
        {
            var foldout = new Foldout { text = title, value = true };

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            var field = new ObjectField($"{title} profile") { objectType = type, allowSceneObjects = false };
            field.style.flexGrow = 1;

            var state = new Label();
            state.style.color = ProfileGraphs.PeakColor;
            state.style.marginLeft = 4;
            state.style.marginRight = 4;

            var save = new Button(() =>
            {
                Copies.Save(field.value as ScriptableObject);
                RefreshState();
            }) { text = "Save", tooltip = "Write the edits into the asset." };

            var revert = new Button(() =>
            {
                Copies.Revert(field.value as ScriptableObject);
                RefreshState();
            }) { text = "Revert", tooltip = "Drop the edits and go back to the asset's values." };

            var body = new VisualElement();

            field.SetValueWithoutNotify(asset);
            field.RegisterValueChangedCallback(change =>
            {
                store(change.newValue);
                ShowInspector(body, change.newValue);
                RefreshState();
            });

            header.Add(field);
            header.Add(state);
            header.Add(save);
            header.Add(revert);
            foldout.Add(header);
            foldout.Add(body);
            parent.Add(foldout);

            ShowInspector(body, asset);
            return new Section(foldout, field, state, save, revert);
        }

        private static void ShowInspector(VisualElement body, Object asset)
        {
            body.Clear();
            if (asset is ScriptableObject profile)
            {
                body.Add(new InspectorElement(Copies.CopyFor(profile)));
            }
        }

        private void RefreshState()
        {
            // Scheduled before CreateGUI has built the sections only in theory, but cheap to guard.
            if (engine == null)
            {
                return;
            }

            foreach (Section section in new[] { engine, gearbox, tire })
            {
                bool modified = Copies.IsModified(section.Source);
                section.State.text = modified ? UnsavedMark : string.Empty;
                section.Save.SetEnabled(modified);
                section.Revert.SetEnabled(modified);
            }

            hasUnsavedChanges = Copies.AnyModified;
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
                    : "Pick the profiles to edit. Edits go to a working copy until you press Save; in Play Mode the car being driven takes the copies.";
                return;
            }

            ProfileWorkingCopies copies = Copies;
            CarEngine carEngine = car.carEngine;
            EngineProfile carEngineProfile = carEngine != null ? copies.SourceOf(carEngine.engineProfile) : null;
            GearboxProfile carGearboxProfile = carEngine != null ? copies.SourceOf(carEngine.gearboxProfile) : null;
            List<(string Label, Suspension Wheel, TireProfile Profile)> tires = VehicleLiveSampler.Wheels
                .Where(trace => trace.Wheel != null)
                .Select(trace => (trace.Label, trace.Wheel, copies.SourceOf(trace.Wheel.tireProfile)))
                .ToList();

            // The car drives with the copies, so edits count at once. Leaving Play Mode reloads the
            // scene, which puts the car's own references back.
            if (carEngine != null)
            {
                carEngine.engineProfile = copies.CopyFor(carEngineProfile);
                carEngine.gearboxProfile = copies.CopyFor(carGearboxProfile);
            }

            foreach (var (_, wheel, profile) in tires)
            {
                wheel.tireProfile = copies.CopyFor(profile);
            }

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
            status.text = $"Following {car.name}, which drives with the working copies. Edits apply on the next physics step; Save writes them into the asset."
                + (builtInDefaults ? " Some profiles are built-in defaults - create and assign assets to tune them." : string.Empty);
        }

        private static bool IsAsset(Object profile) => profile != null && AssetDatabase.Contains(profile);
    }
}
