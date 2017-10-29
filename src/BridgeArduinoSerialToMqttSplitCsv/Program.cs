using System;
using duinocom;
using System.Threading;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Text;
using System.Configuration;

namespace CSharpReadArduinoSerial
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			var userId = ConfigurationSettings.AppSettings["UserId"];
			var pass = ConfigurationSettings.AppSettings["Password"];
			var host = ConfigurationSettings.AppSettings["Host"];
			var serialBaudRate = Convert.ToInt32(ConfigurationSettings.AppSettings["SerialBaudRate"]);

			var detector = new DuinoPortDetector ();
			var port = detector.Guess ();
			//Console.WriteLine (port.PortName);
			int i = 0;

			using (var communicator = new DuinoCommunicator (port.PortName, serialBaudRate))
			{
				//communicator.ReallyShortPause = 50;
				communicator.Open ();

				var isRunning = true;

				var client = new MqttClient (host);

				var clientId = Guid.NewGuid ().ToString ();

				client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
				client.Connect (clientId, userId, pass);

				while (isRunning) {
					var output = communicator.Read ();

					//Thread.Sleep (1);

					Publish (client, output);
					
					//Thread.Sleep (1);
				}

				communicator.Close ();
			}
		}

		public static void Publish(MqttClient client, string data)
		{
			var incomingLinePrefix = ConfigurationSettings.AppSettings["IncomingLinePrefix"];

			var isValidDataLine = !String.IsNullOrEmpty (data.Trim ())
			                      && data.StartsWith (incomingLinePrefix);

			if (isValidDataLine) {
				Console.WriteLine (data);

				var dividerCharacter = ConfigurationSettings.AppSettings["DividerSplitCharacter"].ToCharArray()[0];
				var equalsCharacter = ConfigurationSettings.AppSettings["EqualsSplitCharacter"].ToCharArray()[0];
				var userId = ConfigurationSettings.AppSettings["UserId"];
				var deviceName = ConfigurationSettings.AppSettings["DeviceName"];
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
					
						Console.WriteLine (fullTopic);

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
	}
}
