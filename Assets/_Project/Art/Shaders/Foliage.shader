// GardenWise - leaves, grass and flower petals (built-in render pipeline)
// alpha cut-out, visible from both sides, and a gentle sway in the wind (more at the top than at the base)
// The URP version is FoliageURP.shader; ThirdPartyImport picks the right one for the active pipeline.
Shader "GardenWise/Foliage"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo (RGB), cut-out (A)", 2D) = "white" {}
        _Cutoff ("Alpha cut-off", Range(0, 1)) = 0.4
        _WindStrength ("Wind strength (m)", Range(0, 0.3)) = 0.06
        _WindSpeed ("Wind speed", Range(0, 5)) = 1.6
    }

    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        Cull Off

        CGPROGRAM
        #pragma surface surf Lambert alphatest:_Cutoff addshadow vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        float _WindStrength;
        float _WindSpeed;

        struct Input
        {
            float2 uv_MainTex;
            fixed facing : VFACE;
        };

        // height above the object's base (above the ground once static batching has merged everything)
        void vert (inout appdata_full v)
        {
            float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
            float3 origin = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
            float height = saturate((world.y - origin.y) / 3.0);
            float phase = _Time.y * _WindSpeed + world.x * 0.35 + world.z * 0.25;
            float3 offset = float3(sin(phase), 0, cos(phase * 0.8)) * _WindStrength * height;
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, offset);
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            // the back of a leaf is lit like its front
            o.Normal = IN.facing > 0 ? float3(0, 0, 1) : float3(0, 0, -1);
        }
        ENDCG
    }

    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}
