using System;
using NUnit.Framework;

namespace BridgeArduinoSerialToMqttSplitCsv.Tests.Unit
{
    [TestFixture (Category = "Unit")]
    public class ProgramParseOutputLineTestFixture
    {
        public ProgramParseOutputLineTestFixture ()
        {

        }

        [Test]
        public void Test_ParseOutputLine ()
        {
            var dateString = DateTime.Now.ToString ();

            var outputLine = "D;A:1;B:2;C:" + dateString + ";;";

            var data = MainClass.ParseOutputLine (outputLine);

            Assert.AreEqual ("1", data ["A"], "Invalid data on key A.");
            Assert.AreEqual ("2", data ["B"], "Invalid data on key B.");
            Assert.AreEqual (dateString, data ["C"], "Invalid data on key C.");
        }
    }
}

