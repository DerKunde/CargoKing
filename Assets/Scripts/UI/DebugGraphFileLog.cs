using System.Collections.Generic;
using System.IO;
using CargoKing.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CargoKing.UI
{
    /// <summary>
    /// Writes everything <see cref="DebugGraph"/> is fed to a CSV file, one row per FixedUpdate.
    ///
    /// It taps the existing graphs rather than asking anyone to log values a second time: every
    /// DebugGraph.Plot call in the project already becomes a column here, and so does any new one,
    /// with no wiring. What the overlay shows and what the file holds can therefore never drift
    /// apart.
    ///
    /// Spawns itself, so nothing has to be dragged into the scene. Editor and development builds
    /// only - a shipped build should not be writing telemetry to disk.
    /// </summary>
    public class DebugGraphFileLog : MonoBehaviour
    {
        /// <summary>Rows between flushes. At 50 Hz this is about a second of driving.</summary>
        private const int FlushInterval = 50;

        /// <summary>
        /// How many consecutive FixedUpdates DebugGraph's revision has to hold still before the
        /// column set is taken as complete. Series are created lazily on their first Plot call, so
        /// writing a header on the very first step would miss whatever is plotted further down the
        /// same frame.
        /// </summary>
        private const int RevisionSettleSteps = 3;

        private readonly List<GraphSeries> columns = new List<GraphSeries>();
        private readonly List<float> rowValues = new List<float>();

        private StreamWriter writer;
        private string currentPath;
        private int lastRevision = -1;
        private int stableRevisionSteps;
        private int rowsSinceFlush;
        private bool warnedAboutLateSeries;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            // Left visible in the hierarchy on purpose: it is the only handle on a thing that
            // spawns itself and writes to disk, so being able to find it and switch it off
            // matters more than a tidy hierarchy. No DontSave flags either - the file only gets
            // flushed and closed if OnDestroy actually runs when Play Mode ends.
            GameObject host = new GameObject(nameof(DebugGraphFileLog));
            host.AddComponent<DebugGraphFileLog>();
            DontDestroyOnLoad(host);
        }
#endif

        /// <summary>Where the logs go: alongside the project, in the folder git already ignores.</summary>
        private static string LogDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));

        private void Update()
        {
            // Edge-detected in Update rather than FixedUpdate, which can run several times or not
            // at all in a frame and would drop or double the keypress.
            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
            {
                StartNewFile();
            }
        }

        private void FixedUpdate()
        {
            if (!TryLockColumns())
            {
                return;
            }

            if (writer == null)
            {
                StartNewFile();
            }

            WriteRow();
        }

        /// <summary>
        /// True once the column set is settled. Until then nothing is written - losing the first
        /// few hundredths of a second costs nothing and keeps the file to a single header.
        /// </summary>
        private bool TryLockColumns()
        {
            if (columns.Count > 0)
            {
                if (DebugGraph.Revision != lastRevision && !warnedAboutLateSeries)
                {
                    warnedAboutLateSeries = true;
                    Debug.LogWarning(
                        $"{nameof(DebugGraphFileLog)}: new graph series appeared after the header was " +
                        "written and will not be logged. Press F9 to start a file that includes them.");
                }

                return true;
            }

            if (DebugGraph.Revision != lastRevision)
            {
                lastRevision = DebugGraph.Revision;
                stableRevisionSteps = 0;
                return false;
            }

            stableRevisionSteps++;

            if (stableRevisionSteps < RevisionSettleSteps || DebugGraph.Channels.Count == 0)
            {
                return false;
            }

            CollectColumns();
            return columns.Count > 0;
        }

        private void CollectColumns()
        {
            columns.Clear();

            foreach (DebugGraphChannel channel in DebugGraph.Channels)
            {
                foreach (GraphSeries series in channel.Series)
                {
                    columns.Add(series);
                }
            }
        }

        /// <summary>
        /// Closes the current file and opens a fresh one, so a single manoeuvre can be captured on
        /// its own instead of being buried in a whole session.
        /// </summary>
        public void StartNewFile()
        {
            CloseFile();

            if (columns.Count == 0)
            {
                CollectColumns();
            }

            if (columns.Count == 0)
            {
                return;
            }

            Directory.CreateDirectory(LogDirectory);
            currentPath = Path.Combine(LogDirectory, $"telemetry-{System.DateTime.Now:yyyyMMdd-HHmmss}.csv");

            writer = new StreamWriter(currentPath, append: false) { AutoFlush = false };
            writer.WriteLine(TelemetryCsv.FormatHeader(ColumnNames()));
            rowsSinceFlush = 0;

            Debug.Log($"{nameof(DebugGraphFileLog)}: logging {columns.Count} series to {currentPath}");
        }

        private List<string> ColumnNames()
        {
            List<string> names = new List<string> { "time" };

            foreach (DebugGraphChannel channel in DebugGraph.Channels)
            {
                foreach (GraphSeries series in channel.Series)
                {
                    // A graph holding a single same-named series reads better as just that name.
                    names.Add(channel.Name == series.Name ? series.Name : $"{channel.Name}/{series.Name}");
                }
            }

            return names;
        }

        private void WriteRow()
        {
            rowValues.Clear();
            rowValues.Add(Time.fixedTime);

            for (int i = 0; i < columns.Count; i++)
            {
                rowValues.Add(columns[i].Latest);
            }

            writer.WriteLine(TelemetryCsv.FormatRow(rowValues));

            if (++rowsSinceFlush >= FlushInterval)
            {
                writer.Flush();
                rowsSinceFlush = 0;
            }
        }

        private void OnDestroy()
        {
            CloseFile();
        }

        private void OnApplicationQuit()
        {
            CloseFile();
        }

        private void CloseFile()
        {
            if (writer == null)
            {
                return;
            }

            writer.Flush();
            writer.Dispose();
            writer = null;
        }
    }
}
