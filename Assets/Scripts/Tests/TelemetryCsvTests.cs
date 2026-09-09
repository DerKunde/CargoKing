using System.Globalization;
using System.Threading;
using CargoKing.Diagnostics;
using NUnit.Framework;

namespace CargoKing.Tests
{
    public class TelemetryCsvTests
    {
        private static void UnderCulture(string cultureName, System.Action body)
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
                body();
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void FormatValue_UnderAGermanLocale_StillWritesADecimalPoint()
        {
            // The machine this project is developed on runs a German locale, where the default
            // float formatting produces "1234,56" - which would silently split every row into an
            // extra column and make the whole file unreadable.
            UnderCulture("de-DE", () => Assert.AreEqual("1234.56", TelemetryCsv.FormatValue(1234.56f)));
        }

        [Test]
        public void FormatValue_NotANumber_IsWrittenOutRatherThanHidden()
        {
            // Physics going NaN is exactly the kind of thing this log exists to catch, so it has
            // to survive into the file instead of being turned into a zero.
            Assert.AreEqual("NaN", TelemetryCsv.FormatValue(float.NaN));
        }

        [Test]
        public void FormatRow_JoinsValuesWithCommas()
        {
            Assert.AreEqual("1,2.5,-3.25", TelemetryCsv.FormatRow(new[] { 1f, 2.5f, -3.25f }));
        }

        [Test]
        public void FormatHeader_JoinsFieldNamesWithCommas()
        {
            Assert.AreEqual("time,Engine RPM", TelemetryCsv.FormatHeader(new[] { "time", "Engine RPM" }));
        }

        [Test]
        public void FormatField_ContainingAComma_IsQuoted()
        {
            // Series names come from whatever a Plot call was given, so one of them containing a
            // comma some day is a question of when, not if.
            Assert.AreEqual("\"Torque, Nm\"", TelemetryCsv.FormatField("Torque, Nm"));
        }

        [Test]
        public void FormatField_ContainingAQuote_IsQuotedAndEscaped()
        {
            Assert.AreEqual("\"say \"\"hi\"\"\"", TelemetryCsv.FormatField("say \"hi\""));
        }

        [Test]
        public void FormatField_Plain_IsLeftAlone()
        {
            Assert.AreEqual("Engine RPM", TelemetryCsv.FormatField("Engine RPM"));
        }
    }
}
