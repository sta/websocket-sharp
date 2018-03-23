$root = (split-path -parent $MyInvocation.MyCommand.Definition) + '\..'

Write-Host "root: $root"

$version = [System.Reflection.Assembly]::LoadFile("$root\websocket-sharp\bin\Release\net45\DotNetProjects.websocket-sharp.dll").GetName().Version
$versionStr = "{0}.{1}.{2}" -f ($version.Major, $version.Minor, $version.Build)

Write-Host "Setting .nuspec version tag to $versionStr"

$content = (Get-Content $root\NuGet\websocket.nuspec) 
$content = $content -replace '\$version\$',$versionStr

$content | Out-File $root\nuget\websocket.compiled.nuspec

& $root\NuGet\NuGet.exe pack $root\nuget\websocket.compiled.nuspec