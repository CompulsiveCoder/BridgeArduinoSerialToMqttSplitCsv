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
#  7za a -t7z /media/user/storage/zipped/${filename}.7z $f -mx9 -r -ppassword -mhe
done

#for filename in /Data/*.txt; do
#    for ((i=0; i<=3; i++)); do
#        ./MyProgram.exe "$filename" "Logs/$(basename "$filename" .txt)_Log$i.txt"
    


#
#

#sudo systemctl daemon-reload
#sudo systemctl enable greensense-jenkins-docker.service

#echo "Reboot required"
#sudo reboot

#    done
#done
