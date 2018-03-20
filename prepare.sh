echo "Preparing project"
echo "Dir: $PWD"

DIR=$PWD

sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
sudo echo "deb http://download.mono-project.com/repo/debian wheezy main" > /etc/apt/sources.list.d/mono-xamarin.list
sudo apt-get update && apt-get install -y --no-install-recommends ca-certificates-mono nuget
sudo cert-sync /etc/ssl/certs/ca-certificates.crt
#nuget update -self

cd $DIR

