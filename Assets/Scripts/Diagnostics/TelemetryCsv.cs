using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CargoKing.Diagnostics
{
    /// <summary>
    /// CSV formatting for the telemetry log, kept free of any Unity dependency so it can run
    /// under a plain EditMode test.
    /// </summary>
    public static class TelemetryCsv
    {
        /// <summary>
        /// Four decimals is enough to see a torque or an RPM settle without turning the file into
        /// noise, and short enough that a few minutes of 50 Hz logging stays a manageable size.
        /// </summary>
        private const string ValueFormat = "0.####";

        /// <summary>
        /// One value, always with a decimal POINT. Deliberately pinned to the invariant culture
        /// rather than left to the machine's: on a German locale the default formatting yields
        /// "1234,56", which would split the row into an extra column and quietly corrupt every
        /// line of the file.
        /// </summary>
        public static string FormatValue(float value)
        {
            return value.ToString(ValueFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// One text field, quoted only when it has to be. Series names come from whatever string
        /// a Plot call was given, so one of them carrying a comma is a matter of time.
        /// </summary>
        public static string FormatField(string field)
        {
            if (field == null)
            {
                return string.Empty;
            }

            bool needsQuoting = field.IndexOf(',') >= 0
                || field.IndexOf('"') >= 0
                || field.IndexOf('\n') >= 0
                || field.IndexOf('\r') >= 0;

            if (!needsQuoting)
            {
                return field;
            }

            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        public static string FormatRow(IReadOnlyList<float> values)
        {
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(FormatValue(values[i]));
            }

            return builder.ToString();
        }

        public static string FormatHeader(IReadOnlyList<string> fields)
        {
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(FormatField(fields[i]));
            }

            return builder.ToString();
        }
    }
}
