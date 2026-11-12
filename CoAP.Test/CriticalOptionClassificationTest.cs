using System;
using NUnit.Framework;

namespace CoAP.Protocol
{
    [TestFixture]
    public sealed class CriticalOptionClassificationTest
    {
        [TestCase(1)]
        [TestCase(11)]
        [TestCase(19)]
        [TestCase(31)]
        [TestCase(205)]
        [TestCase(65001)]
        public void OddOptionNumbersAreCriticalAndNotElective(Int32 number)
        {
            Assert.That(Option.IsCritical((OptionType)number), Is.True);
            Assert.That(Option.IsElective((OptionType)number), Is.False);
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(12)]
        [TestCase(28)]
        [TestCase(204)]
        [TestCase(65000)]
        public void EvenOptionNumbersAreElectiveAndNotCritical(Int32 number)
        {
            Assert.That(Option.IsCritical((OptionType)number), Is.False);
            Assert.That(Option.IsElective((OptionType)number), Is.True);
        }
    }
}
