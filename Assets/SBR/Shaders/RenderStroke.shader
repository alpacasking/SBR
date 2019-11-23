Shader "Hidden/RenderStroke"
{

    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        Blendop Min
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom

            #include "UnityCG.cginc"

            struct strokeData {
                float4 pos;
                float4 color;
                float4 eigens;
            };

            struct v2g
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR0;
                float4 eigens: COLOR1;
            };
            
            struct g2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR0;
                float2 uv : TEXCOORD0;
            };

            StructuredBuffer<strokeData> inputBuffer;
            sampler2D _MainTex;
            float _StrokeSize;
            float _StrokeRatio;

            v2g vert (uint id : SV_VertexID)
            {
                v2g o;
                o.color = inputBuffer[id].color;
                float4 tempPos = inputBuffer[id].pos;
                tempPos.x = 2.0*tempPos.x/_ScreenParams.x - 1.0;
                tempPos.y = 2.0*(_ScreenParams.y-tempPos.y)/_ScreenParams.y - 1.0;

                o.pos = tempPos;
                o.eigens = inputBuffer[id].eigens;
                return o;
            }

            [maxvertexcount(4)]
            void geom (point v2g input[1], inout TriangleStream<g2f> outStream)
            {
                float2x2 rotateMatrix;
                rotateMatrix[0][0] = input[0].eigens.x;
                rotateMatrix[0][1] = -input[0].eigens.y;
                rotateMatrix[1][0] = input[0].eigens.y;
                rotateMatrix[1][1] = input[0].eigens.x;
                
                float4 pos = input[0].pos;
                float ratio = _ScreenParams.x / _ScreenParams.y;
                float dx = _StrokeSize*input[0].eigens.z;
                float dy = _StrokeSize*input[0].eigens.z*_StrokeRatio;
                g2f output;
                output.color=input[0].color;
                float2 temp = mul(rotateMatrix,float2(-dx, dy) );
                temp.y= temp.y*ratio;
                output.pos = float4(temp+pos.xy,pos.z,pos.w);
                output.uv = float2(0,0);
                outStream.Append(output);

                temp = mul(rotateMatrix,float2(dx, dy) );
                temp.y= temp.y*ratio;
                output.pos = float4(temp+pos.xy,pos.z,pos.w);
                output.uv = float2(1,0);
                outStream.Append(output);

                temp = mul(rotateMatrix, float2(-dx, -dy));
                temp.y= temp.y*ratio;
                output.pos = float4(temp+pos.xy,pos.z,pos.w);
                output.uv = float2(0,1);
                outStream.Append(output);

                temp = mul( rotateMatrix,float2(dx, -dy));
                temp.y= temp.y*ratio;
                output.pos = float4(temp+pos.xy,pos.z,pos.w);
                output.uv = float2(1,1);
                outStream.Append(output);

                outStream.RestartStrip();
            }

            float4 frag (g2f i) : COLOR
            {
                float4 sc = tex2D(_MainTex,i.uv);
                float3 tempColor = i.color.rgb;
                return float4(lerp(1,tempColor.r,1-sc.r),lerp(1,tempColor.g,1-sc.r),lerp(1,tempColor.b,1-sc.r),1-sc.r); 
            }
            ENDCG
        }
    }
}
