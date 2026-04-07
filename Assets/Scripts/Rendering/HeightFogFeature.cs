using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class HeightFogFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class HeightFogSettings
    {
        public Color fogColor             = new Color(0.45f, 0.47f, 0.50f, 1f);
        [Range(0f, 1f)]
        public float heightFogDensity     = 0.6f;
        public float fogHeightStart       = -1f;
        public float fogHeightEnd         = 12f;
        [Range(0f, 0.01f)]
        public float distanceFogDensity   = 0.0015f;
    }

    public HeightFogSettings settings = new HeightFogSettings();

    private HeightFogPass _pass;
    private Material      _material;

    public override void Create()
    {
        var shader = Shader.Find("Custom/HeightFog");
        if (shader == null)
        {
            Debug.LogError("[HeightFog] Shader 'Custom/HeightFog' not found.");
            return;
        }
        _material = CoreUtils.CreateEngineMaterial(shader);
        _pass = new HeightFogPass(_material)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null || _pass == null) return;
        _pass.UpdateSettings(settings);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
    }

    // ── Pass ──────────────────────────────────────────────────────────────────
    class HeightFogPass : ScriptableRenderPass
    {
        static readonly int s_FogColor    = Shader.PropertyToID("_HeightFogColor");
        static readonly int s_FogDensity  = Shader.PropertyToID("_HeightFogDensity");
        static readonly int s_FogStart    = Shader.PropertyToID("_HeightFogStart");
        static readonly int s_FogEnd      = Shader.PropertyToID("_HeightFogEnd");
        static readonly int s_DistDensity = Shader.PropertyToID("_DistanceFogDensity");

        readonly Material _mat;

        class PassData
        {
            public Material       material;
            public TextureHandle  source;
        }

        public HeightFogPass(Material mat)
        {
            _mat = mat;
        }

        public void UpdateSettings(HeightFogSettings s)
        {
            _mat.SetColor(s_FogColor,    s.fogColor);
            _mat.SetFloat(s_FogDensity,  s.heightFogDensity);
            _mat.SetFloat(s_FogStart,    s.fogHeightStart);
            _mat.SetFloat(s_FogEnd,      s.fogHeightEnd);
            _mat.SetFloat(s_DistDensity, s.distanceFogDensity);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            var source = resourceData.activeColorTexture;

            // Crea texture temporanea con stesse dimensioni del colore attivo
            var tempDesc = renderGraph.GetTextureDesc(source);
            tempDesc.name        = "_HeightFogTemp";
            tempDesc.clearBuffer = false;
            var temp = renderGraph.CreateTexture(tempDesc);

            // Pass 1: source → temp con shader HeightFog
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("HeightFog", out var passData))
            {
                passData.material = _mat;
                passData.source   = source;

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(temp, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // Pass 2: temp → source (copy back)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("HeightFog_CopyBack", out var passData))
            {
                passData.source = temp;

                builder.UseTexture(temp, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }
    }
}
