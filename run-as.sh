echo "Running as specific device"
echo "  Device name: $1"
mono app/BridgeArduinoSerialToMqttSplitCsv.exe --DeviceName=$1
