// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Hidden/DotShader"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)

    }

    SubShader
    {
        Tags { "Queue"="Overlay" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "UnityCG.cginc"



            struct v2f {
                half3 color : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            float _Size;
            fixed4 _Color;
            float _Alpha;
            float3 _DistortionParam;
            float _Cutoff;
            float3 _NewPos;
            int _RandDot;
            uniform sampler2D _DisplaceTex;
            
            //buffers
            uniform StructuredBuffer<float4> SphereLocations;
            StructuredBuffer<int> Triangles;
            StructuredBuffer<float3> Positions;

            float2 distortPosition(float2 xy)
            {   
               if (any(abs(xy) > _Cutoff))
                {
                    return float2(100,100);
                }

                float r = sqrt(pow(xy.x,2) + pow(xy.y-_DistortionParam.z,2));
                float theta = atan(r);
                float delta_rad = _DistortionParam.y * pow(theta, 3.0);
                float center = pow(abs(atan(_DistortionParam.z)),3) * _DistortionParam.y * _DistortionParam.z;

                float2 xy_dist = _DistortionParam.x * xy + delta_rad * float2(xy.x , xy.y - _DistortionParam.z) + float2(0,center);
                
                return xy_dist;
            }

            //the vertex shader function
            v2f vert(uint vertex_id: SV_VertexID, uint instance_id: SV_InstanceID)

            {
                v2f o;
                //get vertex position
                int positionIndex = Triangles[vertex_id];
                float3 position = Positions[positionIndex];
                // scale mesh size with size factor and vertical screen resolution (then _Size scales roughtly pixel size)
                position *= _Size ;
                // adjust the aspect ratio of the mesh to the aspect ratio of the screen
                position.x *= _ScreenParams.y / _ScreenParams.x;
                // project sphere location to clip space

                float4 location = mul(UNITY_MATRIX_V, float4(SphereLocations[instance_id].xyz, 1));
                // project to image plane
                location.xy = location.xy/location.z;
                // distort the xy position
                location.xy = distortPosition(location.xy);
                // reproject
                location.xy = location.xy*location.z;

                location = mul(UNITY_MATRIX_P, location);

                // perform projective division (we have no idea why we need *2)
                location.xy = location.xy * 2 / location.w;
                location.z = 1;
                // set w=1, so that the automatic projective divisio after the vert shader does not change the position anymore
                location.w = sign(location.w);

                // return sphere center (location) + the vertex position (position) with position.w = 1
                o.pos = float4(position,1) + location;
                o.color = _Color.rgb * SphereLocations[instance_id].w;

                return o;
            }
            

            fixed4 frag(v2f i) : SV_Target
            
            {
                fixed4 c = 0;
                c.rgb = i.color;
                c.a = 1 * _Alpha;
                return c;
            }
            ENDCG
        }
    }
}
