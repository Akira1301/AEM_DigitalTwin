param([switch]$Validate)
$ErrorActionPreference = 'Stop'
$factoryProject = $PSScriptRoot
$factoryVersionLine = Get-Content -LiteralPath (Join-Path $factoryProject 'ProjectSettings\ProjectVersion.txt') | Select-Object -First 1
$factoryVersion = $factoryVersionLine.Split(':')[1].Trim()
$factoryEditor = Join-Path ${env:ProgramFiles} "Unity\Hub\Editor\$factoryVersion\Editor\Unity.exe"
if (-not (Test-Path -LiteralPath $factoryEditor)) { throw "Unity $factoryVersion not found: $factoryEditor" }
$factoryLogs = Join-Path $factoryProject 'Logs'
New-Item -ItemType Directory -Path $factoryLogs -Force | Out-Null

function Invoke-FactoryUnity([string]$Method, [string]$LogName, [bool]$Quit, [string]$SuccessMarker) {
    $factoryLog = Join-Path $factoryLogs $LogName
    $factoryArgs = '-batchmode -projectPath "{0}" -executeMethod {1} -logFile "{2}" -screen-width 1600 -screen-height 900' -f $factoryProject, $Method, $factoryLog
    if ($Quit) { $factoryArgs += ' -quit' }
    Write-Host "Unity: $Method"
    $factoryProcess = Start-Process -FilePath $factoryEditor -ArgumentList $factoryArgs -WindowStyle Hidden -PassThru
    if (-not $factoryProcess.WaitForExit(180000)) {
        throw "Unity has not finished within 180 seconds. Check the running Unity process and $factoryLog before starting it again."
    }
    Write-Host "Unity exit code: $($factoryProcess.ExitCode)"
    $factoryLogText = Get-Content -LiteralPath $factoryLog -Raw -Encoding UTF8
    if ($factoryProcess.ExitCode -ne 0 -or $factoryLogText -match 'error CS\d+' -or -not $factoryLogText.Contains($SuccessMarker)) {
        Get-Content -LiteralPath $factoryLog -Tail 60 -Encoding UTF8
        throw "Unity validation failed. See $factoryLog"
    }
    Write-Host "OK: $factoryLog"
}

Invoke-FactoryUnity 'FactorySceneBuilder.Build' 'FactoryBuild.log' $true 'FACTORY_BUILD_SUCCESS'
if ($Validate) { Invoke-FactoryUnity 'FactorySmokeTest.Run' 'FactorySmokeTest.log' $false 'FACTORY_TEST_SUCCESS' }
Write-Host 'Ready: Assets/Scenes/Factory.unity. Open the scene and press Play.'
