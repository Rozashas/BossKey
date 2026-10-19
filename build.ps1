$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\BossKey.csproj'
$output = Join-Path $PSScriptRoot 'App'
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false "-p:PathMap=$PSScriptRoot=/src" -o $output
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
