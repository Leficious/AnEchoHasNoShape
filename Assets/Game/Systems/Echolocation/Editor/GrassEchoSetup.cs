using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AnEchoHasNoShape.Echolocation.Editor
{
    /// <summary>Reapplies the grass integration after restoring the licensed asset pack.</summary>
    public static class GrassEchoSetup
    {
        private const string Root = "Assets/_POLYTOPE_ENVIRONMENTS/Lowpoly_Environments/Sources/";
        private const string ShaderPath = Root + "Shaders/PT_Vegetation_Plants_Shader.shader";
        private const string MaterialPath = Root + "Materials/PT_Grass_Mat.mat";

        [MenuItem("Tools/An Echo Has No Shape/Set Up Grass Echo Wash")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run grass setup outside Play Mode.");

            PatchShader();
            var report = new StringBuilder();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null || !material.HasProperty("_GrassEchoStrength"))
                throw new InvalidOperationException("The grass material or patched shader is unavailable.");
            Undo.RecordObject(material, "Configure grass echo wash");
            // Preserve later tuning when rerunning the setup.
            if (material.GetFloat("_GrassEchoStrength") <= 0f)
            {
                material.SetFloat("_GrassEchoStrength", 0.25f);
                material.SetFloat("_GrassEchoFadeSeconds", 1.5f);
                material.SetFloat("_GrassEchoPermanentStrength", 0.25f);
            }
            material.enableInstancing = true;
            material.renderQueue = 2450;
            material.SetOverrideTag("RenderType", "TransparentCutout");
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);

            // Instanced detail meshes use the prefab material. Legacy grass mode
            // substitutes Unity's built-in grass shader and cannot see the wash.
            foreach (string guid in AssetDatabase.FindAssets("t:TerrainData", new[] { "Assets/Game/Terrain/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                DetailPrototype[] prototypes = data.detailPrototypes;
                bool changed = false;
                bool hasGrass = false;
                var paintedLayers = new System.Collections.Generic.Dictionary<int, int[,]>();
                for (int i = 0; i < prototypes.Length; i++)
                {
                    DetailPrototype prototype = prototypes[i];
                    if (prototype.prototype == null || prototype.prototype.name != "PT_Grass_02") continue;
                    hasGrass = true;
                    report.AppendLine(path + " detail " + i + ": PT_Grass_02, instanced=" + prototype.useInstancing + ", mode=" + prototype.renderMode);
                    if (prototype.useInstancing && prototype.usePrototypeMesh && prototype.renderMode == DetailRenderMode.VertexLit) continue;
                    if (!changed) Undo.RegisterCompleteObjectUndo(data, "Enable instanced grass echo material");
                    paintedLayers[i] = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, i);
                    prototype.usePrototypeMesh = true;
                    prototype.renderMode = DetailRenderMode.VertexLit;
                    prototype.useInstancing = true;
                    prototypes[i] = prototype;
                    changed = true;
                }
                if (!changed)
                {
                    if (hasGrass) data.RefreshPrototypes();
                    continue;
                }
                data.detailPrototypes = prototypes;
                // Preserve the painted density exactly if Unity rebuilds a detail layer.
                foreach (var layer in paintedLayers)
                {
                    int[,] after = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer.Key);
                    bool equal = true;
                    for (int y = 0; y < after.GetLength(0) && equal; y++)
                        for (int x = 0; x < after.GetLength(1); x++)
                            if (after[y, x] != layer.Value[y, x]) { equal = false; break; }
                    if (!equal) data.SetDetailLayer(0, 0, layer.Key, layer.Value);
                }
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
                data.RefreshPrototypes();
                report.AppendLine("Updated instancing; painted density preserved.");
            }
            foreach (Terrain terrain in Terrain.activeTerrains) terrain.Flush();

            foreach (string passName in new[] { "Forward", "DepthOnly", "DepthNormals", "ShadowCaster" })
            {
                int pass = material.FindPass(passName);
                if (pass < 0) throw new InvalidOperationException("Missing grass shader pass: " + passName);
                ShaderUtil.CompilePass(material, pass, true);
            }
            foreach (var message in ShaderUtil.GetShaderMessages(material.shader))
            {
                report.AppendLine(message.severity + ": " + message.message);
                if (message.severity.ToString() == "Error")
                    throw new InvalidOperationException(message.message);
            }
            report.AppendLine("PASS: grass material configured; shader passes compiled.");
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/GrassEchoSetup.txt", report.ToString());
            Debug.Log(report.ToString());
        }

        private static void PatchShader()
        {
            string source = File.ReadAllText(ShaderPath);
            int graphStart = source.IndexOf("/*ASEBEGIN", StringComparison.Ordinal);
            if (graphStart < 0) throw new InvalidOperationException("Unexpected plants shader format.");
            string shader = source.Substring(0, graphStart);
            if (!shader.Contains("#define GRASS_ECHO_WASH_PATCH"))
            {
                if (!shader.Contains("#define _ALPHATEST_ON 1"))
                    throw new InvalidOperationException("Grass requires the original alpha-cutout shader.");
                shader = shader.Replace("\"RenderType\"=\"Transparent\" \"Queue\"=\"Transparent\"",
                    "\"RenderType\"=\"TransparentCutout\" \"Queue\"=\"AlphaTest\"");
                shader = shader.Replace("ZWrite Off", "ZWrite On");
                shader = shader.Replace("Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha", "Blend One Zero");
                shader = Regex.Replace(shader, @"(?m)^[\t ]*#define _SURFACE_TYPE_TRANSPARENT 1\r?\n", "");
                shader = shader.Replace("\"LightMode\"=\"UniversalForward\"", "\"LightMode\"=\"UniversalForwardOnly\"");
                shader = shader.Replace("\"LightMode\"=\"DepthNormals\"", "\"LightMode\"=\"DepthNormalsOnly\"");
                shader = shader.Replace("\"LightMode\"=\"UniversalGBuffer\"", "\"LightMode\"=\"GrassUnusedGBuffer\"");
                shader = shader.Replace("HLSLINCLUDE", "HLSLINCLUDE\n\t\t#define GRASS_ECHO_WASH_PATCH 1\n\t\t#include \"Assets/Game/Systems/Echolocation/Shaders/GrassEchoWash.hlsl\"");
                shader = shader.Replace("_Smoothness(\"Smoothness\"",
                    "[Header(Grass Echo Wash)] _GrassEchoStrength(\"Wash Strength\", Range(0, 1)) = 0\n" +
                    "\t\t_GrassEchoFadeSeconds(\"Wash Fade Seconds\", Range(0.1, 4)) = 1.5\n" +
                    "\t\t_GrassEchoPermanentStrength(\"World Echo Warmth\", Range(0, 0.3)) = 0.08\n\t\t_Smoothness(\"Smoothness\"");
                shader = shader.Replace("CBUFFER_START(UnityPerMaterial)", "CBUFFER_START(UnityPerMaterial)\n\t\t\tfloat _GrassEchoStrength;\n\t\t\tfloat _GrassEchoFadeSeconds;\n\t\t\tfloat _GrassEchoPermanentStrength;");
                int forwardStart = shader.IndexOf("Name \"Forward\"", StringComparison.Ordinal);
                int insertAt = shader.IndexOf("#ifdef ASE_FINAL_COLOR_ALPHA_MULTIPLY", forwardStart, StringComparison.Ordinal);
                if (forwardStart < 0 || insertAt < 0) throw new InvalidOperationException("Cannot locate the forward grass color output.");
                shader = shader.Insert(insertAt,
                    "color.rgb = ApplyGrassEchoWash(color.rgb, WorldPosition, _GrassEchoStrength, _GrassEchoFadeSeconds, _GrassEchoPermanentStrength);\n\n\t\t\t\t");
            }
            shader = RepairDepthNormals(shader);
            string patched = shader + source.Substring(graphStart);
            if (patched != source) File.WriteAllText(ShaderPath, patched);
            AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceSynchronousImport);
        }

        // The vendor's normals pass is an unconnected graph stub: solid alpha and
        // no wind. Once used by deferred rendering it must match the visible mesh.
        private static string RepairDepthNormals(string shader)
        {
            int start = shader.IndexOf("Name \"DepthNormals\"", StringComparison.Ordinal);
            int end = shader.IndexOf("ENDHLSL", start, StringComparison.Ordinal);
            string pass = shader.Substring(start, end - start);
            if (pass.Contains("GRASS_DEPTH_NORMALS_REPAIRED")) return shader;
            int forwardStart = shader.IndexOf("Name \"Forward\"", StringComparison.Ordinal);
            string forward = shader.Substring(forwardStart, shader.IndexOf("ENDHLSL", forwardStart, StringComparison.Ordinal) - forwardStart);
            int noiseStart = forward.IndexOf("float3 mod2D289", StringComparison.Ordinal);
            int vertexStart = forward.IndexOf("VertexOutput VertexFunction", StringComparison.Ordinal);
            if (noiseStart < 0 || vertexStart < noiseStart)
                throw new InvalidOperationException("Cannot locate the grass wind helpers.");
            string noise = forward.Substring(noiseStart, vertexStart - noiseStart);
            int windStart = forward.IndexOf("float simplePerlin2D308", StringComparison.Ordinal);
            int windEnd = forward.IndexOf("float4 LOCALWIND353 = staticSwitch320;", windStart, StringComparison.Ordinal)
                + "float4 LOCALWIND353 = staticSwitch320;".Length;
            string wind = forward.Substring(windStart, windEnd - windStart);
            pass = pass.Replace("#pragma vertex vert", "#define GRASS_DEPTH_NORMALS_REPAIRED 1\n\t\t\t#pragma shader_feature_local _CUSTOMWIND_ON\n\t\t\t#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT\n\t\t\t#pragma vertex vert");
            // Applies to both VertexInput and optional tessellation control data.
            pass = pass.Replace("float4 ase_tangent : TANGENT;", "float4 ase_tangent : TANGENT;\n\t\t\t\tfloat4 texcoord : TEXCOORD0;");
            pass = pass.Replace("float4 worldTangent : TEXCOORD3;", "float4 worldTangent : TEXCOORD3;\n\t\t\t\tfloat2 grassUV : TEXCOORD4;");
            pass = pass.Replace("VertexOutput VertexFunction", "sampler2D _BaseTexture;\n\t\t\t" + noise + "VertexOutput VertexFunction");
            pass = pass.Replace("float3 vertexValue = defaultVertexValue;", wind + "\n\t\t\t\tfloat3 vertexValue = LOCALWIND353.xyz;\n\t\t\t\to.grassUV = v.texcoord.xy;");
            pass = pass.Replace("o.ase_tangent = v.ase_tangent;", "o.ase_tangent = v.ase_tangent;\n\t\t\t\to.texcoord = v.texcoord;");
            pass = pass.Replace("VertexInput o = (VertexInput) 0;", "VertexInput o = (VertexInput) 0;\n\t\t\t\to.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;");
            pass = pass.Replace("float Alpha = 1;", "float Alpha = 1.0 - step(tex2D(_BaseTexture, IN.grassUV).a, 1.0 - _LeavesThickness);");
            pass = pass.Replace("float AlphaClipThreshold = 0.5;", "float AlphaClipThreshold = 0.1;");
            return shader.Substring(0, start) + pass + shader.Substring(end);
        }
    }
}
