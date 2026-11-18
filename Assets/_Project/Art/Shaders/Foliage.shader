// GardenWise - leaves, grass and flower petals: alpha cut-out and visible from both sides
// (the Standard shader only draws the front of a leaf card, so trees looked empty from behind)
Shader "GardenWise/Foliage"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo (RGB), cut-out (A)", 2D) = "white" {}
        _Cutoff ("Alpha cut-off", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        Cull Off

        CGPROGRAM
        #pragma surface surf Lambert alphatest:_Cutoff addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;

        struct Input
        {
            float2 uv_MainTex;
            fixed facing : VFACE;
        };

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
