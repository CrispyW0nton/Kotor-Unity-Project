#!/usr/bin/env python3
"""
Generate Assets/Scenes/Game.unity — the main gameplay scene for KotOR-Unity.

Objects created:
  _PersistentManagers (ref only — these already exist from Boot scene via DontDestroyOnLoad)
  Cameras (parent)
    RTSCamera  — RTSCamera.cs + Camera
    ActionCamera — ActionCamera.cs + Camera
    CameraTransitionController — CameraTransitionController.cs
  Player — PlayerStatsBehaviour + RTSPlayerController + NavMeshAgent + CharacterController
  HUD — Canvas with HUDManager (all optional refs left as 0)
  GameManager — GameManager.cs
  AreaLoader — AreaLoader.cs
  EventSystem + StandaloneInputModule

Script GUIDs (from user project .meta files):
  GameManager          1c9ecbd6de3b1804db301d2be0c2a0d0
  ModeSwitchSystem     b2e03ae3f83708340a102e6987b57cb1  (added to GameManager GO)
  RTSCamera            e7e23543c28fe8f4dacb631d8bc60c56
  ActionCamera         443fde4e99c043242ab329771fe25d41
  CameraTransitionCtrl 46a129f49f08ed04886787d5f52385b8
  PlayerStatsBehaviour 2ef78df0915112046a93109282d657a1
  RTSPlayerController  bd0064989272d054cbf0f8d7d5ba75f4
  HUDManager           550c2c83a0d138844aa8b6a24fe6a64b
  AreaLoader           651db73161f8d1f4fa75c589324fbd1d
"""

SCENE_HEADER = """\
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!29 &1
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
  m_SceneGUID: 00000000000000000000000000000000
  m_OcclusionCullingData: {fileID: 0}
--- !u!104 &2
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 9
  m_Fog: 0
  m_FogColor: {r: 0.5, g: 0.5, b: 0.5, a: 1}
  m_FogMode: 3
  m_FogDensity: 0.01
  m_LinearFogStart: 0
  m_LinearFogEnd: 300
  m_AmbientSkyColor: {r: 0.212, g: 0.227, b: 0.259, a: 1}
  m_AmbientEquatorColor: {r: 0.114, g: 0.125, b: 0.133, a: 1}
  m_AmbientGroundColor: {r: 0.047, g: 0.043, b: 0.035, a: 1}
  m_AmbientIntensity: 1
  m_AmbientMode: 0
  m_SubtractiveShadowColor: {r: 0.42, g: 0.478, b: 0.627, a: 1}
  m_SkyboxMaterial: {fileID: 10304, guid: 0000000000000000f000000000000000, type: 0}
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {fileID: 0}
  m_SpotCookie: {fileID: 10001, guid: 0000000000000000e000000000000000, type: 0}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {fileID: 0}
  m_Sun: {fileID: 0}
  m_UseRadianceAmbientProbe: 0
--- !u!157 &3
LightmapSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 12
  m_GIWorkflowMode: 1
  m_GISettings:
    serializedVersion: 2
    m_BounceScale: 1
    m_IndirectOutputScale: 1
    m_AlbedoBoost: 1
    m_EnvironmentLightingMode: 0
    m_EnableBakedLightmaps: 1
    m_EnableRealtimeLightmaps: 0
  m_LightmapEditorSettings:
    serializedVersion: 12
    m_Resolution: 2
    m_BakeResolution: 40
    m_AtlasSize: 1024
    m_AO: 0
    m_AOMaxDistance: 1
    m_CompAOExponent: 1
    m_CompAOExponentDirect: 0
    m_ExtractAmbientOcclusion: 0
    m_Padding: 2
    m_LightmapParameters: {fileID: 0}
    m_LightmapsBakeMode: 1
    m_TextureCompression: 1
    m_FinalGather: 0
    m_FinalGatherFiltering: 1
    m_FinalGatherRayCount: 256
    m_ReflectionCompression: 2
    m_MixedBakeMode: 2
    m_BakeBackend: 1
    m_PVRSampling: 1
    m_PVRDirectSampleCount: 32
    m_PVRSampleCount: 512
    m_PVRBounces: 2
    m_PVREnvironmentSampleCount: 256
    m_PVREnvironmentReferencePointCount: 2048
    m_PVRFilteringMode: 1
    m_PVRDenoiserTypeDirect: 1
    m_PVRDenoiserTypeIndirect: 1
    m_PVRDenoiserTypeAO: 1
    m_PVRFilterTypeDirect: 0
    m_PVRFilterTypeIndirect: 0
    m_PVRFilterTypeAO: 0
    m_PVREnvironmentMIS: 1
    m_PVRCulling: 1
    m_PVRFilteringGaussRadiusDirect: 1
    m_PVRFilteringGaussRadiusIndirect: 5
    m_PVRFilteringGaussRadiusAO: 2
    m_PVRFilteringAtrousPositionSigmaDirect: 0.5
    m_PVRFilteringAtrousPositionSigmaIndirect: 2
    m_PVRFilteringAtrousPositionSigmaAO: 1
    m_ExportTrainingData: 0
    m_TrainingDataDestination: TrainingData
    m_LightProbeSampleCountMultiplier: 4
  m_LightingDataAsset: {fileID: 0}
  m_LightingSettings: {fileID: 0}
--- !u!196 &4
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 3
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.4
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
    manualTileSize: 0
    tileSize: 256
    buildHeightMesh: 0
    maxJobWorkers: 0
    preserveTilesOutsideBounds: 0
    debug:
      m_Flags: 0
  m_NavMeshData: {fileID: 0}
"""

# ──────────────────────────────────────────────────────────────────────────────
# File-ID allocations  (deterministic, easy to grep)
# ──────────────────────────────────────────────────────────────────────────────
#
#  100 – 109  Cameras root GO + Transform
#  110 – 119  RTSCamera GO + Transform + Camera + MonoBehaviour(RTSCamera)
#  120 – 129  ActionCamera GO + Transform + Camera + MonoBehaviour(ActionCamera)
#  130 – 139  CameraTransitionController GO + Transform + MonoBehaviour
#  140 – 149  Player GO + Transform + CharacterController + MonoBehaviour(PlayerStats) + MonoBehaviour(RTSPlayer)
#  150 – 159  GameManager GO + Transform + MonoBehaviour(GameManager) + MonoBehaviour(ModeSwitchSystem)
#  160 – 169  AreaLoader GO + Transform + MonoBehaviour
#  170 – 179  HUD Canvas GO + Transform + Canvas + CanvasScaler + GraphicRaycaster + MonoBehaviour(HUDManager)
#  180 – 189  EventSystem GO + Transform + EventSystem + StandaloneInputModule
#  190 – 199  DirectionalLight GO + Transform + Light
#  200 – 209  Ground plane GO + Transform + MeshFilter + MeshRenderer + MeshCollider
#  210 – 219  Spawn point GO + Transform

def go_transform(fid_go, fid_tr, name, tag, fid_parent_tr, children_fids, pos="0, 0, 0", rot="-0, -0, -0, 1"):
    children_yaml = "\n".join(f"  - {{fileID: {c}}}" for c in children_fids)
    if children_yaml:
        children_yaml = "\n" + children_yaml
    return f"""\
--- !u!1 &{fid_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {fid_tr}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: {tag}
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{fid_tr}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: {rot.split(', ')[0].strip()}, y: {rot.split(', ')[1].strip()}, z: {rot.split(', ')[2].strip()}, w: {rot.split(', ')[3].strip()}}}
  m_LocalPosition: {{x: {pos.split(', ')[0].strip()}, y: {pos.split(', ')[1].strip()}, z: {pos.split(', ')[2].strip()}}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: [{children_yaml}]
  m_Father: {{fileID: {fid_parent_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
"""

def go_with_components(fid_go, fid_tr, name, tag, fid_parent_tr, children_fids, component_fids, pos="0, 0, 0", rot="-0, -0, -0, 1"):
    """GO that carries multiple extra components (listed in m_Component)."""
    comp_lines = f"  - component: {{fileID: {fid_tr}}}\n"
    for c in component_fids:
        comp_lines += f"  - component: {{fileID: {c}}}\n"
    children_yaml = "\n".join(f"  - {{fileID: {c}}}" for c in children_fids)
    if children_yaml:
        children_yaml = "\n" + children_yaml
    return f"""\
--- !u!1 &{fid_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
{comp_lines.rstrip()}
  m_Layer: 0
  m_Name: {name}
  m_TagString: {tag}
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{fid_tr}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: {rot.split(', ')[0].strip()}, y: {rot.split(', ')[1].strip()}, z: {rot.split(', ')[2].strip()}, w: {rot.split(', ')[3].strip()}}}
  m_LocalPosition: {{x: {pos.split(', ')[0].strip()}, y: {pos.split(', ')[1].strip()}, z: {pos.split(', ')[2].strip()}}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: [{children_yaml}]
  m_Father: {{fileID: {fid_parent_tr}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
"""

def monobehaviour(fid_mb, fid_go, guid, extra_fields=""):
    return f"""\
--- !u!114 &{fid_mb}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
{extra_fields}"""

def camera_component(fid_cam, fid_go, fov=70, near=0.3, far=1000):
    return f"""\
--- !u!20 &{fid_cam}
Camera:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  m_Enabled: 1
  serializedVersion: 2
  m_ClearFlags: 1
  m_BackGroundColor: {{r: 0.19215686, g: 0.3019608, b: 0.4745098, a: 0}}
  m_projectionMatrixMode: 1
  m_GateFitMode: 2
  m_FOVAxisMode: 0
  m_Iso: 200
  m_ShutterSpeed: 0.005
  m_Aperture: 16
  m_FocusDistance: 10
  m_FocalLength: 50
  m_BladeCount: 5
  m_Curvature: {{x: 2, y: 11}}
  m_BarrelClipping: 0.25
  m_Anamorphism: 0
  m_SensorSize: {{x: 23.76, y: 13.365}}
  m_LensShift: {{x: 0, y: 0}}
  m_NormalizedViewPortRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  near clip plane: {near}
  far clip plane: {far}
  field of view: {fov}
  orthographic: 0
  orthographic size: 5
  m_Depth: -1
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingPath: -1
  m_TargetTexture: {{fileID: 0}}
  m_TargetDisplay: 0
  m_TargetEye: 3
  m_HDR: 1
  m_AllowMSAA: 1
  m_AllowDynamicResolution: 0
  m_ForceIntoRT: 0
  m_OcclusionCulling: 1
  m_StereoConvergence: 10
  m_StereoSeparation: 0.022
"""

def audio_listener(fid_al, fid_go):
    return f"""\
--- !u!81 &{fid_al}
AudioListener:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  m_Enabled: 1
"""

def character_controller(fid_cc, fid_go):
    return f"""\
--- !u!111 &{fid_cc}
CharacterController:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  m_Enabled: 1
  serializedVersion: 3
  m_Height: 1.8
  m_Radius: 0.35
  m_SlopeLimit: 45
  m_StepOffset: 0.3
  m_SkinWidth: 0.08
  m_MinMoveDistance: 0.001
  m_Center: {{x: 0, y: 0.9, z: 0}}
"""

def canvas_component(fid_canvas, fid_go):
    return f"""\
--- !u!223 &{fid_canvas}
Canvas:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  m_Enabled: 1
  serializedVersion: 3
  m_RenderMode: 0
  m_Camera: {{fileID: 0}}
  m_PlaneDistance: 100
  m_PixelPerfect: 0
  m_ReceivesEvents: 1
  m_OverrideSorting: 0
  m_OverridePixelPerfect: 0
  m_SortingBucketNormalizedSize: 0
  m_VertexColorAlwaysGammaSpace: 0
  m_AdditionalShaderChannelsFlag: 25
  m_UpdateRectTransformForStandalone: 0
  m_SortingLayerID: 0
  m_SortingOrder: 0
  m_TargetDisplay: 0
"""

def canvas_scaler(fid_cs, fid_go):
    return f"""\
--- !u!114 &{fid_cs}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 0cd44c1031e13a943bb63640046fad76, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_UiScaleMode: 1
  m_ReferencePixelsPerUnit: 100
  m_ScaleFactor: 1
  m_ReferenceResolution: {{x: 1920, y: 1080}}
  m_ScreenMatchMode: 0
  m_MatchWidthOrHeight: 0.5
  m_PhysicalUnit: 3
  m_FallbackScreenDPI: 96
  m_DefaultSpriteDPI: 96
  m_DynamicPixelsPerUnit: 1
  m_PresetInfoIsWorld: 0
"""

def graphic_raycaster(fid_gr, fid_go):
    return f"""\
--- !u!114 &{fid_gr}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: dc42784cf147c0c48a680349fa168899, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_IgnoreReversedGraphics: 1
  m_BlockingObjects: 0
  m_BlockingMask:
    serializedVersion: 2
    m_Bits: 4294967295
"""

def rect_transform(fid_rt, fid_go, fid_parent, children_fids, anchor_min="0, 0", anchor_max="1, 1", pivot="0.5, 0.5", pos="0, 0, 0", size="0, 0"):
    children_yaml = "\n".join(f"  - {{fileID: {c}}}" for c in children_fids)
    if children_yaml:
        children_yaml = "\n" + children_yaml
    return f"""\
--- !u!224 &{fid_rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: [{children_yaml}]
  m_Father: {{fileID: {fid_parent}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: {anchor_min.split(', ')[0]}, y: {anchor_min.split(', ')[1]}}}
  m_AnchorMax: {{x: {anchor_max.split(', ')[0]}, y: {anchor_max.split(', ')[1]}}}
  m_AnchoredPosition: {{x: {pos.split(', ')[0]}, y: {pos.split(', ')[1]}}}
  m_SizeDelta: {{x: {size.split(', ')[0]}, y: {size.split(', ')[1]}}}
  m_Pivot: {{x: {pivot.split(', ')[0]}, y: {pivot.split(', ')[1]}}}
"""

def event_system_comp(fid_es, fid_go):
    return f"""\
--- !u!114 &{fid_es}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 76c392e42b5098c458856cdf6ecaaaa1, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_FirstSelected: {{fileID: 0}}
  m_sendNavigationEvents: 1
  m_DragThreshold: 10
"""

def standalone_input(fid_si, fid_go):
    return f"""\
--- !u!114 &{fid_si}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 4f231c4fb786f3946a6b90b886c48677, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_HorizontalAxis: Horizontal
  m_VerticalAxis: Vertical
  m_SubmitButton: Submit
  m_CancelButton: Cancel
  m_InputActionsPerSecond: 10
  m_RepeatDelay: 0.5
  m_ForceModuleActive: 0
"""

def directional_light(fid_go, fid_tr, fid_light):
    return f"""\
--- !u!1 &{fid_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {fid_tr}}}
  - component: {{fileID: {fid_light}}}
  m_Layer: 0
  m_Name: Directional Light
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{fid_tr}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0.40821788, y: -0.23456968, z: 0.10938163, w: 0.8754261}}
  m_LocalPosition: {{x: 0, y: 3, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 50, y: -30, z: 0}}
--- !u!108 &{fid_light}
Light:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  m_Enabled: 1
  serializedVersion: 10
  m_Type: 1
  m_Shape: 0
  m_Color: {{r: 1, g: 0.95686275, b: 0.8392157, a: 1}}
  m_Intensity: 1
  m_Range: 10
  m_SpotAngle: 30
  m_InnerSpotAngle: 21.80208
  m_CookieSize: 10
  m_Shadows:
    m_Type: 2
    m_Resolution: -1
    m_CustomResolution: -1
    m_Strength: 1
    m_Bias: 0.05
    m_NormalBias: 0.4
    m_NearPlane: 0.2
    m_CullingMatrixOverride:
      e00: 1
      e01: 0
      e02: 0
      e03: 0
      e10: 0
      e11: 1
      e12: 0
      e13: 0
      e20: 0
      e21: 0
      e22: 1
      e23: 0
      e30: 0
      e31: 0
      e32: 0
      e33: 1
    m_UseCullingMatrixOverride: 0
  m_Cookie: {{fileID: 0}}
  m_DrawHalo: 0
  m_Flare: {{fileID: 0}}
  m_RenderMode: 0
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingLayerMask: 1
  m_Lightmapping: 4
  m_LightShadowCasterMode: 0
  m_AreaSize: {{x: 1, y: 1}}
  m_BounceIntensity: 1
  m_ColorTemperature: 6570
  m_UseColorTemperature: 0
  m_BoundingSphereOverride: {{x: 0, y: 0, z: 0, w: 0}}
  m_UseBoundingSphereOverride: 0
  m_UseViewFrustumForShadowCasterCull: 1
  m_ShadowRadius: 0
  m_ShadowAngle: 0
"""

# ──────────────────────────────────────────────────────────────────────────────
# Script GUIDs
# ──────────────────────────────────────────────────────────────────────────────
GUID_GameManager          = "1c9ecbd6de3b1804db301d2be0c2a0d0"
GUID_ModeSwitchSystem     = "b2e03ae3f83708340a102e6987b57cb1"
GUID_RTSCamera            = "e7e23543c28fe8f4dacb631d8bc60c56"
GUID_ActionCamera         = "443fde4e99c043242ab329771fe25d41"
GUID_CameraTransitionCtrl = "46a129f49f08ed04886787d5f52385b8"
GUID_PlayerStatsBehaviour = "2ef78df0915112046a93109282d657a1"
GUID_RTSPlayerController  = "bd0064989272d054cbf0f8d7d5ba75f4"
GUID_HUDManager           = "550c2c83a0d138844aa8b6a24fe6a64b"
GUID_AreaLoader           = "651db73161f8d1f4fa75c589324fbd1d"

# ──────────────────────────────────────────────────────────────────────────────
# Build scene
# ──────────────────────────────────────────────────────────────────────────────
chunks = [SCENE_HEADER]

# ── Cameras (root GO with no script) ─────────────────────────────────────────
# Cameras root  (fid 100/101)
chunks.append(go_with_components(
    fid_go=100, fid_tr=101,
    name="Cameras", tag="Untagged",
    fid_parent_tr=0,
    children_fids=[111, 121, 131],
    component_fids=[],
    pos="0, 0, 0"
))

# RTSCamera GO (fid 110/111 + camera 112 + MB 113)
rts_cam_fields = """\
  minHeight: 10
  maxHeight: 50
  defaultHeight: 25
  zoomSpeed: 5
  zoomSmoothTime: 0.2
  minFOV: 25
  maxFOV: 60
  panSpeed: 20
  panSmoothTime: 0.15
  rotateSpeed: 80
  pitchAngle: 55
  autoFrameSquadOnEnter: 1
  squadTargets: []
"""
chunks.append(go_with_components(
    fid_go=110, fid_tr=111,
    name="RTSCamera", tag="Untagged",
    fid_parent_tr=101,
    children_fids=[],
    component_fids=[112, 113],
    pos="0, 25, -15",
    rot="-0.42, -0, -0, 0.9"
))
chunks.append(camera_component(112, 110, fov=45))
chunks.append(monobehaviour(113, 110, GUID_RTSCamera, rts_cam_fields))

# ActionCamera GO (fid 120/121 + camera 122 + MB 123 + AudioListener 124)
action_cam_fields = """\
  target: {fileID: 141}
  offset: {x: 0, y: 1.5, z: -3}
  shoulderOffset: 0.5
  positionSmoothTime: 0.1
  rotationSmoothTime: 0.08
  normalFOV: 70
  aimFOV: 45
  fovSmoothTime: 0.1
  collisionMask:
    serializedVersion: 2
    m_Bits: 4294967295
  collisionRadius: 0.2
  minDistance: 0.5
"""
chunks.append(go_with_components(
    fid_go=120, fid_tr=121,
    name="ActionCamera", tag="MainCamera",
    fid_parent_tr=101,
    children_fids=[],
    component_fids=[122, 123, 124],
    pos="0, 1.5, -3",
    rot="-0, -0, -0, 1"
))
chunks.append(camera_component(122, 120, fov=70))
chunks.append(monobehaviour(123, 120, GUID_ActionCamera, action_cam_fields))
chunks.append(audio_listener(124, 120))

# CameraTransitionController (fid 130/131 + MB 132)
ctc_fields = f"""\
  actionCamera: {{fileID: 122}}
  rtsCamera: {{fileID: 112}}
  playerTransform: {{fileID: 141}}
  playerRenderer: {{fileID: 0}}
  pulseColor: {{r: 0.2, g: 0.8, b: 1, a: 0.8}}
"""
chunks.append(go_with_components(
    fid_go=130, fid_tr=131,
    name="CameraTransitionController", tag="Untagged",
    fid_parent_tr=101,
    children_fids=[],
    component_fids=[132],
    pos="0, 0, 0"
))
chunks.append(monobehaviour(132, 130, GUID_CameraTransitionCtrl, ctc_fields))

# ── Player GO (fid 140/141 + CC 142 + MB_PlayerStats 143 + MB_RTSPlayer 144) ─
player_stats_fields = """\
  characterName: Revan
  startingLevel: 1
"""
rts_player_fields = """\
  panSpeed: 20
  edgeScrollThreshold: 10
  edgeScrollEnabled: 1
  selectableLayerMask:
    serializedVersion: 2
    m_Bits: 4294967295
  terrainLayerMask:
    serializedVersion: 2
    m_Bits: 4294967295
  selectionRaycastRange: 100
  squadMembers: []
"""
chunks.append(go_with_components(
    fid_go=140, fid_tr=141,
    name="Player", tag="Player",
    fid_parent_tr=0,
    children_fids=[],
    component_fids=[142, 143, 144],
    pos="0, 0, 0"
))
chunks.append(character_controller(142, 140))
chunks.append(monobehaviour(143, 140, GUID_PlayerStatsBehaviour, player_stats_fields))
chunks.append(monobehaviour(144, 140, GUID_RTSPlayerController, rts_player_fields))

# ── GameManager GO (fid 150/151 + MB_GameManager 152 + MB_ModeSwitchSystem 153) ─
gm_fields = """\
  kotorDir: 
  targetGame: 0
  entryModule: danm14aa
  difficulty: 1
  defaultMode: 0
  debugMode: 0
  showModeOverlay: 1
"""
chunks.append(go_with_components(
    fid_go=150, fid_tr=151,
    name="GameManager", tag="Untagged",
    fid_parent_tr=0,
    children_fids=[],
    component_fids=[152, 153],
    pos="0, 0, 0"
))
chunks.append(monobehaviour(152, 150, GUID_GameManager, gm_fields))
chunks.append(monobehaviour(153, 150, GUID_ModeSwitchSystem, ""))

# ── AreaLoader GO (fid 160/161 + MB 162) ─────────────────────────────────────
al_fields = """\
  startupModuleName: danm14aa
  autoLoadOnStart: 1
"""
chunks.append(go_with_components(
    fid_go=160, fid_tr=161,
    name="AreaLoader", tag="Untagged",
    fid_parent_tr=0,
    children_fids=[],
    component_fids=[162],
    pos="0, 0, 0"
))
chunks.append(monobehaviour(162, 160, GUID_AreaLoader, al_fields))

# ── HUD Canvas (fid 170/RT:171 + Canvas 172 + CanvasScaler 173 + GR 174 + HUDManager MB 175) ─
hud_fields = """\
  healthBar: {fileID: 0}
  shieldBar: {fileID: 0}
  staminaBar: {fileID: 0}
  healthText: {fileID: 0}
  modeLabel: {fileID: 0}
  modeIndicatorBg: {fileID: 0}
  actionModeColor: {r: 0.9, g: 0.4, b: 0.1, a: 1}
  rtsModeColor: {r: 0.1, g: 0.6, b: 1, a: 1}
  modeSwitchCooldownBar: {fileID: 0}
  modeSwitchCooldownText: {fileID: 0}
  ammoText: {fileID: 0}
  abilityIcons: []
  abilityCooldownBars: []
  rtsPauseIndicator: {fileID: 0}
  hitChanceText: {fileID: 0}
  crosshair: {fileID: 0}
  xpBar: {fileID: 0}
  levelText: {fileID: 0}
"""
# HUD Canvas GO — uses RectTransform, so build manually
hud_canvas_go = f"""\
--- !u!1 &170
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 171}}
  - component: {{fileID: 172}}
  - component: {{fileID: 173}}
  - component: {{fileID: 174}}
  - component: {{fileID: 175}}
  m_Layer: 5
  m_Name: HUD
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""
chunks.append(hud_canvas_go)
chunks.append(rect_transform(171, 170, 0, []))
chunks.append(canvas_component(172, 170))
chunks.append(canvas_scaler(173, 170))
chunks.append(graphic_raycaster(174, 170))
chunks.append(monobehaviour(175, 170, GUID_HUDManager, hud_fields))

# ── EventSystem (fid 180/181 + ES 182 + SIM 183) ─────────────────────────────
chunks.append(go_with_components(
    fid_go=180, fid_tr=181,
    name="EventSystem", tag="Untagged",
    fid_parent_tr=0,
    children_fids=[],
    component_fids=[182, 183],
    pos="0, 0, 0"
))
chunks.append(event_system_comp(182, 180))
chunks.append(standalone_input(183, 180))

# ── Directional Light (fid 190/191/192) ──────────────────────────────────────
chunks.append(directional_light(190, 191, 192))

# ── SpawnPoint helper (fid 210/211) — marks where Player spawns ──────────────
chunks.append(go_transform(
    fid_go=210, fid_tr=211,
    name="SpawnPoint", tag="Untagged",
    fid_parent_tr=0,
    children_fids=[],
    pos="0, 0, 5"
))

scene = "\n".join(chunks)

out = "Assets/Scenes/Game.unity"
with open(out, "w") as f:
    f.write(scene)

lines = scene.count("\n")
# Count anchor markers
import re
anchors = len(re.findall(r"^--- ", scene, re.MULTILINE))
print(f"Game.unity written ({len(scene):,} characters, {lines:,} lines, {anchors} YAML documents)")
print("Objects created:")
print("  Cameras (parent) → RTSCamera, ActionCamera, CameraTransitionController")
print("  Player (PlayerStatsBehaviour + RTSPlayerController + CharacterController)")
print("  GameManager (GameManager + ModeSwitchSystem)")
print("  AreaLoader")
print("  HUD Canvas (HUDManager — all optional refs null)")
print("  EventSystem + StandaloneInputModule")
print("  Directional Light")
print("  SpawnPoint")
print()
print("Key wiring:")
print("  ActionCamera.target  → Player Transform (fileID 141)")
print("  CameraTransitionCtrl.actionCamera → ActionCamera Camera (fileID 122)")
print("  CameraTransitionCtrl.rtsCamera    → RTSCamera Camera (fileID 112)")
print("  CameraTransitionCtrl.playerTransform → Player Transform (fileID 141)")
print("  Player tag = 'Player'  (HUDManager.Start does FindWithTag('Player'))")
