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

# Keep the ground-check fix reproducible without publishing vendor source.
$controllerPath = Join-Path $projectRoot 'Assets/_FIRST_PERSON_CONTROLLER/FirstPersonController.cs'
if (Test-Path -LiteralPath $controllerPath) {
    $controllerSource = [IO.File]::ReadAllText($controllerPath)
    $oldGroundCheck = 'Physics.Raycast(origin, direction, out RaycastHit hit, distance)'
    $newGroundCheck = 'Physics.Raycast(origin, direction, out RaycastHit hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)'
    if ($controllerSource.Contains($oldGroundCheck)) {
        [IO.File]::WriteAllText($controllerPath, $controllerSource.Replace($oldGroundCheck, $newGroundCheck))
        Write-Output 'Player ground check now ignores triggers.'
    } elseif ($controllerSource -notmatch 'Physics\.Raycast\(origin, direction, out RaycastHit hit, distance,\s*Physics\.DefaultRaycastLayers, QueryTriggerInteraction\.Ignore\)') {
        throw 'Player ground-check implementation changed; inspect it before reapplying the trigger fix.'
    }
    $controllerSource = [IO.File]::ReadAllText($controllerPath)
    if ($controllerSource -notmatch 'Visual yaw is independent') {
        $controllerSource = $controllerSource.Replace("`r`n", "`n")
        $yawRepairs = @(
            @('rb.interpolation = RigidbodyInterpolation.Interpolate;', "rb.interpolation = RigidbodyInterpolation.Interpolate;`n        yaw = transform.eulerAngles.y;"),
            @('yaw = transform.localEulerAngles.y + Input.GetAxis("Mouse X") * mouseSensitivity;', 'yaw = Mathf.Repeat(yaw + Input.GetAxis("Mouse X") * mouseSensitivity, 360f);'),
            @('transform.localEulerAngles = new Vector3(0, yaw, 0);', "// Visual yaw is independent of the interpolated physics body.`n            joint.rotation = Quaternion.Euler(0f, yaw, 0f);"),
            @("    void FixedUpdate()`n    {", "    private void LateUpdate()`n    {`n        if (joint != null) joint.rotation = Quaternion.Euler(0f, yaw, 0f);`n    }`n`n    void FixedUpdate()`n    {`n        if (cameraCanMove) rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));"),
            @('Vector3 targetVelocity = transform.TransformDirection(localTargetVelocity);', 'Vector3 targetVelocity = Quaternion.Euler(0f, yaw, 0f) * localTargetVelocity;'),
            @('transform.SetPositionAndRotation(destination.position, bodyRotation);', "transform.SetPositionAndRotation(destination.position, bodyRotation);`n        if (joint != null) joint.rotation = bodyRotation;")
        )
        foreach ($repair in $yawRepairs) {
            if (-not $controllerSource.Contains($repair[0])) { throw 'Player controller changed; inspect it before applying the independent-yaw repair.' }
            $controllerSource = $controllerSource.Replace($repair[0], $repair[1])
        }
        [IO.File]::WriteAllText($controllerPath, $controllerSource)
        Write-Output 'Player camera yaw separated from physics interpolation.'
    }
}
