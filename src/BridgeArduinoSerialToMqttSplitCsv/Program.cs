using System;
using duinocom;
using System.Threading;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Text;
using System.Configuration;
using System.IO;
using System.IO.Ports;

namespace BridgeArduinoSerialToMqttSplitCsv
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			var arguments = new Arguments (args);

			var userId = GetConfigValue (arguments, "UserId");
			var pass = GetConfigValue (arguments, "Password");
			var host = GetConfigValue (arguments, "Host");
			var serialPortName = GetConfigValue (arguments, "SerialPort");
			var serialBaudRate = Convert.ToInt32(GetConfigValue (arguments, "SerialBaudRate"));
			SerialPort port = null;

			if (String.IsNullOrEmpty (serialPortName)) {
				var detector = new SerialPortDetector ();
				port = detector.Detect ();
				serialPortName = port.PortName;
			}

			Console.WriteLine (port.PortName);

			var serialClient = new SerialClient (port);

				//communicator.ReallyShortPause = 50;
			try{
				serialClient.Open ();

				var isRunning = true;

				var mqttClient = new MqttClient (host);

				var clientId = Guid.NewGuid ().ToString ();

				mqttClient.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
				mqttClient.Connect (clientId, userId, pass);

				while (isRunning) {
					var output = serialClient.Read ();

					Thread.Sleep (300);

					Publish (arguments, mqttClient, output);
					
					//Thread.Sleep (1);
				}

				serialClient.Close ();
			}
			catch (IOException ex) {
				Console.WriteLine ("Error: Please ensure device is connected to port '" + serialPortName + "' and not already in use.\n\n" + ex.Message);
			}
		}

		public static void Publish(Arguments arguments, MqttClient client, string data)
		{
			var incomingLinePrefix = ConfigurationSettings.AppSettings["IncomingLinePrefix"];

			var isValidDataLine = !String.IsNullOrEmpty (data.Trim ())
			                      && data.StartsWith (incomingLinePrefix);

			if (isValidDataLine) {
				Console.WriteLine (data);

				var dividerCharacter = ConfigurationSettings.AppSettings["DividerSplitCharacter"].ToCharArray()[0];
				var equalsCharacter = ConfigurationSettings.AppSettings["EqualsSplitCharacter"].ToCharArray()[0];
				var userId = GetConfigValue (arguments, "UserId");
				var deviceName = GetConfigValue (arguments, "DeviceName");
				var topicPrefix = "/" + userId;
				var useTopicPrefix = Convert.ToBoolean(ConfigurationSettings.AppSettings["UseTopicPrefix"]);

				foreach (var item in data.Split(dividerCharacter)) {
					var parts = item.Split (equalsCharacter);
					if (parts.Length == 2) {
						var key = parts [0];
						var value = parts [1];

						var fullTopic = "/" + deviceName + "/" + key;

						if (useTopicPrefix)
							fullTopic = topicPrefix + fullTopic;
					
						Console.WriteLine(fullTopic + ":" + value);

						client.Publish (fullTopic, Encoding.UTF8.GetBytes (value));
					}
				}
			}
		}

		// this code runs when a message was received
		public static void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
		{
			string ReceivedMessage = Encoding.UTF8.GetString(e.Message);

			//Console.WriteLine (ReceivedMessage);
		}

		public static string GetConfigValue(Arguments arguments, string argumentKey)
		{
			var value = String.Empty;

			if (arguments.Contains (argumentKey))
				value = arguments [argumentKey];
			else
				value = ConfigurationSettings.AppSettings[argumentKey];

			return value;
		}
	}
}
