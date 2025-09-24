set -e
export TERM=xterm
export COLUMNS=120
export LINES=200
apt-get update
apt-get -y install curl
diagDir="`pwd`/diag-tools"
mkdir -p $diagDir
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --install-dir $diagDir/.dotnet
export PATH="$DOTNET_ROOT:$diagDir:$PATH"
$diagDir/.dotnet/dotnet tool install --tool-path $diagDir dotnet-trace
export DOTNET_ROOT="$diagDir/.dotnet"
export PATH="$DOTNET_ROOT:$diagDir:$PATH"
echo  ">>COMPLETED DOWNLOAD<<"
echo  ">>STARTED ANALYSIS<<"
dotnet-trace ps | cat
echo  ">>COMPLETED ANALYSIS<<"