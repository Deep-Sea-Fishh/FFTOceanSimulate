Shader "FFTOcean/Ocean"
{
    Properties
    {
        //表面颜色
        _OceanColorShallow("Ocean Color Shallow", Color) = (1, 1, 1, 1)
        //水底颜色
        _OceanColorDeep("Ocean Color Deep", Color) = (1, 1, 1, 1)
        //泡沫颜色
        _BubblesColor("Bubbles Color", Color) = (1, 1, 1, 1)
        //镜面反射颜色
        _Specular("Specular", Color) = (1, 1, 1, 1)
        //光泽度
        _Gloss("Gloss", Range(8.0, 256)) = 20
        //菲涅尔缩放因子
        _FresnelScale("Fresnel Scale", Range(0, 1)) = 0.5
        //位移贴图
        _Displace("Displace", 2D) = "black" { }
        //法线贴图
        _Normal("Normal", 2D) = "black" { }
        //泡沫贴图
        _Bubbles("Bubbles", 2D) = "black" { }
    }
        SubShader
    {
        Tags { "RenderType" = "Opaque" "LightMode" = "ForwardBase" }
        LOD 100

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex: POSITION;
                float2 uv: TEXCOORD0;
            };

            struct v2f
            {
                float4 pos: SV_POSITION;
                float2 uv: TEXCOORD0;
                float3 worldPos: TEXCOORD1;
            };

            float4 _OceanColorShallow;
            float4 _OceanColorDeep;
            float4 _BubblesColor;
            float4 _Specular;
            float _Gloss;
            float _FresnelScale;
            sampler2D _Displace;
            sampler2D _Normal;
            sampler2D _Bubbles;
            float4 _Displace_ST;

            v2f vert(appdata v)
            {
                v2f o;
                o.uv = TRANSFORM_TEX(v.uv, _Displace);
                float4 displcae = tex2Dlod(_Displace, float4(o.uv, 0, 0));
                v.vertex += float4(displcae.xyz, 0);
                o.pos = UnityObjectToClipPos(v.vertex);

                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                //法线和泡沫
                float3 normal = UnityObjectToWorldNormal(tex2D(_Normal, i.uv).rgb);
                float bubbles = tex2D(_Bubbles, i.uv).r;

                //获取光照向量，观察向量，反射向量
                float3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
                float3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                float3 reflectDir = reflect(-viewDir, normal);

                //对天空盒进行采样
                float4 rgbm = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflectDir, 0);
                float3 sky = DecodeHDR(rgbm, unity_SpecCube0_HDR);

                //计算菲涅尔系数
                float fresnel = saturate(_FresnelScale + (1 - _FresnelScale) * pow(1 - dot(normal, viewDir), 5));
                //海面颜色
                float facing = saturate(dot(viewDir, normal));
                float3 oceanColor = lerp(_OceanColorShallow, _OceanColorDeep, facing);

                //环境光
                float3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb;
                //泡沫漫反射
                float3 bubblesDiffuse = _BubblesColor.rbg * _LightColor0.rgb * saturate(dot(lightDir, normal));
                //海面漫反射
                float3 oceanDiffuse = oceanColor * _LightColor0.rgb * saturate(dot(lightDir, normal));
                //半程向量
                float3 halfDir = normalize(lightDir + viewDir);
                //高光
                float3 specular = _LightColor0.rgb * _Specular.rgb * pow(max(0, dot(normal, halfDir)), _Gloss);
                //漫反射
                float3 diffuse = lerp(oceanDiffuse, bubblesDiffuse, bubbles);
                //最终颜色  blinn-phong光照模型
                float3 col = ambient + lerp(diffuse, sky, fresnel) + specular;

                return float4(col, 1);
            }
            ENDCG

        }
    }
}
