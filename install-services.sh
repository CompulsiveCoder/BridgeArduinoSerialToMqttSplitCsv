FILES=svc/*
for f in $FILES
do
  echo $f
  filename=$(basename "$f")
  echo $filename

  echo "Found service:"
  echo $filename

  echo ""

  echo "Copying to /lib/systemd/system/:"

  sudo cp -fv $f /lib/systemd/system/
  sudo chmod 644 /lib/systemd/system/$filename
  sudo systemctl daemon-reload
  sudo systemctl enable $filename
done

echo "Reboot required"
#sudo reboot
