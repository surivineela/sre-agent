 apt-get update
 apt-get -y install curl
diagDir="`pwd`/diag-tools"
mkdir $diagDir
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --install-dir $diagDir/.dotnet
export PATH="$DOTNET_ROOT:$diagDir:$PATH"
$diagDir/.dotnet/dotnet tool install --tool-path $diagDir dotnet-dump
export DOTNET_ROOT="$diagDir/.dotnet"
export PATH="$DOTNET_ROOT:$diagDir:$PATH"
curl https://dotnetanalysis.blob.core.windows.net/lin64/DotnetAnalyzer -o dotnetanalyzer
chmod +x dotnetanalyzer
echo  ">>COMPLETED DOWNLOAD<<"
dotnet-dump collect -p 1 -o dump
echo  ">>STARTED ANALYSIS<<"
./dotnetanalyzer analyze-memory dump
echo  "COMPLETED ANALYSIS"
