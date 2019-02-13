using System;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace BridgeArduinoSerialToMqttSplitCsv.Tests.Integration
{
	public class BaseTestFixture
	{
		public BaseTestFixture()
		{
		}

		[SetUp]
		public void Initialize()
		{
			Console.WriteLine("");
			Console.WriteLine("====================");
			Console.WriteLine("Preparing test");
		}

		[TearDown]
		public virtual void Finish()
		{
			HandleFailureFile();

			Console.WriteLine("Finished test");
			Console.WriteLine("====================");
			Console.WriteLine("");
		}

		public void HandleFailureFile()
		{
			var failuresDir = Path.GetFullPath("../../failures");

			var fixtureName = TestContext.CurrentContext.Test.FullName;

			var failureFile = Path.Combine(failuresDir, fixtureName + ".txt");

			if (TestContext.CurrentContext.Result.State == TestState.Error
			  || TestContext.CurrentContext.Result.State == TestState.Failure)
			{
				Console.WriteLine("Test failed.");

				Console.WriteLine(failuresDir);
				Console.WriteLine(fixtureName);
				Console.WriteLine(failureFile);

				if (!Directory.Exists(failuresDir))
					Directory.CreateDirectory(failuresDir);

				File.WriteAllText(failureFile, fixtureName);
			}
			else
			{
				Console.WriteLine("Test passed.");
				if (File.Exists(failureFile))
					File.Delete(failureFile);
			}
		}

		public string GetDevicePort()
		{
			var devicePort = Environment.GetEnvironmentVariable("MQTT_BRIDGE_EXAMPLE_DEVICE_PORT");

			if (String.IsNullOrEmpty(devicePort))
				devicePort = "/dev/ttyUSB0";

			Console.WriteLine("Device port: " + devicePort);

			return devicePort;
		}

		public int GetDeviceSerialBaudRate()
		{
			return 9600;
		}
	}
}
