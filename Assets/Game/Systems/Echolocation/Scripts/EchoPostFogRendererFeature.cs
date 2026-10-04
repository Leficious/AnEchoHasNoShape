using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace AnEchoHasNoShape.Echolocation
{
    /// <summary>
    /// Draws echo-only geometry after transparent objects and full-screen fog.
    /// AERO runs at event 500 and Phase Veil at 501, so this pass uses 502.
    /// </summary>
    public sealed class EchoPostFogRendererFeature : ScriptableRendererFeature
    {
        private sealed class EchoPostFogPass : ScriptableRenderPass
        {
            private static readonly ShaderTagId EchoShaderTag = new ShaderTagId("EchoRevealPostFog");
            private static readonly ShaderTagId ArchitectureDepthTag = new ShaderTagId("EchoArchitectureDepth");
            private static readonly ProfilingSampler ProfilingSampler = new ProfilingSampler("Echo Post Fog");
            private readonly FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all);

            private sealed class PassData
            {
                public RendererListHandle rendererList;
                public RendererListHandle depthList;
            }

            public EchoPostFogPass()
            {
                renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingTransparents + 2);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();

                var shaderTags = new List<ShaderTagId> { EchoShaderTag };
                DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
                    shaderTags,
                    renderingData,
                    cameraData,
                    lightData,
                    SortingCriteria.CommonTransparent);

                RendererListParams rendererListParams = new RendererListParams(
                    renderingData.cullResults,
                    drawingSettings,
                    filteringSettings);

                DrawingSettings depthSettings = RenderingUtils.CreateDrawingSettings(
                    new List<ShaderTagId> { ArchitectureDepthTag },
                    renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                RendererListParams depthListParams = new RendererListParams(
                    renderingData.cullResults, depthSettings, filteringSettings);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "Echo Post Fog",
                    out PassData passData,
                    ProfilingSampler))
                {
                    passData.rendererList = renderGraph.CreateRendererList(rendererListParams);
                    passData.depthList = renderGraph.CreateRendererList(depthListParams);
                    builder.UseRendererList(passData.depthList);
                    builder.UseRendererList(passData.rendererList);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.depthList);
                        context.cmd.DrawRendererList(data.rendererList);
                    });
                }
            }
        }

        private EchoPostFogPass pass;

        public override void Create()
        {
            pass = new EchoPostFogPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass != null)
            {
                renderer.EnqueuePass(pass);
            }
        }
    }
}
