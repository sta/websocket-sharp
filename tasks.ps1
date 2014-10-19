properties {
	$configuration = "Release"
	$platform = "Any CPU"
	$folderPath = ".\"
	$cleanPackages = $false
	$oldEnvPath = ""
	$fwkVersions = "4.5","4.5.1","4.5.2"
}

task default -depends CleanUpMsBuildPath

task CleanUpMsBuildPath -depends BuildPackages {
	if($oldEnvPath -ne "")
	{
		Write-Host "Reverting Path variable"
		$Env:Path = $oldEnvPath
	}
}

task BuildPackages -depends Test {
	Exec { nuget pack websocket-sharp.nuspec -OutputDirectory BuildOutput }
	Exec { nuget pack websocket-sharp.symbols.nuspec -OutputDirectory BuildOutput -Symbols }
}

task Test -depends Compile, Clean {
	'Running Tests'
	foreach($fwk in $fwkVersions) {
		Write-Host "Building v. $fwk"
		$output = ".\BuildOutput\$fwk\$configuration"
		$testAssembly = Resolve-Path "$output\WebSocketSharp.Tests.dll"
		Exec { .\packages\NUnit.Runners.2.6.3\tools\nunit-console.exe $testAssembly }
	}
}

task Compile -depends UpdatePackages {
	$msbuild = Resolve-Path "${Env:ProgramFiles(x86)}\MSBuild\12.0\Bin\MSBuild.exe"
	foreach($fwk in $fwkVersions) {
		$output = "..\BuildOutput\$fwk\$configuration"
		$options = "/p:configuration=$configuration;platform=$platform;TargetFrameworkVersion=$fwk;OutputPath=$output"
		Exec { & $msbuild websocket-sharp.sln $options }
	}
	'Executed Compile!'
}

task UpdatePackages -depends Clean {
	$packageConfigs = Get-ChildItem -Path .\ -Include "packages.config" -Recurse
	foreach($config in $packageConfigs){
		#Write-Host $config.DirectoryName
		Exec { nuget i $config.FullName -o packages -source https://nuget.org/api/v2/ }
	}
}

task Clean -depends CheckMsBuildPath { 
	Get-ChildItem $folderPath -include bin,obj -Recurse | foreach ($_) { remove-item $_.fullname -Force -Recurse }
	if($cleanPackages -eq $true){
		if(Test-Path "$folderPath\packages"){
			Get-ChildItem "$folderPath\packages" -Recurse | Where { $_.PSIsContainer } | foreach ($_) { Write-Host $_.fullname; remove-item $_.fullname -Force -Recurse }
		}
	}
	
	if(Test-Path "$folderPath\BuildOutput"){
		Get-ChildItem "$folderPath\BuildOutput" -Recurse | foreach ($_) { Write-Host $_.fullname; remove-item $_.fullname -Force -Recurse }
	}
}

task CheckMsBuildPath {
	$envPath = $Env:Path
	if($envPath.Contains("C:\Windows\Microsoft.NET\Framework\v4.0") -eq $false)
	{
		if(Test-Path "C:\Windows\Microsoft.NET\Framework\v4.0.30319")
		{
			$oldEnvPath = $envPath
			$Env:Path = $envPath + ";C:\Windows\Microsoft.NET\Framework\v4.0.30319"
		}
		else
		{
			throw "Could not determine path to MSBuild. Make sure you have .NET 4.0.30319 installed"
		}
	}
}

task ? -Description "Helper to display task info" {
	Write-Documentation
}
