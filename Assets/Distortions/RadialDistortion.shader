Shader "Hidden/RadialDistortion"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    
    // Shader code pasted into all further CGPROGRAM blocks
    CGINCLUDE
    #include "UnityCG.cginc"

    struct appdata
    {
       float4 vertex : POSITION;
       float2 uv : TEXCOORD0;
    };

    

    struct v2f
    {
        float2 uv : TEXCOORD0;
        float4 vertex : SV_POSITION;
    };

    sampler2D _MainTex;
    
    float _magn;
    float _radial;
    float _asym;
    float _XScaling;
    float _YScaling;
    float _XShift;
    float _YShift;
    float4 _MainTex_ST;
    
    fixed4 frag (v2f input) : SV_Target
    {
        float2 uv = input.uv;
        float2 uv_distorted;
		float2 xy = (uv - 0.5) * 2;
		// get the vertical and horizontal shift of the projection matrix to correct for the shifted center of the texture
		float x_shift = unity_CameraProjection[0][2]; 
		float y_shift = unity_CameraProjection[1][2];
		
        xy.x = xy.x + x_shift;
		xy.y = xy.y + y_shift;
        
        float r_sq = xy.x* xy.x + (xy.y - _asym) * (xy.y - _asym);
        
        //xd = (_magn + _radial * r) * x;
        //yd = (_magn + _radial * r) * y;

        uv_distorted.x =  1 / (_magn + _radial * r_sq) * xy.x;
        uv_distorted.y =  1/(_magn + _radial * r_sq) * xy.y;
        
        // reverse the shift and scaling back to [0,1]
		uv_distorted.x = uv_distorted.x - x_shift;
		uv_distorted.y = uv_distorted.y - y_shift;
		uv_distorted = uv_distorted / 2 + 0.5;

        //UnityStereoScreenSpaceUVAdjust(uv_distorted, _MainTex_ST);
        return tex2D(_MainTex, uv_distorted);
    }
    
    v2f vert (appdata v)
    {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        return o;
    }
    ENDCG

    SubShader
    {
        Pass
        {
            // No culling or depth
            Cull Off ZWrite Off ZTest Always
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
    }
}