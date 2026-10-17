param([switch]$Validate, [switch]$CheckOriginal)
$ErrorActionPreference = 'Stop'
$labProject = $PSScriptRoot
$labVersion = (Get-Content (Join-Path $labProject 'ProjectSettings\ProjectVersion.txt') | Select-Object -First 1).Split(':')[1].Trim()
$labEditor = Join-Path ${env:ProgramFiles} "Unity\Hub\Editor\$labVersion\Editor\Unity.exe"
if (-not (Test-Path -LiteralPath $labEditor)) { throw "Unity $labVersion not found." }
New-Item -ItemType Directory -Path (Join-Path $labProject 'Logs') -Force | Out-Null
function Invoke-LabUnity([string]$Method,[string]$Log,[string]$Marker,[bool]$Quit) {
    $labLog = Join-Path $labProject "Logs\$Log"
    $labArgs = '-batchmode -projectPath "{0}" -executeMethod {1} -logFile "{2}"' -f $labProject,$Method,$labLog
    if ($Quit) { $labArgs += ' -quit' }
    Write-Host "Unity: $Method"
    $labProcess = Start-Process -FilePath $labEditor -ArgumentList $labArgs -WindowStyle Hidden -PassThru
    if (-not $labProcess.WaitForExit(180000)) { throw "Unity timeout. Check $labLog before restarting." }
    Write-Host "UNITY_EXIT_CODE=$($labProcess.ExitCode)"
    $labText = Get-Content -LiteralPath $labLog -Raw -Encoding UTF8
    if ($labProcess.ExitCode -ne 0 -or $labText -match 'error CS\d+' -or -not $labText.Contains($Marker)) {
        Get-Content -LiteralPath $labLog -Tail 60 -Encoding UTF8
        throw "Validation failed: $labLog"
    }
}
Invoke-LabUnity 'LabSceneBuilder.Build' 'LabBuild.log' 'LAB_SCENES_BUILD_SUCCESS' $true
if ($Validate) { Invoke-LabUnity 'LabVerification.Run' 'LabVerification.log' 'LAB_TEST_SUCCESS' $false }
if ($CheckOriginal) { Invoke-LabUnity 'FactorySmokeTest.Run' 'FactorySmokeTest.log' 'FACTORY_TEST_SUCCESS' $false }
Write-Host 'Ready: Lab2Telemetry.unity and FactoryConnected.unity'
