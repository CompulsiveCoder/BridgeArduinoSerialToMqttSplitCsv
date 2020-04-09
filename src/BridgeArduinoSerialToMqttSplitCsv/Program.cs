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
using System.Net.Mail;
using System.Net;

namespace BridgeArduinoSerialToMqttSplitCsv
{
  public class MainClass
  {
    public static bool IsVerbose;
    public static bool IsSubscribed = false;
    public static SerialClient Client = null;
    public static string IncomingKeyValueSeparator = "";
    public static int WaitTimeBeforeRetry = 3;
    public static string SelfHostName = String.Empty;
    public static MqttClient MqttClient = null;

    public static string DeviceName = "";

    public static string MqttHost = "";
    public static string MqttUserId = "";
    public static string MqttPassword = "";
    public static int MqttPort = 1883;
    public static string[] MqttSubscribeTopics = new string[]{ };

    public static bool IsMqttConnected {
      get { return MqttClient != null && MqttClient.IsConnected; }
    }

    public static void Main (string[] args)
    {
      Console.Title = "MQTTBridge";
    
      var arguments = new Arguments (args);

      Run (arguments);
    }

    public static void Run (Arguments arguments)
    {
      SelfHostName = GetSelfHostName ();

      IsVerbose = arguments.Contains ("v");
      MqttUserId = GetConfigValue (arguments, "UserId");
      MqttPassword = GetConfigValue (arguments, "Password");
      MqttHost = GetConfigValue (arguments, "Host");
      MqttPort = Convert.ToInt32 (GetConfigValue (arguments, "MqttPort"));
      DeviceName = GetConfigValue (arguments, "DeviceName");

      MqttSubscribeTopics = GetSubscribeTopics (arguments);

      var serialPortName = GetConfigValue (arguments, "SerialPort");
      var serialBaudRate = Convert.ToInt32 (GetConfigValue (arguments, "SerialBaudRate"));
      var topicPrefix = MqttUserId;
      var useTopicPrefix = Convert.ToBoolean (ConfigurationSettings.AppSettings ["UseTopicPrefix"]);
      IncomingKeyValueSeparator = GetConfigValue (arguments, "IncomingKeyValueSeparator");

      var waitTimeBeforeRetryString = GetConfigValue (arguments, "WaitTimeBeforeRetry");
      if (!String.IsNullOrEmpty (waitTimeBeforeRetryString))
        Int32.TryParse (waitTimeBeforeRetryString, out WaitTimeBeforeRetry);

      var emailAddress = GetConfigValue (arguments, "EmailAddress");
      var smtpServer = GetConfigValue (arguments, "SmtpServer");
      var smtpUsername = GetConfigValue (arguments, "SmtpUsername");
      var smtpPassword = GetConfigValue (arguments, "SmtpPassword");
      var smtpPort = Convert.ToInt32 (GetConfigValue (arguments, "SmtpPort"));

      if (MqttPort == 0)
        MqttPort = 1883;

      Console.WriteLine ("Host: " + MqttHost);
      Console.WriteLine ("UserId: " + MqttUserId);
      Console.WriteLine ("Port: " + MqttPort);
      Console.WriteLine ("Wait time before retry: " + WaitTimeBeforeRetry + " seconds");
      //Console.WriteLine ("Host: " + host);

      SerialPort port = GetDevicePort (serialPortName, serialBaudRate);

      Console.WriteLine ("Device name: " + GetConfigValue (arguments, "DeviceName"));
      Console.WriteLine ("Serial port name: " + serialPortName);

      var deviceTopic = DeviceName;

      if (useTopicPrefix)
        deviceTopic = topicPrefix + deviceTopic;

      Console.WriteLine (deviceTopic + "/[Key]");

      if (port == null) {
        Console.WriteLine ("Error: Device port not found.");
      } else {
        Console.WriteLine ("Serial port: " + port.PortName);

        Client = new SerialClient (port);

        try {
          // TODO: Remove if not needed
          //var output = Client.ReadLine ();
          //Console.WriteLine (output);
          //Thread.Sleep(100);
          //Client.Close();

          var isRunning = true;

          SetupMQTT (MqttHost, MqttUserId, MqttPassword, MqttPort, DeviceName, MqttSubscribeTopics);

          while (isRunning) {
            if (!Client.Port.IsOpen) {
              Client.Open ();
              Console.WriteLine ("Opened serial port");

              Thread.Sleep (2000);
            }

            if (!MqttClient.IsConnected) {
              Console.WriteLine ("MQTT is not connected. Reconnecting...");
              SetupMQTT (MqttHost, MqttUserId, MqttPassword, MqttPort, DeviceName, MqttSubscribeTopics);
            }

            while (Client.Port.BytesToRead > 0) {
              var value = Client.ReadLine ().Trim ();
              if (!String.IsNullOrEmpty (value)) {
                Console.WriteLine ("> " + value.Trim ());
                if (value.Contains ("D;") && value.Contains (";;")) {
                  var topics = new List<string> ();

                  Publish (arguments, value, topics);
                }
              }
              Thread.Sleep (10);
            }


            Thread.Sleep (1000);
          }

        } catch (Exception ex) {
          Console.WriteLine ("Connection lost with: " + serialPortName);
          Console.WriteLine (ex.ToString ());
          Console.WriteLine ();
          Console.WriteLine ("Waiting for " + WaitTimeBeforeRetry + " seconds then retrying");

          SendErrorEmail (ex, DeviceName, serialPortName, smtpServer, emailAddress, smtpUsername, smtpPassword, smtpPort);

          Thread.Sleep (WaitTimeBeforeRetry * 1000);
        }
      }
    }

    public static void SetupMQTT (string mqttHost, string mqttUsername, string mqttPassword, int mqttPort, string deviceName, string[] subscribeTopics)
    {
      while (!IsMqttConnected) {
        try {
          MqttClient = new MqttClient (mqttHost, mqttPort, false, null, null, MqttSslProtocols.None);

          var clientId = Guid.NewGuid ().ToString ();

          MqttClient.MqttMsgPublishReceived += MqttClient_MqttMsgPublishReceived;
          MqttClient.ConnectionClosed += MqttClient_ConnectionClosed;
          MqttClient.Connect (clientId, mqttUsername, mqttPassword);

          foreach (var topic in subscribeTopics) {
            MqttClient.Subscribe (new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
          }

          var assembly = System.Reflection.Assembly.GetExecutingAssembly ();
          var fvi = FileVersionInfo.GetVersionInfo (assembly.Location);
          var version = fvi.FileVersion;

          MqttClient.Publish (deviceName + "/bridge/version", Encoding.UTF8.GetBytes (version),
            MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, // QoS level
            true);

          Console.WriteLine ("Connected to MQTT broker");
        } catch (Exception ex) {
          Console.WriteLine ("Failed to connect.");
          Console.WriteLine (ex.ToString ());

          Console.WriteLine ("Waiting for " + WaitTimeBeforeRetry + " seconds before retrying...");

          Thread.Sleep (WaitTimeBeforeRetry * 1000);
        }
      }
    }

    static void MqttClient_ConnectionClosed (object sender, EventArgs e)
    {
      Console.WriteLine ("MQTT connection closed. Reconnecting...");
      SetupMQTT (MqttHost, MqttUserId, MqttPassword, MqttPort, DeviceName, MqttSubscribeTopics);
    }

    public static SerialPort GetDevicePort (string serialPortName, int serialBaudRate)
    {
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

      return port;
    }

    public static void SendErrorEmail (Exception error, string deviceName, string serialPort, string smtpServer, string emailAddress, string smtpUsername, string smtpPassword, int smtpPort)
    {
      var areDetailsProvided = (smtpServer != "mail.example.com" &&
                               emailAddress != "user@example.com" &&
                               smtpServer.ToLower () != "na" &&
                               emailAddress.ToLower () != "na" &&
                               !String.IsNullOrWhiteSpace (smtpServer) &&
                               !String.IsNullOrWhiteSpace (emailAddress));

      if (areDetailsProvided) {
        try {
          var notes = String.Empty;
          if (error.Message == "Input/output error")
            notes = "The device was likely disconnected. If it was intentionally disconnected then you can ignore this error. If it wasn't intentionally disconnected then it may be malfunctioning.\n\n";

          var subject = "Error: MQTT bridge for device '" + deviceName + "' on '" + SelfHostName + "'";
          var body = "The following error was thrown by the MQTT bridge utility...\n\nSource host: " + SelfHostName + "\n\nDevice name: " + deviceName + "\n\nPort name:" + serialPort + "\n\n" + notes + error.ToString () + "\n\n\n\n--------------------\n\nEmail sent by MQTT bridge.";

          var mail = new MailMessage (emailAddress, emailAddress, subject, body);

          var smtpClient = new SmtpClient (smtpServer, smtpPort);

          var credentialsAreProvided = (smtpUsername != "user" &&
                                       smtpPassword != "pass" &&
                                       smtpUsername != "na" &&
                                       smtpPassword != "na" &&
                                       !String.IsNullOrWhiteSpace (smtpUsername) &&
                                       !String.IsNullOrWhiteSpace (smtpPassword));

          if (credentialsAreProvided)
            smtpClient.Credentials = new NetworkCredential (smtpUsername, smtpPassword);

          smtpClient.Send (mail);

        } catch (Exception ex) {
          Console.WriteLine ("");
          Console.WriteLine ("An error occurred while sending error report...");
          Console.WriteLine ("SMTP Server: " + smtpServer);
          Console.WriteLine ("Email Address: " + emailAddress);
          Console.WriteLine ("SMTP Username: " + smtpUsername);
          Console.WriteLine ("SMTP Password: [hidden]");
          Console.WriteLine ("SMTP Port: " + smtpPort);
          Console.WriteLine ("");
          Console.WriteLine (ex.ToString ());
          Console.WriteLine ("");
        }
      } else {
        Console.WriteLine ("");
        Console.WriteLine ("SMTP server and email address not provided. Skipping error report email.");
        Console.WriteLine ("");
      }
    }

    public static void Publish (Arguments arguments, string output, List<string> topics)
    {
      var incomingLinePrefix = ConfigurationSettings.AppSettings ["IncomingLinePrefix"];

      var data = GetLastDataLine (output);

      if (!String.IsNullOrEmpty (data)) {

        var dividerCharacter = ConfigurationSettings.AppSettings ["DividerSplitCharacter"].ToCharArray () [0];
        var equalsCharacter = ConfigurationSettings.AppSettings ["EqualsSplitCharacter"].ToCharArray () [0];
        var userId = GetConfigValue (arguments, "UserId");
        var deviceName = GetConfigValue (arguments, "DeviceName");
        var topicPrefix = userId;
        var useTopicPrefix = Convert.ToBoolean (ConfigurationSettings.AppSettings ["UseTopicPrefix"]);
        var summaryKey = GetConfigValue (arguments, "SummaryKey");

        var deviceTopic = deviceName;

        if (useTopicPrefix)
          deviceTopic = topicPrefix + deviceTopic;

        // TODO: Remove if not needed. Should be obsolete now.
        var summaryValue = "";

        foreach (var item in data.Split(dividerCharacter)) {
          if (item.Contains (equalsCharacter.ToString ())) {
            var key = item.Substring (0, item.IndexOf (equalsCharacter));
            var value = item.Substring (item.IndexOf (equalsCharacter) + 1, item.Length - item.IndexOf (equalsCharacter) - 1);

            if (!String.IsNullOrEmpty (value)) {
              if (key == summaryKey)
                summaryValue = value;

              var fullTopic = deviceTopic + "/" + key;

              if (IsVerbose)
                Console.WriteLine (fullTopic + ":" + value);

              if (!topics.Contains (fullTopic))
                topics.Add (fullTopic);

              MqttClient.Publish (fullTopic, Encoding.UTF8.GetBytes (value),
                MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, // QoS level
                true);
            }
          }
        }

        var timeTopic = deviceTopic + "/Time";

        var time = DateTime.Now.ToString ("MM/dd/yyyy HH:mm:ss");

        if (IsVerbose)
          Console.WriteLine (timeTopic + ":" + time);

        MqttClient.Publish (timeTopic, Encoding.UTF8.GetBytes (time),
          MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, // QoS level
          true);

        var statusMessageTopic = deviceTopic + "/StatusMessage";

        MqttClient.Publish (statusMessageTopic, Encoding.UTF8.GetBytes ("Online"),
          MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, // QoS level
          true);

        // TODO: Remove if not needed. This triggers push notifications which should
        // be used for alerts not just for data
        /*var pushNotificationTopic = "/push/" + deviceName;

                if (IsVerbose)
                    Console.WriteLine (pushNotificationTopic + ":" + deviceName + ":" + summaryValue);

                var pushSummary = deviceName + ":" + summaryValue;

                client.Publish (pushNotificationTopic, Encoding.UTF8.GetBytes (pushSummary),
                    MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, // QoS level
                    false);*/
      }
    }

    public static void MqttClient_MqttMsgPublishReceived (object sender, MqttMsgPublishEventArgs e)
    {
      var topic = e.Topic;
      var topicSections = topic.Split ('/');
      var subTopic = topicSections [topicSections.Length - 2];

      if (IsVerbose)
        Console.WriteLine ("Subtopic: " + subTopic);

      var message = System.Text.Encoding.Default.GetString (e.Message);

      if (IsVerbose)
        Console.WriteLine ("Message received: " + message);

      var serialCommand = subTopic + IncomingKeyValueSeparator + message;

      Console.WriteLine ("Incoming command: " + serialCommand);

      SendMessageToDevice (serialCommand);
    }

    public static void SendMessageToDevice (string message)
    {
      try {
        if (!Client.Port.IsOpen)
          Client.Open ();

        Client.WriteLine (message);

        //Client.Close();
      } catch (Exception ex) {
        Console.WriteLine ("Failed to send message to device");
        Console.WriteLine (ex.ToString ());
        Console.WriteLine ();
        Console.WriteLine ("Waiting for " + WaitTimeBeforeRetry + " seconds then retrying");

        Thread.Sleep (WaitTimeBeforeRetry * 1000);

        SendMessageToDevice (message);
      }
    }

    public static string GetConfigValue (Arguments arguments, string argumentKey)
    {
      var value = String.Empty;

      if (IsVerbose)
        Console.WriteLine ("Getting config/argument value for: " + argumentKey);

      if (arguments.Contains (argumentKey)) {
        value = arguments [argumentKey];
        if (IsVerbose)
          Console.WriteLine ("Found in arguments");
      } else {

        try {
          value = ConfigurationManager.AppSettings [argumentKey];
        } catch (Exception ex) {
          Console.WriteLine ("Failed to get configuration value: " + argumentKey);
          throw ex;
        }

        if (IsVerbose)
          Console.WriteLine ("Looking in config");
      }

      return value;
    }

    public static string[] GetSubscribeTopics (Arguments arguments)
    {
      var topics = GetConfigValue (arguments, "SubscribeTopics").Split (',');
      var userId = GetConfigValue (arguments, "UserId");
      var deviceName = GetConfigValue (arguments, "DeviceName");
      var topicPrefix = userId;
      var useTopicPrefix = Convert.ToBoolean (ConfigurationSettings.AppSettings ["UseTopicPrefix"]);


      var list = new List<String> ();

      foreach (var topic in topics) {
        var fullTopic = deviceName + "/" + topic + "/in";

        if (useTopicPrefix)
          fullTopic = topicPrefix + fullTopic;

        list.Add (fullTopic);
      }

      return list.ToArray ();
    }

    public static Dictionary<string, string> ParseOutputLine (string outputLine)
    {
      var dictionary = new Dictionary<string, string> ();

      if (IsValidOutputLine (outputLine)) {
        foreach (var pair in outputLine.Split(';')) {
          var colonPosition = pair.IndexOf (":");
          if (colonPosition > 0) {
            var key = pair.Substring (0, colonPosition);
            var value = pair.Substring (colonPosition + 1, pair.Length - colonPosition - 1);
            dictionary [key] = value;
          }
        }
      }

      return dictionary;
    }

    public static string GetLastDataLine (string output)
    {
      var lines = output.Trim ().Split ('\n');

      for (int i = lines.Length - 1; i >= 0; i--) {
        var line = lines [i].Trim ();
        if (IsValidOutputLine (line))
          return line;
      }

      return String.Empty;
    }

    public static bool IsValidOutputLine (string outputLine)
    {
      var dataPrefix = "D;";

      var dataPostFix = ";;";

      return outputLine.StartsWith (dataPrefix)
      && outputLine.EndsWith (dataPostFix);
    }

    public static string GetSelfHostName ()
    {
      var starter = new ProcessStarter ();

      starter.WriteOutputToConsole = false;

      starter.Start ("hostname");

      var selfHostName = "";
      if (!starter.IsError) {
        selfHostName = starter.Output.Trim ();
      }

      Console.WriteLine ("Self: " + selfHostName);

      return selfHostName;
    }
  }
}
