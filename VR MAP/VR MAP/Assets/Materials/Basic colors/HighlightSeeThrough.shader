Shader "Custom/HighlightSeeThrough"
{
    Properties
    {
        _Color ("Color", Color) = (0, 1, 1, 0.8)
        _EmissionColor ("Emission Color", Color) = (0, 1, 1, 1)
        _EmissionStrength ("Emission Strength", Float) = 3.0
        _WallAlpha ("Transparence à travers murs", Range(0, 1)) = 0.3
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        
        // ✅ Premier pass : Rendu derrière les objets (visible à travers les murs)
        Pass
        {
            Name "BEHIND_WALLS"
            ZWrite Off
            ZTest Greater // Dessine seulement quand quelque chose est devant
            Blend SrcAlpha OneMinusSrcAlpha // ✅ MODIFIÉ : Blend plus visible
            Cull Off // ✅ MODIFIÉ : Voir des deux côtés
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
            };
            
            fixed4 _Color;
            fixed4 _EmissionColor;
            float _EmissionStrength;
            float _WallAlpha;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // ✅ Calculer l'émission
                fixed4 col = _Color;
                col.rgb += _EmissionColor.rgb * _EmissionStrength;
                
                // ✅ Transparence réduite quand derrière les murs
                col.a = _WallAlpha;
                
                // ✅ S'assurer que la couleur est visible
                col.rgb = saturate(col.rgb);
                
                return col;
            }
            ENDCG
        }
        
        // ✅ Deuxième pass : Rendu normal (visible normalement)
        Pass
        {
            Name "NORMAL_VIEW"
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha // ✅ MODIFIÉ
            Cull Off // ✅ MODIFIÉ
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
            };
            
            fixed4 _Color;
            fixed4 _EmissionColor;
            float _EmissionStrength;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // ✅ Couleur pleine quand visible normalement
                fixed4 col = _Color;
                col.rgb += _EmissionColor.rgb * _EmissionStrength;
                
                // ✅ Alpha complet quand visible normalement
                col.a = _Color.a;
                
                // ✅ S'assurer que la couleur est visible
                col.rgb = saturate(col.rgb);
                
                return col;
            }
            ENDCG
        }
    }
    
    // ✅ Fallback pour compatibilité
    FallBack "Transparent/Diffuse"
}
