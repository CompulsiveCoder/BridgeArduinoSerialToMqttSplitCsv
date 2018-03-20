DIR=$PWD

echo "Initializing project"
echo "Dir: $PWD"

cd lib && \
sh getlibs.sh && \
cd $DIR && \

git submodule update --init && \

cd mod/duinocom && \
sh init.sh && \
sh build.sh && \
cd $DIR && \

echo "Init complete!"