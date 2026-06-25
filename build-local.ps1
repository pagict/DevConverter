# Optional local build script. This still requires .NET SDK 9.
# The recommended no-local-compiler path is GitHub Actions.
$ErrorActionPreference = "Stop"
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained false -p:Platform=x64 -o .\publish\DevConvert
Compress-Archive -Path .\publish\DevConvert -DestinationPath .\DevConvert-PowerToysRun-x64.zip -Force
Write-Host "Built .\DevConvert-PowerToysRun-x64.zip"
