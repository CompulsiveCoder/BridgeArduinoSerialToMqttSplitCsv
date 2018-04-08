using System;
using duinocom;
using System.Threading;
using System.Text;
using System.Configuration;
using System.IO;
using System.IO.Ports;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Diagnostics;

namespace BridgeArduinoSerialToMqttSplitCsv
{
	class MainClass
	{
		public static bool IsVerbose;

		public static bool IsSubscribed = false;

		public static SerialClient Client = null;

		public static void Main (string[] args)
		{
			var arguments = new Arguments (args);

			Run (arguments);
		}

		public static void Run(Arguments arguments)
		{
			IsVerbose = arguments.Contains ("v");
			var userId = GetConfigValue (arguments, "UserId");
			var pass = GetConfigValue (arguments, "Password");
			var host = GetConfigValue (arguments, "Host");
			var deviceName = GetConfigValue (arguments, "DeviceName");
			var serialPortName = GetConfigValue (arguments, "SerialPort");
			var serialBaudRate = Convert.ToInt32 (GetConfigValue (arguments, "SerialBaudRate"));
			var topicPrefix = "/" + userId;
			var useTopicPrefix = Convert.ToBoolean(ConfigurationSettings.AppSettings["UseTopicPrefix"]);

			SerialPort port = null;

			if (String.IsNullOrEmpty (serialPortName)) {
				Console.WriteLine ("Serial port not specified. Detecting.");
				var detector = new SerialPortDetector ();
				port = detector.Detect ();
				serialPortName = port.PortName;
			} else {
				Console.WriteLine ("Serial port specified");
				port = new SerialPort (serialPortName, serialBaudRate);
			}

			Console.WriteLine ("Device name: " + GetConfigValue (arguments, "DeviceName"));
			Console.WriteLine ("Port name: " + serialPortName);

			var deviceTopic = "/" + deviceName;

			if (useTopicPrefix)
				deviceTopic = topicPrefix + deviceTopic;

			Console.WriteLine (deviceTopic + "/[Key]");

			if (port == null) {
				Console.WriteLine ("Error: Device port not found.");
			} else {
				Console.WriteLine ("Serial port: " + port.PortName);

				Client = new SerialClient (port);

				try {

					if (!Client.Port.IsOpen)
						Client.Open ();	
					
					//Thread.Sleep(100);
					var output = Client.Read ();
					Console.WriteLine(output);
					//Thread.Sleep(100);
					//Client.Close();

					var isRunning = true;

					var mqttClient = new MqttClient(host);

					var clientId = Guid.NewGuid ().ToString ();

					mqttClient.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
					mqttClient.Connect (clientId, userId, pass);

					var subscribeTopics = GetSubscribeTopics(arguments);
					foreach (var topic in subscribeTopics)
					{
						mqttClient.Subscribe(new string[] {topic}, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
					}
					
					var assembly = System.Reflection.Assembly.GetExecutingAssembly();
					var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
					var version = fvi.FileVersion;

					mqttClient.Publish ("/" + deviceName + "/bridge/version", Encoding.UTF8.GetBytes (version),
	                	MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, // QoS level
	                	true);

					while (isRunning) {

						if (!Client.Port.IsOpen)
							Client.Open ();	
						
						//Thread.Sleep(100);
						output = "";
						while (!output.Contains(";;"))
						{	
							var value = Client.Read ();
							if (!String.IsNullOrEmpty(value))
								output += value;
							Thread.Sleep(10);
						}

						//Thread.Sleep(100);
						//Client.Close();

						//Console.WriteLine("----- Serial output");
						//Console.WriteLine(output);
						//Console.WriteLine("-----");

						var topics = new List<string>();

						Publish (arguments, mqttClient, output, topics);

						//Thread.Sleep(10);

					}

				} catch (Exception ex) {
					Console.WriteLine ("Connection lost with: " + serialPortName);
					Console.WriteLine(ex.ToString());
					Console.WriteLine ();
					Console.WriteLine ("Waiting for 10 seconds then retrying");

					Thread.Sleep (10000);
					Run (arguments);
				}
			}
		}

		public static void Publish(Arguments arguments, MqttClient client, string output, List<string> topics)
		{
			var incomingLinePrefix = ConfigurationSettings.AppSettings["IncomingLinePrefix"];

			var data = GetLastDataLine (output);

			if (!String.IsNullOrEmpty(data)) {
				//if (IsVerbose)
				Console.WriteLine("----- Data");
				Console.WriteLine (data);
				Console.WriteLine ("-----");
				//else
				//	Console.WriteLine (".");

				var dividerCharacter = ConfigurationSettings.AppSettings["DividerSplitCharacter"].ToCharArray()[0];
				var equalsCharacter = ConfigurationSettings.AppSettings["EqualsSplitCharacter"].ToCharArray()[0];
				var userId = GetConfigValue (arguments, "UserId");
				var deviceName = GetConfigValue (arguments, "DeviceName");
				var topicPrefix = "/" + userId;
				var useTopicPrefix = Convert.ToBoolean(ConfigurationSettings.AppSettings["UseTopicPrefix"]);

				var deviceTopic = "/" + deviceName;

				if (useTopicPrefix)
					deviceTopic = topicPrefix + deviceTopic;

				foreach (var item in data.Split(dividerCharacter)) {
					var parts = item.Split (equalsCharacter);
					if (parts.Length == 2) {
						var key = parts [0];
						var value = parts [1];

						if (!String.IsNullOrEmpty (value)) {
							var fullTopic = deviceTopic + "/" + key;

							if (IsVerbose)
								Console.WriteLine (fullTopic + ":" + value);

							if (!topics.Contains (fullTopic))
								topics.Add (fullTopic);

							client.Publish (fullTopic, Encoding.UTF8.GetBytes (value),
			                	MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, // QoS level
			                	true);
						}
					}
				}

				var timeTopic = deviceTopic + "/Time";

				var time = DateTime.Now.ToString ("MM/dd/yyyy HH:mm:ss");

				if (IsVerbose)
					Console.WriteLine (timeTopic + ":" + time);

				client.Publish (timeTopic, Encoding.UTF8.GetBytes (time),
                	MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, // QoS level
                	true);

				var pushNotificationTopic = "/push/" + deviceName;

				if (IsVerbose)
					Console.WriteLine (pushNotificationTopic + ":Updated");

				client.Publish (pushNotificationTopic, Encoding.UTF8.GetBytes ("Updated"));
			}
		}

		// this code runs when a message was received
		public static void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
		{
			var topic = e.Topic;
			var topicSections = topic.Split ('/');
			var subTopic = topicSections [topicSections.Length - 2];

			if (IsVerbose)
				Console.WriteLine ("Subtopic: " + subTopic);

			var message = System.Text.Encoding.Default.GetString(e.Message);

			if (IsVerbose)
				Console.WriteLine("Message received: " + message);

			Console.WriteLine(subTopic + message);
			
			SendMessageToDevice(subTopic + message);
		}
		
		public static void SendMessageToDevice(string message)
		{
			try
			{
				if (!Client.Port.IsOpen)
					Client.Open ();	
				
				Client.WriteLine (message);

				//Client.Close();
			}
			catch (Exception ex)
			{
				Console.WriteLine ("Failed to send message to device");
				Console.WriteLine(ex.ToString());
				Console.WriteLine ();
				Console.WriteLine ("Waiting for 10 seconds then retrying");

				Thread.Sleep (10000);

				SendMessageToDevice (message);
			}
		}

		public static string GetConfigValue(Arguments arguments, string argumentKey)
		{
			var value = String.Empty;

			if (IsVerbose)
				Console.WriteLine ("Getting config/argument value for: " + argumentKey);

			if (arguments.Contains (argumentKey)) {
				value = arguments [argumentKey];
				if (IsVerbose)
					Console.WriteLine ("Found in arguments");
			} else {

				try{
					value = ConfigurationManager.AppSettings [argumentKey];
				}
				catch (Exception ex) {
					Console.WriteLine("Failed to get configuration value: " + argumentKey);
					throw ex;
				}

				if (IsVerbose)
					Console.WriteLine ("Looking in config");
			}

			return value;
		}

		public static string[] GetSubscribeTopics(Arguments arguments)
		{
			var topics = GetConfigValue (arguments, "SubscribeTopics").Split (',');
			var userId = GetConfigValue (arguments, "UserId");
			var deviceName = GetConfigValue (arguments, "DeviceName");
			var topicPrefix = "/" + userId;
			var useTopicPrefix = Convert.ToBoolean(ConfigurationSettings.AppSettings["UseTopicPrefix"]);


			var list = new List<String> ();

			foreach (var topic in topics) {
				var fullTopic = "/" + deviceName + "/" + topic + "/in";

				if (useTopicPrefix)
					fullTopic = topicPrefix + fullTopic;

				list.Add (fullTopic);
			}

			return list.ToArray ();
		}
		
		public static Dictionary<string, int> ParseOutputLine(string outputLine)
		{
			var dictionary = new Dictionary<string, int> ();

			if (IsValidOutputLine (outputLine)) {
				foreach (var pair in outputLine.Split(';')) {
					var parts = pair.Split (':');

					if (parts.Length == 2) {
						var key = parts [0];
						var value = 0;
						try {
							value = Convert.ToInt32 (parts [1]);

							dictionary [key] = value;
						} catch {
							Console.WriteLine ("Warning: Invalid key/value pair '" + pair + "'");
						}
					}
				}
			}

			return dictionary;
		}

		public static string GetLastDataLine(string output)
		{
			var lines = output.Split ('\n');

			for (int i = lines.Length - 1; i >= 0; i--) {
				var line = lines [i].Trim();
				if (IsValidOutputLine(line))
					return line;
			}

			return String.Empty;
		}

		public static bool IsValidOutputLine(string outputLine)
		{
			var dataPrefix = "D;";

			var dataPostFix = ";;";

			return outputLine.StartsWith(dataPrefix)
				&& outputLine.EndsWith(dataPostFix);
		}

	}
}
