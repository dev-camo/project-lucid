namespace ProjectLucid.Editor
{
    // Shipped Amplify Color declarations (Assembly-CSharp 1.10.1), taken from the DummyDll ECMA-335
    // tables (.agents/workflows/evidence.py type <Type>, metadata section) in metadata order.
    // Fields and methods are "name:flags" with the flags as lowercase hexadecimal.
    internal sealed class ShippedType
    {
        public readonly string Name;
        public readonly int Flags;
        public readonly string Base;
        public readonly string[] Fields;
        public readonly string[] Methods;

        public ShippedType(string name, int flags, string baseName, string[] fields, string[] methods)
        {
            Name = name;
            Flags = flags;
            Base = baseName;
            Fields = fields;
            Methods = methods;
        }
    }

    internal static class OriginalAmplifyColorShippedTypes
    {
        public static readonly ShippedType[] All =
        {
            new ShippedType("AmplifyColorBase", 0x100001, "UnityEngine.MonoBehaviour",
                new[]
                {
                    "LutSize:8056", "LutWidth:8056", "LutHeight:8056", "DepthCurveLutRange:8051", "Tonemapper:6",
                    "Exposure:6", "LinearWhitePoint:6", "ApplyDithering:6", "QualityLevel:6", "BlendAmount:6",
                    "LutTexture:6", "LutBlendTexture:6", "MaskTexture:6", "UseDepthMask:6", "DepthMaskCurve:6",
                    "UseVolumes:6", "ExitVolumeBlendTime:6", "TriggerVolumeProxy:6", "VolumeCollisionMask:6",
                    "ownerCamera:1", "shaderBase:1", "shaderBlend:1", "shaderBlendCache:1", "shaderMask:1",
                    "shaderMaskBlend:1", "shaderDepthMask:1", "shaderDepthMaskBlend:1", "shaderProcessOnly:1",
                    "blendCacheLut:1", "defaultLut:1", "depthCurveLut:1", "depthCurveColors:1", "colorSpace:1",
                    "qualityLevel:1", "materialBase:1", "materialBlend:1", "materialBlendCache:1",
                    "materialMask:1", "materialMaskBlend:1", "materialDepthMask:1", "materialDepthMaskBlend:1",
                    "materialProcessOnly:1", "blending:1", "blendingTime:1", "blendingTimeCountdown:1",
                    "onFinishBlend:1", "prevDepthMaskCurve:1", "volumesBlending:1", "volumesBlendingTime:1",
                    "volumesBlendingTimeCountdown:1", "volumesLutBlendTexture:1", "volumesBlendAmount:1",
                    "worldLUT:1", "currentVolumeLut:1", "midBlendLUT:1", "blendingFromMidBlend:1",
                    "worldVolumeEffects:1", "currentVolumeEffects:1", "blendVolumeEffects:1", "worldExposure:1",
                    "currentExposure:1", "blendExposure:1", "effectVolumesBlendAdjust:1", "enteredVolumes:1",
                    "actualTriggerProxy:1", "EffectFlags:6", "sharedInstanceID:1", "silentError:1"
                },
                new[]
                {
                    "get_DefaultLut:886", "get_IsBlending:886", "get_effectVolumesBlendAdjusted:881",
                    "get_SharedInstanceID:886", "get_WillItBlend:886", "NewSharedInstanceID:86",
                    "ReportMissingShaders:81", "ReportNotSupported:81", "CheckShader:81", "CheckShaders:81",
                    "CheckSupport:81", "OnEnable:81", "OnDisable:81", "VolumesBlendTo:81", "BlendTo:86",
                    "CheckCamera:81", "Start:81", "Update:81", "EnterVolume:86", "ExitVolume:86",
                    "UpdateVolumes:81", "SetupShader:81", "ReleaseMaterials:81", "CreateDefaultLut:81",
                    "CreateDepthCurveLut:81", "UpdateDepthCurveLut:81", "CheckUpdateDepthCurveLut:81",
                    "CreateHelperTextures:81", "CheckMaterialAndShader:81", "CreateMaterials:81",
                    "SetMaterialKeyword:81", "SafeRelease:81", "ReleaseTextures:81", "ValidateLutDimensions:96",
                    "UpdatePostEffectParams:81", "ComputeShaderPass:81", "OnRenderImage:81", ".ctor:1886"
                }),
            new ShippedType("AmplifyColorEffect", 0x100101, "AmplifyColorBase",
                new string[0],
                new[]
                {
                    ".ctor:1886"
                }),
            new ShippedType("AmplifyColorRenderMask", 0x100001, "AmplifyColorRenderMaskBase",
                new string[0],
                new[]
                {
                    ".ctor:1886"
                }),
            new ShippedType("AmplifyColorRenderMaskBase", 0x100001, "UnityEngine.MonoBehaviour",
                new[]
                {
                    "ClearColor:6", "RenderLayers:6", "DebugMask:6", "referenceCamera:1", "maskCamera:1",
                    "colorEffect:1", "width:1", "height:1", "maskTexture:1", "colorMaskShader:1",
                    "singlePassStereo:1"
                },
                new[]
                {
                    "OnEnable:81", "OnDisable:81", "DestroyCamera:81", "DestroyRenderTextures:81",
                    "UpdateRenderTextures:81", "UpdateCameraProperties:81", "OnPreRender:81", ".ctor:1886"
                }),
            new ShippedType("AmplifyColorTriggerProxy", 0x100001, "AmplifyColorTriggerProxyBase",
                new[]
                {
                    "sphereCollider:1", "rigidBody:1"
                },
                new[]
                {
                    "Start:81", "LateUpdate:81", ".ctor:1886"
                }),
            new ShippedType("AmplifyColorTriggerProxy2D", 0x100001, "AmplifyColorTriggerProxyBase",
                new[]
                {
                    "circleCollider:1", "rigidBody:1"
                },
                new[]
                {
                    "Start:81", "LateUpdate:81", ".ctor:1886"
                }),
            new ShippedType("AmplifyColorTriggerProxyBase", 0x100001, "UnityEngine.MonoBehaviour",
                new[]
                {
                    "Reference:6", "OwnerEffect:6"
                },
                new[]
                {
                    ".ctor:1886"
                }),
            new ShippedType("AmplifyColorVolume", 0x100001, "AmplifyColorVolumeBase",
                new string[0],
                new[]
                {
                    "OnTriggerEnter:81", "OnTriggerExit:81", ".ctor:1886"
                }),
            new ShippedType("AmplifyColorVolume2D", 0x100001, "AmplifyColorVolumeBase",
                new string[0],
                new[]
                {
                    "OnTriggerEnter2D:81", "OnTriggerExit2D:81", ".ctor:1886"
                }),
            new ShippedType("AmplifyColorVolumeBase", 0x100001, "UnityEngine.MonoBehaviour",
                new[]
                {
                    "LutTexture:6", "Exposure:6", "EnterBlendTime:6", "Priority:6", "ShowInSceneView:6",
                    "EffectContainer:6"
                },
                new[]
                {
                    "OnDrawGizmos:81", "OnDrawGizmosSelected:81", ".ctor:1886"
                }),
            new ShippedType("AmplifyColor.Quality", 0x101, "System.Enum",
                new[]
                {
                    "value__:606", "Mobile:8056", "Standard:8056"
                },
                new string[0]),
            new ShippedType("AmplifyColor.Tonemapping", 0x101, "System.Enum",
                new[]
                {
                    "value__:606", "Disabled:8056", "Photographic:8056", "FilmicHable:8056", "FilmicACES:8056"
                },
                new string[0]),
            new ShippedType("AmplifyColor.RenderLayer", 0x102109, "System.ValueType",
                new[]
                {
                    "mask:6", "color:6"
                },
                new[]
                {
                    ".ctor:1886"
                }),
            new ShippedType("AmplifyColor.VersionInfo", 0x102001, "System.Object",
                new[]
                {
                    "Major:8056", "Minor:8056", "Release:8056", "StageSuffix:11", "TrialSuffix:11", "m_major:1",
                    "m_minor:1", "m_release:1"
                },
                new[]
                {
                    "StaticToString:96", "ToString:c6", "get_Number:886", ".ctor:1881", ".ctor:1881", "Current:96",
                    "Matches:96", ".cctor:1891"
                }),
            new ShippedType("AmplifyColor.VolumeEffectField", 0x102001, "System.Object",
                new[]
                {
                    "fieldName:6", "fieldType:6", "valueSingle:6", "valueColor:6", "valueBoolean:6",
                    "valueVector2:6", "valueVector3:6", "valueVector4:6"
                },
                new[]
                {
                    ".ctor:1886", ".ctor:1886", "IsValidType:96", "UpdateValue:86"
                }),
            new ShippedType("AmplifyColor.VolumeEffectComponent", 0x102001, "System.Object",
                new[]
                {
                    "componentName:6", "fields:6"
                },
                new[]
                {
                    ".ctor:1886", "AddField:86", "AddField:86", "RemoveEffectField:86", ".ctor:1886",
                    "UpdateComponent:86", "FindEffectField:86", "ListAcceptableFields:96", "GetFieldNames:86"
                }),
            new ShippedType("AmplifyColor.VolumeEffectComponent+<>c", 0x102103, "System.Object",
                new[]
                {
                    "<>9:36", "<>9__9_0:16", "<>9__10_0:16"
                },
                new[]
                {
                    ".cctor:1891", ".ctor:1886", "<ListAcceptableFields>b__9_0:83", "<GetFieldNames>b__10_0:83"
                }),
            new ShippedType("AmplifyColor.VolumeEffectComponent+<>c__DisplayClass7_0", 0x100103, "System.Object",
                new[]
                {
                    "fieldFlags:6"
                },
                new[]
                {
                    ".ctor:1886", "<UpdateComponent>b__0:83"
                }),
            new ShippedType("AmplifyColor.VolumeEffect", 0x102001, "System.Object",
                new[]
                {
                    "gameObject:6", "components:6"
                },
                new[]
                {
                    ".ctor:1886", "BlendValuesToVolumeEffect:96", "AddComponent:86", "RemoveEffectComponent:86",
                    "UpdateVolume:86", "SetValues:86", "BlendValues:86", "FindEffectComponent:86",
                    "ListAcceptableComponents:96", "GetComponentNames:86"
                }),
            new ShippedType("AmplifyColor.VolumeEffect+<>c", 0x102103, "System.Object",
                new[]
                {
                    "<>9:36", "<>9__10_0:16", "<>9__11_0:16"
                },
                new[]
                {
                    ".cctor:1891", ".ctor:1886", "<ListAcceptableComponents>b__10_0:83",
                    "<GetComponentNames>b__11_0:83"
                }),
            new ShippedType("AmplifyColor.VolumeEffectContainer", 0x102001, "System.Object",
                new[]
                {
                    "volumes:6"
                },
                new[]
                {
                    ".ctor:1886", "AddColorEffect:86", "AddJustColorEffect:86", "FindVolumeEffect:86",
                    "RemoveVolumeEffect:86", "GetStoredEffects:86"
                }),
            new ShippedType("AmplifyColor.VolumeEffectContainer+<>c", 0x102103, "System.Object",
                new[]
                {
                    "<>9:36", "<>9__6_0:16"
                },
                new[]
                {
                    ".cctor:1891", ".ctor:1886", "<GetStoredEffects>b__6_0:83"
                }),
            new ShippedType("AmplifyColor.VolumeEffectFieldFlags", 0x102001, "System.Object",
                new[]
                {
                    "fieldName:6", "fieldType:6", "blendFlag:6"
                },
                new[]
                {
                    ".ctor:1886", ".ctor:1886"
                }),
            new ShippedType("AmplifyColor.VolumeEffectComponentFlags", 0x102001, "System.Object",
                new[]
                {
                    "componentName:6", "componentFields:6", "blendFlag:6"
                },
                new[]
                {
                    ".ctor:1886", ".ctor:1886", ".ctor:1886", "UpdateComponentFlags:86", "UpdateComponentFlags:86",
                    "GetFieldNames:86"
                }),
            new ShippedType("AmplifyColor.VolumeEffectComponentFlags+<>c", 0x102103, "System.Object",
                new[]
                {
                    "<>9:36", "<>9__8_0:16", "<>9__8_1:16"
                },
                new[]
                {
                    ".cctor:1891", ".ctor:1886", "<GetFieldNames>b__8_0:83", "<GetFieldNames>b__8_1:83"
                }),
            new ShippedType("AmplifyColor.VolumeEffectComponentFlags+<>c__DisplayClass6_0", 0x100103, "System.Object",
                new[]
                {
                    "field:6"
                },
                new[]
                {
                    ".ctor:1886", "<UpdateComponentFlags>b__0:83"
                }),
            new ShippedType("AmplifyColor.VolumeEffectComponentFlags+<>c__DisplayClass7_0", 0x100103, "System.Object",
                new[]
                {
                    "pi:6"
                },
                new[]
                {
                    ".ctor:1886", "<UpdateComponentFlags>b__0:83"
                }),
            new ShippedType("AmplifyColor.VolumeEffectFlags", 0x102001, "System.Object",
                new[]
                {
                    "components:6"
                },
                new[]
                {
                    ".ctor:1886", "AddComponent:86", "UpdateFlags:86", "UpdateCamFlags:96",
                    "GenerateEffectData:86", "FindComponentFlags:86", "GetComponentNames:86"
                }),
            new ShippedType("AmplifyColor.VolumeEffectFlags+<>c", 0x102103, "System.Object",
                new[]
                {
                    "<>9:36", "<>9__7_0:16", "<>9__7_1:16"
                },
                new[]
                {
                    ".cctor:1891", ".ctor:1886", "<GetComponentNames>b__7_0:83", "<GetComponentNames>b__7_1:83"
                }),
            new ShippedType("AmplifyColor.VolumeEffectFlags+<>c__DisplayClass2_0", 0x100103, "System.Object",
                new[]
                {
                    "c:6"
                },
                new[]
                {
                    ".ctor:1886", "<AddComponent>b__0:83"
                }),
            new ShippedType("AmplifyColor.VolumeEffectFlags+<>c__DisplayClass3_0", 0x100103, "System.Object",
                new[]
                {
                    "comp:6"
                },
                new[]
                {
                    ".ctor:1886", "<UpdateFlags>b__0:83"
                })
        };
    }
}
