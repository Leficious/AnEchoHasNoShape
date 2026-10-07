# Reapply the project's foliage depth fix after restoring/updating the licensed pack
# or regenerating its shader in Amplify. Does not redistribute the vendor shader.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$shaderPath = Join-Path $projectRoot 'Assets/_POLYTOPE_ENVIRONMENTS/Lowpoly_Environments/Sources/Shaders/PT_Vegetation_Foliage_Shader.shader'
$source = [System.IO.File]::ReadAllText($shaderPath)

# Leave Amplify's serialized graph untouched; this patches the generated HLSL.
$graphOffset = $source.IndexOf('/*ASEBEGIN')
if ($graphOffset -lt 0) { throw 'Expected Amplify graph marker was not found. Inspect the updated shader before patching.' }
$shader = $source.Substring(0, $graphOffset)
$graph = $source.Substring($graphOffset)
if ($shader -notmatch 'Shader "Polytope Studio/PT_Vegetation_Foliage_Shader"' -or
    $shader -notmatch '#define _ALPHATEST_ON 1' -or
    $shader -notmatch 'clip\(Alpha - AlphaClipThreshold\)') {
    throw 'The foliage shader structure has changed; the cutout fix needs review.'
}

# Fog samples the opaque camera depth texture. Binary leaf cutouts must be in
# that queue, with matching clipping in their color, depth, and shadow passes.
$shader = $shader.Replace('"RenderType"="Transparent" "Queue"="Transparent"', '"RenderType"="TransparentCutout" "Queue"="AlphaTest"')
$shader = [regex]::Replace($shader, '(?m)^([\t ]*)ZWrite Off([\t ]*\r?)$', '${1}ZWrite On${2}')
$shader = $shader.Replace('Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha', 'Blend One Zero')
$shader = [regex]::Replace($shader, '(?m)^[\t ]*#define _SURFACE_TYPE_TRANSPARENT 1\r?\n', '')
# Preserve the original forward pass's translucency lighting in deferred URP.
$shader = $shader.Replace('"LightMode"="UniversalForward"', '"LightMode"="UniversalForwardOnly"')
$shader = $shader.Replace('"LightMode"="DepthNormals"', '"LightMode"="DepthNormalsOnly"')
$shader = $shader.Replace('"LightMode"="UniversalGBuffer"', '"LightMode"="PolytopeFoliageUnusedGBuffer"')

if ($shader -notmatch '"RenderType"="TransparentCutout" "Queue"="AlphaTest"' -or
    $shader -match '#define _SURFACE_TYPE_TRANSPARENT|Blend SrcAlpha|ZWrite Off') {
    throw 'Could not establish consistent opaque cutout render states.'
}
$patched = $shader + $graph
if ($patched -ne $source) {
    [System.IO.File]::WriteAllText($shaderPath, $patched, [System.Text.UTF8Encoding]::new($false))
    Write-Output 'Applied Polytope foliage fog-depth fix. Return to Unity to reimport the shader.'
} else {
    Write-Output 'Polytope foliage fog-depth fix is already applied.'
}
