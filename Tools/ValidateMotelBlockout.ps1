param([string]$UnityData)
$ErrorActionPreference = 'Stop'
$previousProcessorCount = $env:DOTNET_PROCESSOR_COUNT
$env:DOTNET_PROCESSOR_COUNT = '2'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    if (!$UnityData) {
        $version = [regex]::Match((Get-Content ProjectSettings/ProjectVersion.txt -Raw), 'm_EditorVersion: (\S+)').Groups[1].Value
        $UnityData = "C:/Program Files/Unity/Hub/Editor/$version/Editor/Data"
    }
    $compiler = Get-ChildItem "$UnityData/DotNetSdk/sdk" -Recurse -Filter csc.dll | Sort-Object FullName -Descending | Select-Object -First 1
    if (!$compiler) { throw 'Unity Roslyn compiler not found; supply -UnityData.' }
    $dotnet = Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
    $outputPath = Join-Path $projectRoot ('Temp/MotelValidation-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $outputPath | Out-Null
    $utf8 = [Text.UTF8Encoding]::new($false)
    $runtimeDll = (Join-Path $outputPath 'Assembly-CSharp.dll').Replace('\','/')
    # A failed Player build leaves newer, incomplete response files. Use the Editor pair.
    $runtimeRsp = Get-ChildItem Library/Bee/artifacts -Recurse -Filter Assembly-CSharp.rsp |
        Where-Object { Test-Path (Join-Path $_.DirectoryName 'Assembly-CSharp-Editor.rsp') } |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (!$runtimeRsp) { throw 'Unity compiler response files not found. Let Unity import and compile the project first.' }
    $editorRsp = Join-Path $runtimeRsp.DirectoryName 'Assembly-CSharp-Editor.rsp'
    $runtimeLines = @(Get-Content $runtimeRsp.FullName | Where-Object { $_ -notmatch '^-(out|refout):' -and $_ -notmatch '^"Assets[/\\]_Project[/\\]' })
    $runtimeLines += '-out:"' + $runtimeDll + '"'
    $runtimeLines += @(Get-ChildItem Assets/_Project/Scripts -File -Filter '*.cs' | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
    $runtimeCheck = Join-Path $outputPath 'runtime.rsp'
    [IO.File]::WriteAllLines($runtimeCheck, $runtimeLines, $utf8)
    & $dotnet $compiler.FullName "@$runtimeCheck"
    if ($LASTEXITCODE -ne 0) { throw 'Runtime compilation failed.' }
    Write-Output 'PASS: runtime compilation.'
    $editorLines = @(Get-Content $editorRsp | Where-Object { $_ -notmatch '^-(out|refout):' -and $_ -notmatch '^-r:.*[/\\]Assembly-CSharp\.ref\.dll' -and $_ -notmatch '^"Assets[/\\]_Project[/\\]' })
    $editorLines += '-out:"' + (Join-Path $outputPath 'Assembly-CSharp-Editor.dll').Replace('\','/') + '"'
    $editorLines += '-r:"' + $runtimeDll + '"'
    $editorLines += @(Get-ChildItem Assets/_Project/Scripts/Editor -Recurse -File -Filter '*.cs' | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
    $editorCheck = Join-Path $outputPath 'editor.rsp'
    [IO.File]::WriteAllLines($editorCheck, $editorLines, $utf8)
    & $dotnet $compiler.FullName "@$editorCheck"
    if ($LASTEXITCODE -ne 0) { throw 'Editor compilation failed.' }
    Write-Output 'PASS: editor compilation.'

    $runner = Join-Path $outputPath 'Check.cs'
    [IO.File]::WriteAllText($runner, @'
using System;
using Motel.Editor;
public static class Check {
    public static int Main() {
        try { Console.WriteLine(MotelBlockoutValidation.Validate(MotelBlockoutPlan.Create())); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
    }
}
'@, $utf8)
    $core = Get-ChildItem "$UnityData/NetCoreRuntime/shared/Microsoft.NETCore.App" -Directory | Sort-Object Name -Descending | Select-Object -First 1
    $lines = @('-target:exe', '-nologo', '-langversion:9.0')
    $lines += '-out:"' + (Join-Path $outputPath 'Check.dll') + '"'
    $referencePack = Get-ChildItem "$UnityData/DotNetSdk/packs/Microsoft.NETCore.App.Ref" -Directory | Sort-Object Name -Descending | Select-Object -First 1
    $lines += @(Get-ChildItem (Join-Path $referencePack.FullName 'ref/net8.0') -Filter '*.dll' | ForEach-Object { '-r:"' + $_.FullName + '"' })
    $lines += '"Assets/_Project/Scripts/Editor/MotelBlockoutPlan.cs"'
    $lines += '"Assets/_Project/Scripts/Editor/MotelGuestRoomModule.cs"'
    $lines += '"Assets/_Project/Scripts/Editor/MotelBlockoutValidation.cs"'
    $lines += '"' + $runner + '"'
    $checkRsp = Join-Path $outputPath 'check.rsp'
    [IO.File]::WriteAllLines($checkRsp, $lines, $utf8)
    & $dotnet $compiler.FullName "@$checkRsp"
    if ($LASTEXITCODE -ne 0) { throw 'Geometry checker compilation failed.' }
    $configuration = @{ runtimeOptions = @{ tfm = 'net8.0'; framework = @{ name = 'Microsoft.NETCore.App'; version = $core.Name } } }
    [IO.File]::WriteAllText((Join-Path $outputPath 'Check.runtimeconfig.json'), ($configuration | ConvertTo-Json -Depth 5), $utf8)
    & $dotnet (Join-Path $outputPath 'Check.dll')
    if ($LASTEXITCODE -ne 0) { throw 'Blockout geometry validation failed.' }
    Write-Output "Validation output: $outputPath"
} finally {
    Pop-Location
    if ($null -eq $previousProcessorCount) { Remove-Item Env:DOTNET_PROCESSOR_COUNT -ErrorAction SilentlyContinue }
    else { $env:DOTNET_PROCESSOR_COUNT = $previousProcessorCount }
}
