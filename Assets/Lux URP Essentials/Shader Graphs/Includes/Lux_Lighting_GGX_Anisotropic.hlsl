// We have to mute URP's default decal implementation as it would tweak our albedo - which needs to be pure black
// as otherwise default lighting would no be stripped by the shader compiler.
// This mean that decals will use our custom lighting as well.

#ifdef _DBUFFER
    #undef _DBUFFER
    #define _CUSTOMDBUFFER
#endif


#if !defined(SHADERGRAPH_PREVIEW) || defined(LIGHTWEIGHT_LIGHTING_INCLUDED)

//  As we do not have access to the vertex lights we will make the shader always sample add lights per pixel
    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
        #undef _ADDITIONAL_LIGHTS_VERTEX
        #define _ADDITIONAL_LIGHTS
    #endif

    #if defined(LIGHTWEIGHT_LIGHTING_INCLUDED) || defined(UNIVERSAL_LIGHTING_INCLUDED)

        struct AdditionalData {
            half3   tangentWS;
            half3   bitangentWS;
            float   partLambdaV;
            half    roughnessT;
            half    roughnessB;
            half3   anisoReflectionNormal;
        };

        half3 DirectBDRF_LuxGGXAniso(BRDFData brdfData, AdditionalData addData, half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS, half NdotL)
        {
        #ifndef _SPECULARHIGHLIGHTS_OFF
            float3 lightDirectionWSFloat3 = float3(lightDirectionWS);
            float3 halfDir = SafeNormalize(lightDirectionWSFloat3 + float3(viewDirectionWS));
            
            float NoH = saturate(dot(float3(normalWS), halfDir));
            half LoH = half(saturate(dot(lightDirectionWSFloat3, halfDir)));
            
            half NdotV = saturate(dot(normalWS, viewDirectionWS ));

        //  GGX Aniso
            float3 tangentWS = float3(addData.tangentWS);
            float3 bitangentWS = float3(addData.bitangentWS);

            float TdotH = dot(tangentWS, halfDir);
            float TdotL = dot(tangentWS, lightDirectionWSFloat3);
            float BdotH = dot(bitangentWS, halfDir);
            float BdotL = dot(bitangentWS, lightDirectionWSFloat3);

            half3 F = F_Schlick(brdfData.specular, LoH); // 1.91: was float3

            //float TdotV = dot(tangentWS, viewDirectionWS);
            //float BdotV = dot(bitangentWS, viewDirectionWS);

            float DV = DV_SmithJointGGXAniso(
                TdotH, BdotH, NoH, NdotV, TdotL, BdotL, NdotL,
                addData.roughnessT, addData.roughnessB, addData.partLambdaV
            );
            half3 specularLighting = F * DV;

            return specularLighting + brdfData.diffuse;
        #else
            return brdfData.diffuse;
        #endif
        }

        half3 LightingPhysicallyBased_LuxGGXAniso(BRDFData brdfData, AdditionalData addData, half3 lightColor, half3 lightDirectionWS, half lightAttenuation, half3 normalWS, half3 viewDirectionWS, half NdotL)
        {
            half3 radiance = lightColor * (lightAttenuation * NdotL);
            return DirectBDRF_LuxGGXAniso(brdfData, addData, normalWS, lightDirectionWS, viewDirectionWS, NdotL) * radiance;
        }

        half3 LightingPhysicallyBased_LuxGGXAniso(BRDFData brdfData, AdditionalData addData, Light light, half3 normalWS, half3 viewDirectionWS, half NdotL)
        {
            return LightingPhysicallyBased_LuxGGXAniso(brdfData, addData, light.color, light.direction, light.distanceAttenuation * light.shadowAttenuation, normalWS, viewDirectionWS, NdotL);
        }

    //  As we need both normals here - otherwise kept in sync with latest URP function
        half3 GlobalIllumination_LuxAniso(BRDFData brdfData, BRDFData brdfDataClearCoat, float clearCoatMask,
            half3 bakedGI, half occlusion, float3 positionWS,
            half3 anisoReflectionNormal,
            half3 normalWS, half3 viewDirectionWS, float2 normalizedScreenSpaceUV)
        {
            half3 reflectVector = reflect(-viewDirectionWS, anisoReflectionNormal);
            half NoV = saturate(dot(normalWS, viewDirectionWS));
            half fresnelTerm = Pow4(1.0 - NoV);

            half3 indirectDiffuse = bakedGI;
            half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, positionWS, brdfData.perceptualRoughness, 1.0h, normalizedScreenSpaceUV);

            half3 color = EnvironmentBRDF(brdfData, indirectDiffuse, indirectSpecular, fresnelTerm);

            if (IsOnlyAOLightingFeatureEnabled())
            {
                color = half3(1,1,1); // "Base white" for AO debug lighting mode
            }

        #if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
            half3 coatIndirectSpecular = GlossyEnvironmentReflection(reflectVector, positionWS, brdfDataClearCoat.perceptualRoughness, 1.0h, normalizedScreenSpaceUV);
            // TODO: "grazing term" causes problems on full roughness
            half3 coatColor = EnvironmentBRDFClearCoat(brdfDataClearCoat, clearCoatMask, coatIndirectSpecular, fresnelTerm);

            // Blend with base layer using khronos glTF recommended way using NoV
            // Smooth surface & "ambiguous" lighting
            // NOTE: fresnelTerm (above) is pow4 instead of pow5, but should be ok as blend weight.
            half coatFresnel = kDielectricSpec.x + kDielectricSpec.a * fresnelTerm;
            return (color * (1.0 - coatFresnel * clearCoatMask) + coatColor) * occlusion;
        #else
            return color * occlusion;
        #endif
        }

    #endif
#endif


void Lighting_half(

//  Base inputs
    float3 positionWS,
    float4 positionSP,
    half3 viewDirectionWS,

//  Normal inputs    
    half3 normalWS,
    half3 tangentWS,
    half3 bitangentWS,
    bool enableNormalMapping,
    half3 normalTS,

//  Surface description
    half3 albedo,
    half metallic,
    half3 specular,
    half smoothness,
    half occlusion,
    half alpha,

//  Lighting specific inputs

    half anisotropy,

    bool enableTransmission,
    half transmissionStrength,
    half transmissionPower,
    half transmissionDistortion,
    half transmissionShadowstrength,

//  Lightmapping
    float2 lightMapUV,
    float2 dynamicLightMapUV,

//  Final lit color
    out half3 MetaAlbedo,
    out half3 FinalLighting,
    out half3 MetaSpecular,
    out half  MetaSmoothness,
    out half  MetaOcclusion,
    out half3 MetaNormal
)
{


#if defined(SHADERGRAPH_PREVIEW) || ( !defined(LIGHTWEIGHT_LIGHTING_INCLUDED) && !defined(UNIVERSAL_LIGHTING_INCLUDED) )
    FinalLighting = albedo;
    MetaAlbedo = half3(0,0,0);
    MetaSpecular = half3(0,0,0);
    MetaSmoothness = 0;
    MetaOcclusion = 0;
    MetaNormal = half3(0,0,1);
#else


//  /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//  Real Lighting

    if (enableNormalMapping) {
        normalWS = TransformTangentToWorld(normalTS, half3x3(tangentWS.xyz, bitangentWS.xyz, normalWS.xyz));
    }
    normalWS = NormalizeNormalPerPixel(normalWS);

//  GI Lighting
//  We are using the base normalWS here. So decal normals will not affect GI like in all other shaders.
    half3 bakedGI;
    #ifdef LIGHTMAP_ON
        lightMapUV = lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
        #if defined(DYNAMICLIGHTMAP_ON)
            dynamicLightMapUV = dynamicLightMapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
            bakedGI = SAMPLE_GI(lightMapUV, dynamicLightMapUV, half3(0,0,0), normalWS);
        #else
            bakedGI = SAMPLE_GI(lightMapUV, half3(0,0,0), normalWS);
        #endif
    #else
        bakedGI = SampleSH(normalWS); 
    #endif

//  Fill standard URP structs so we can use the built in functions
    InputData inputData = (InputData)0;
    {
        inputData.positionWS = positionWS;
        inputData.normalWS = normalWS;
        inputData.viewDirectionWS = viewDirectionWS;
        inputData.bakedGI = bakedGI;
        #if _MAIN_LIGHT_SHADOWS_SCREEN
        //  Here we need raw
            inputData.shadowCoord = positionSP;
        #else
            inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
        #endif
        //  Apply perspective division
        inputData.normalizedScreenSpaceUV = positionSP.xy * rcp(positionSP.w);
        inputData.shadowMask = SAMPLE_SHADOWMASK(lightMapUV);
    }
    SurfaceData surfaceData = (SurfaceData)0;
    {
        surfaceData.alpha = alpha;
        surfaceData.albedo = albedo;
        surfaceData.metallic = metallic;
        surfaceData.specular = specular;
        surfaceData.smoothness = smoothness;
        surfaceData.occlusion = occlusion;
        surfaceData.normalTS = normalTS;   
    }
//  END: structs

//  Decals
    #if defined(_CUSTOMDBUFFER)
        float2 positionCS = inputData.normalizedScreenSpaceUV * _ScreenSize.xy;
        ApplyDecalToSurfaceData(float4(positionCS, 0, 0), surfaceData, inputData);
    #endif


//  From here on we rely on surfaceData and inputData only! (except debug which outputs the original values)

    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);

//  Debugging
    #if defined(DEBUG_DISPLAY)
        half4 debugColor;
        if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
        {
            //return debugColor;
            FinalLighting = debugColor.rgb;
            MetaAlbedo = debugColor.rgb;
            MetaSpecular = specular;
            MetaSmoothness = smoothness;
            MetaOcclusion = occlusion;
            MetaNormal = normalTS;
        }
    #else

    //  Do not apply energy conservation - we have to use surfaceData as it may contain decal data.
        brdfData.diffuse = surfaceData.albedo;
        brdfData.specular = surfaceData.specular;

        AdditionalData addData;
    //  Adjust tangentWS in case normal mapping is active
        if (enableNormalMapping) {   
            tangentWS = Orthonormalize(tangentWS, inputData.normalWS);
        }           
        addData.tangentWS = tangentWS;
        addData.bitangentWS = cross(inputData.normalWS, tangentWS);

    //  GGX Aniso
        addData.roughnessT = brdfData.roughness * (1 + anisotropy);
        addData.roughnessB = brdfData.roughness * (1 - anisotropy);

        float TdotV = dot(addData.tangentWS, inputData.viewDirectionWS);
        float BdotV = dot(addData.bitangentWS, inputData.viewDirectionWS);
        float NdotV = dot(inputData.normalWS, inputData.viewDirectionWS);
        addData.partLambdaV = GetSmithJointGGXAnisoPartLambdaV(TdotV, BdotV, NdotV, addData.roughnessT, addData.roughnessB);

    //  Set reflection normal and roughness – derived from GetGGXAnisotropicModifiedNormalAndRoughness
        half3 grainDirWS = (anisotropy >= 0.0) ? addData.bitangentWS : addData.tangentWS;
        half stretch = abs(anisotropy) * saturate(1.5h * sqrt(brdfData.perceptualRoughness));
        addData.anisoReflectionNormal = GetAnisotropicModifiedNormal(grainDirWS, inputData.normalWS, inputData.viewDirectionWS, stretch);
        half iblPerceptualRoughness = brdfData.perceptualRoughness * saturate(1.2 - abs(anisotropy));

    //  Override perceptual roughness for ambient specular reflections
        brdfData.perceptualRoughness = iblPerceptualRoughness;

        half4 shadowMask = CalculateShadowMask(inputData);
        AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
        uint meshRenderingLayers = GetMeshRenderingLayer();

        Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);
        half3 mainLightColor = mainLight.color;

        MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

        LightingData lightingData = CreateLightingData(inputData, surfaceData);

    //  In order to use probe blending and proper AO we have to use the new GlobalIllumination function
        lightingData.giColor = GlobalIllumination_LuxAniso(
            brdfData,
            brdfData,   // brdfDataClearCoat,
            0,          // surfaceData.clearCoatMask
            inputData.bakedGI,
            aoFactor.indirectAmbientOcclusion,
            inputData.positionWS,
            addData.anisoReflectionNormal,
            inputData.normalWS,
            inputData.viewDirectionWS,
            inputData.normalizedScreenSpaceUV
        );

        half NdotL;

    //  Main Light
        #if defined(_LIGHT_LAYERS)
            if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
        #endif
            {
        
                NdotL = saturate(dot(inputData.normalWS, mainLight.direction));
                lightingData.mainLightColor = LightingPhysicallyBased_LuxGGXAniso(brdfData, addData, mainLight, inputData.normalWS, inputData.viewDirectionWS, NdotL);

            //  Transmission
                if (enableTransmission) {
                    half3 transLightDir = mainLight.direction + inputData.normalWS * transmissionDistortion;
                    half transDot = dot( transLightDir, -inputData.viewDirectionWS );
                    transDot = exp2(saturate(transDot) * transmissionPower - transmissionPower);
                    lightingData.mainLightColor += brdfData.diffuse * transDot * (1.0h - NdotL) * mainLightColor * lerp(1.0h, mainLight.shadowAttenuation, transmissionShadowstrength) * transmissionStrength * 4;
                }
            }

        #ifdef _ADDITIONAL_LIGHTS
            uint pixelLightCount = GetAdditionalLightsCount();

            #if USE_FORWARD_PLUS
                for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
                    Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
                #if defined(_LIGHT_LAYERS)
                    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                #endif
                    {
                        half3 lightColor = light.color;
                        NdotL = saturate(dot(inputData.normalWS, light.direction ));
                        lightingData.additionalLightsColor += LightingPhysicallyBased_LuxGGXAniso(brdfData, addData, light, inputData.normalWS, inputData.viewDirectionWS, NdotL);

                    //  Transmission
                        if (enableTransmission) {
                            half3 transLightDir = light.direction + inputData.normalWS * transmissionDistortion;
                            half transDot = dot( transLightDir, -inputData.viewDirectionWS );
                            transDot = exp2(saturate(transDot) * transmissionPower - transmissionPower);
                            lightingData.additionalLightsColor += brdfData.diffuse * transDot * (1.0h - NdotL) * lightColor * lerp(1.0h, light.shadowAttenuation, transmissionShadowstrength) * light.distanceAttenuation * transmissionStrength * 4;
                        }
                    }
                }
            #endif

            LIGHT_LOOP_BEGIN(pixelLightCount)    
                    Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
                #if defined(_LIGHT_LAYERS)
                    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                #endif
                    {
                        half3 lightColor = light.color;
                        NdotL = saturate(dot(inputData.normalWS, light.direction ));
                        lightingData.additionalLightsColor += LightingPhysicallyBased_LuxGGXAniso(brdfData, addData, light, inputData.normalWS, inputData.viewDirectionWS, NdotL);

                    //  Transmission
                        if (enableTransmission) {
                            half3 transLightDir = light.direction + inputData.normalWS * transmissionDistortion;
                            half transDot = dot( transLightDir, -inputData.viewDirectionWS );
                            transDot = exp2(saturate(transDot) * transmissionPower - transmissionPower);
                            lightingData.additionalLightsColor += brdfData.diffuse * transDot * (1.0h - NdotL) * lightColor * lerp(1.0h, light.shadowAttenuation, transmissionShadowstrength) * light.distanceAttenuation * transmissionStrength * 4;
                        }
                    }
            LIGHT_LOOP_END
        #endif

        FinalLighting = CalculateFinalColor(lightingData, surfaceData.alpha).xyz;


    //  Set Albedo for meta pass
        #if defined(LIGHTWEIGHT_META_PASS_INCLUDED) || defined(UNIVERSAL_META_PASS_INCLUDED)
            FinalLighting = half3(0,0,0);
            MetaAlbedo = albedo;
            MetaSpecular = specular;
            MetaSmoothness = 0;
            MetaOcclusion = 0;
            MetaNormal = half3(0,0,1);
        #else
            MetaAlbedo = half3(0,0,0);
            MetaSpecular = half3(0,0,0);
            MetaSmoothness = 0;
            MetaOcclusion = 0;
        //  Needed by DepthNormalOnly pass
            MetaNormal = normalTS;
        #endif

    //  End Real Lighting ----------

    #endif // end debug

#endif
}

// Unity 2019.1. needs a float version

void Lighting_float(

//  Base inputs
    float3 positionWS,
    float4 positionSP,
    half3 viewDirectionWS,

//  Normal inputs    
    half3 normalWS,
    half3 tangentWS,
    half3 bitangentWS,
    bool enableNormalMapping,
    half3 normalTS,

//  Surface description
    half3 albedo,
    half metallic,
    half3 specular,
    half smoothness,
    half occlusion,
    half alpha,

//  Lighting specific inputs

    half anisotropy,

    bool enableTransmission,
    half transmissionStrength,
    half transmissionPower,
    half transmissionDistortion,
    half transmissionShadowstrength,

//  Lightmapping
    float2 lightMapUV,
    float2 dynamicLightMapUV,

//  Final lit color
    out half3 MetaAlbedo,
    out half3 FinalLighting,
    out half3 MetaSpecular,
    out half  MetaSmoothness,
    out half  MetaOcclusion,
    out half3 MetaNormal
)
{
    Lighting_half(
        positionWS, positionSP, viewDirectionWS, normalWS, tangentWS, bitangentWS, enableNormalMapping, normalTS, 
        albedo, metallic, specular, smoothness, occlusion, alpha,
        anisotropy, enableTransmission, transmissionStrength, transmissionPower, transmissionDistortion, transmissionShadowstrength,
        lightMapUV, dynamicLightMapUV, MetaAlbedo, FinalLighting, MetaSpecular, MetaSmoothness, MetaOcclusion, MetaNormal);
}