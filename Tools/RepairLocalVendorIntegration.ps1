# Reapply project-owned integration fixes without distributing licensed assets.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$editorPath = Join-Path $projectRoot 'Assets/_ARCHITECT/Scripts/CustomButton.cs'
$editorSource = [IO.File]::ReadAllText($editorPath)
if ($editorSource -notmatch '#if UNITY_EDITOR') {
    [IO.File]::WriteAllText($editorPath, "#if UNITY_EDITOR`n" + $editorSource + "`n#endif`n")
}
$fogPath = Join-Path $projectRoot 'Assets/_AERO_VOLUMETRIC_FOG/Shaders/Volumetric Fog.hlsl'
$fogSource = [IO.File]::ReadAllText($fogPath)
if ($fogSource -notmatch 'bool hasEchoSurface') {
    $needle = 'float echoFogReveal = IsEchoFogRevealIgnored(surfacePositionWS)'
    if (-not $fogSource.Contains($needle)) { throw 'Echo fog integration not found; restore it before applying the sky guard.' }
    $replacement = @'
#if UNITY_REVERSED_Z
        bool hasEchoSurface = rawDepth > 0.000001;
    #else
        bool hasEchoSurface = rawDepth < 0.999999;
    #endif
    float echoFogReveal = !hasEchoSurface || IsEchoFogRevealIgnored(surfacePositionWS)
'@
    [IO.File]::WriteAllText($fogPath, $fogSource.Replace($needle, $replacement))
}
Write-Output 'Architect editor-only guard and AERO sky-depth guard are present.'
