echo "Retrieving required libraries..."

if [ ! -f nuget.exe ]; then
    echo "nuget.exe not found. Downloading..."
    wget http://nuget.org/nuget.exe
fi

mono nuget.exe update -self

echo "Installing libraries..."

mono nuget.exe install M2Mqtt
mono nuget.exe install duinocom.core -version 1.0.5

echo "Installation complete!"
