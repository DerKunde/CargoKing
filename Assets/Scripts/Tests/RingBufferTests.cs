using System;
using CargoKing.Diagnostics;
using NUnit.Framework;

namespace CargoKing.Tests
{
    public class RingBufferTests
    {
        [Test]
        public void BelowCapacity_KeepsEverythingOldestFirst()
        {
            var buffer = new RingBuffer<int>(4);
            buffer.Add(1);
            buffer.Add(2);
            buffer.Add(3);

            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(1, buffer[0]);
            Assert.AreEqual(3, buffer[2]);
        }

        [Test]
        public void PastCapacity_DropsTheOldest()
        {
            var buffer = new RingBuffer<int>(3);
            for (int i = 1; i <= 5; i++)
            {
                buffer.Add(i);
            }

            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(3, buffer[0]);
            Assert.AreEqual(4, buffer[1]);
            Assert.AreEqual(5, buffer[2]);
        }

        [Test]
        public void Clear_Empties()
        {
            var buffer = new RingBuffer<int>(3);
            buffer.Add(1);
            buffer.Add(2);
            buffer.Clear();
            buffer.Add(7);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(7, buffer[0]);
        }

        [Test]
        public void Index_OutsideCount_Throws()
        {
            var buffer = new RingBuffer<int>(3);
            buffer.Add(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => { int _ = buffer[1]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { int _ = buffer[-1]; });
        }

        [Test]
        public void Capacity_BelowOne_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(0));
        }
    }
}
