using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CargoKing.Car.Editor
{
    /// <summary>
    /// Working copies of profile assets for the Vehicle Tuning window: one in-memory copy per asset,
    /// edited instead of the asset and, in Play Mode, driven with - so an asset changes only through
    /// <see cref="Save"/>. A ScriptableSingleton, so the copies outlive the domain reload of entering
    /// Play Mode; they are gone when Unity closes.
    /// </summary>
    internal sealed class ProfileWorkingCopies : ScriptableSingleton<ProfileWorkingCopies>
    {
        [Serializable]
        private sealed class Entry
        {
            public ScriptableObject source;
            public ScriptableObject copy;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        /// <summary>
        /// The working copy of <paramref name="profile"/>, created on first use. Anything that is not
        /// an asset - a built-in default, or already a copy - comes back as it is.
        /// </summary>
        public T CopyFor<T>(T profile) where T : ScriptableObject
        {
            if (profile == null || !AssetDatabase.Contains(profile))
            {
                return profile;
            }

            Entry entry = Find(profile);
            if (entry == null)
            {
                T copy = Instantiate(profile);
                copy.name = profile.name;
                // DontSave also keeps it from being unloaded as an unused asset on entering Play Mode.
                copy.hideFlags = HideFlags.DontSave;
                entry = new Entry { source = profile, copy = copy };
                entries.Add(entry);
            }

            return (T)entry.copy;
        }

        /// <summary>The working copy of <paramref name="profile"/> if there is one, otherwise the profile itself.</summary>
        public T CopyIfAny<T>(T profile) where T : ScriptableObject
        {
            Entry entry = Find(profile);
            return entry != null ? (T)entry.copy : profile;
        }

        /// <summary>The asset behind a working copy; anything else comes back as it is.</summary>
        public T SourceOf<T>(T profile) where T : ScriptableObject
        {
            Entry entry = profile != null ? entries.Find(e => e.copy == profile && e.source != null) : null;
            return entry != null ? (T)entry.source : profile;
        }

        /// <summary>Whether the copy of <paramref name="source"/> holds values the asset does not.</summary>
        public bool IsModified(ScriptableObject source)
        {
            Entry entry = Find(source);
            return entry != null && Differs(entry);
        }

        public bool AnyModified => entries.Exists(entry => entry.source != null && entry.copy != null && Differs(entry));

        /// <summary>Writes the copy into the asset and saves it; undoable.</summary>
        public void Save(ScriptableObject source)
        {
            Entry entry = Find(source);
            if (entry == null)
            {
                return;
            }

            Undo.RecordObject(entry.source, $"Save {entry.source.name}");
            Overwrite(entry.source, entry.copy);
            EditorUtility.SetDirty(entry.source);
            AssetDatabase.SaveAssetIfDirty(entry.source);
        }

        /// <summary>Takes the copy back to the asset's values; undoable.</summary>
        public void Revert(ScriptableObject source)
        {
            Entry entry = Find(source);
            if (entry == null)
            {
                return;
            }

            Undo.RecordObject(entry.copy, $"Revert {entry.source.name}");
            Overwrite(entry.copy, entry.source);
        }

        public void SaveAll()
        {
            foreach (Entry entry in entries)
            {
                if (entry.source != null && entry.copy != null && Differs(entry))
                {
                    Save(entry.source);
                }
            }
        }

        public void RevertAll()
        {
            foreach (Entry entry in entries)
            {
                if (entry.source != null && entry.copy != null && Differs(entry))
                {
                    Revert(entry.source);
                }
            }
        }

        private Entry Find(ScriptableObject source)
        {
            return source != null ? entries.Find(entry => entry.source == source && entry.copy != null) : null;
        }

        // JsonUtility writes the profile's own fields only - not the name or hide flags, which the
        // copy does not share with the asset.
        private static bool Differs(Entry entry)
        {
            return JsonUtility.ToJson(entry.copy) != JsonUtility.ToJson(entry.source);
        }

        private static void Overwrite(ScriptableObject target, ScriptableObject from)
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(from), target);

            // The peaks are derived, not serialized, and OnValidate does not run for a scripted overwrite.
            if (target is TireProfile tire)
            {
                tire.RecalculatePeaks();
            }
        }
    }
}
