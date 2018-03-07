﻿using System;
using duinocom;
using System.Threading;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Text;
using System.Configuration;
using System.IO;
using System.IO.Ports;
using System.Collections.Generic;

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

			IsVerbose = arguments.Contains ("v");
			var userId = GetConfigValue (arguments, "UserId");
			var pass = GetConfigValue (arguments, "Password");
			var host = GetConfigValue (arguments, "Host");
			var serialPortName = GetConfigValue (arguments, "SerialPort");
			var serialBaudRate = Convert.ToInt32 (GetConfigValue (arguments, "SerialBaudRate"));
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

			if (port == null) {
				Console.WriteLine ("Error: Device port not found.");
			} else {
				Console.WriteLine ("Serial port: " + port.PortName);

				Client = new SerialClient (port);

				//communicator.ReallyShortPause = 50;
				try {
					Client.Open ();

					var isRunning = true;

					var mqttClient = new MqttClient (host);

					var clientId = Guid.NewGuid ().ToString ();

					mqttClient.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
					mqttClient.Connect (clientId, userId, pass);

					var subscribeTopics = GetSubscribeTopics(arguments);
					foreach (var topic in subscribeTopics)
					{
						mqttClient.Subscribe(new string[] {topic}, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
					}


					while (isRunning) {
						var output = Client.Read ();

						Thread.Sleep (300);

						var topics = new List<string>();

						Publish (arguments, mqttClient, output, topics);
					
						//Thread.Sleep (1);
					}

					Client.Close ();
				
				} catch (IOException ex) {
					Console.WriteLine ("Error: Please ensure device is connected to port '" + serialPortName + "' and not already in use.\n\n" + ex.Message);
				}
			}
		}

		public static void Publish(Arguments arguments, MqttClient client, string data, List<string> topics)
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

						if (!String.IsNullOrEmpty (value)) {
							var fullTopic = "/" + deviceName + "/" + key;

							if (useTopicPrefix)
								fullTopic = topicPrefix + fullTopic;
					
							Console.WriteLine (fullTopic + ":" + value);

							if (!topics.Contains (fullTopic))
								topics.Add (fullTopic);

							client.Publish (fullTopic, Encoding.UTF8.GetBytes (value));
						}
					}
				}
			}
		}

		public static void Subscribe(Arguments arguments, MqttClient client, List<string> topics)
		{
			client.Subscribe(topics.ToArray(), new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
				
		/*	ushort msgId = client.Subscribe(topics.ToArray(),
			new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,            
				MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });*/
		}

		// this code runs when a message was received
		public static void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
		{
			var topic = e.Topic;
			var topicSections = topic.Split ('/');
			var subTopic = topicSections [topicSections.Length - 2];

			Console.WriteLine ("Subtopic: " + subTopic);

			var message = System.Text.Encoding.Default.GetString(e.Message);
			System.Console.WriteLine("Message received: " + message);

			Client.WriteLine (subTopic + message);
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
				value = ConfigurationSettings.AppSettings [argumentKey];
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
	}
}
