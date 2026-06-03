Shader "Hidden/CircularBorder"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _BorderRadius;
            float _EdgeSoftness;

            fixed4 frag(v2f_img i) : SV_Target
            {
                // sample the original rendered image
                fixed4 col = tex2D(_MainTex, i.uv);

                // compute distance from screen center, normalized so the
                // shortest screen-half is 1.0 (so circles look round, not oval)
                float2 centeredUV = i.uv - 0.5;
                // account for aspect ratio so the mask is circular
                centeredUV.x *= _ScreenParams.x / _ScreenParams.y;
                float dist = length(centeredUV) * 2.0;

                // smooth falloff at the edge of the visible circle
                float mask = 1.0 - smoothstep(_BorderRadius - _EdgeSoftness,
                                              _BorderRadius + _EdgeSoftness,
                                              dist);

                // multiply image with the mask -> outside the circle becomes black
                col.rgb *= mask;

                return col;
            }
            ENDCG
        }
    }
}
