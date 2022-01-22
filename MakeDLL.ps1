param ([string]$output = ".")

sal devenv "C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\Common7\IDE\devenv"

$dll_folder = "websocket-sharp\bin\Release"
$dll_name = "websocket-sharp.dll"

cd $PSScriptRoot
devenv websocket-sharp.sln /build Release /project websocket-sharp\websocket-sharp.csproj /projectconfig Release


Copy-Item ".\$dll_folder\$dll_name" $dll_name
