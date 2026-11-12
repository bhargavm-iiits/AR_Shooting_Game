using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;

namespace RealityDefender.Editor
{
    /// <summary>Reproducible editor authoring. All scene and prefab references are serialized by Unity.</summary>
    public static class ProjectBuilder
    {
        public const string Root = "Assets/RealityDefender/";
        static readonly Color Cyan = new Color(.03f,.78f,1f);
        static Material armor, cyan, amber, white, plane;

        [MenuItem("Reality Defender/Rebuild Game Assets and Scene")]
        public static void BuildProject()
        {
            foreach (string p in new[]{"Art/Materials","Art/Textures","Art/VFX","Prefabs","Scenes"}) Directory.CreateDirectory(Root+p);
            AssetDatabase.Refresh();
            AndroidBuild.Configure();
            ConfigureRenderer(); ConfigureLayers(); ConfigureImports();
            Material energy = EnergyShaderBuilder.Build();
            if (!energy || !energy.shader || energy.shader.name.Contains("Error")) throw new Exception("Energy Shader Graph is not ready");
            armor = Lit("M_Armor",new Color(.075f,.105f,.15f),.6f,.48f);
            cyan = Unlit("M_Cyan",Cyan); amber = Unlit("M_Amber",new Color(1,.54f,.06f));
            white = Unlit("M_Projectile",new Color(1,.88f,.55f));
            plane = Unlit("M_Scan",new Color(.08f,.68f,.88f));
            var basePrefab = BuildBase();
            var targetPrefab = BuildTarget(energy);
            var projectilePrefab = BuildProjectile();
            var railgunPrefab = BuildRailgun();
            var impactPrefab = BuildImpact();
            BuildScene(basePrefab,targetPrefab,projectilePrefab,railgunPrefab,impactPrefab);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Submission/SceneAssembly.json",JsonUtility.ToJson(new AssemblySummary {
                scene=Root+"Scenes/RealityDefender.unity", baseDiameter=.65f, baseHeight=.13f,
                orbDiameter=.14f, targets=3, origin="bottom center y=0; unit scale", renderer="Mobile URP Forward, GLES3, HDR off, AR background feature"
            },true));
            Debug.Log("REALITY_DEFENDER_ASSEMBLY_SUCCEEDED");
        }

        /// <summary>Applies the AR stability fixes to the existing scene without regenerating other assets.</summary>
        [MenuItem("Reality Defender/Apply AR Stability Settings")]
        public static void ApplyArStability()
        {
            var scene=EditorSceneManager.OpenScene(Root+"Scenes/RealityDefender.unity",OpenSceneMode.Single);
            ConfigureArStability();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("REALITY_DEFENDER_AR_STABILITY_APPLIED");
        }

        /// <summary>Batch entry point: apply the stability settings, then build the APK.</summary>
        public static void ApplyArStabilityAndBuild() { ApplyArStability(); AndroidBuild.BuildApk(); }

        static void ConfigureArStability()
        {
            var placement=UnityEngine.Object.FindAnyObjectByType<PlacementController>(FindObjectsInactive.Include);
            if(!placement) throw new Exception("Scene has no PlacementController");
            placement.contactShadowMaterial=ContactShadowMaterial(); placement.arenaScale=2.5f;
            var ringShader=Shader.Find("RealityDefender/Placement Ring"); if(!ringShader) throw new Exception("Placement ring shader did not import");
            SaveMaterial(new Material(ringShader){name="M_PlacementRing"});
            placement.placementRingMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/Materials/M_PlacementRing.mat");
            EditorUtility.SetDirty(placement);
            // Layered orb blast: additive glow + shockwave ring variants of one texture-free particle shader.
            var particleShader=Shader.Find("RealityDefender/Energy Particle"); if(!particleShader) throw new Exception("Energy particle shader did not import");
            var glowMat=new Material(particleShader){name="M_BlastGlow"}; glowMat.SetFloat("_Ring",0f); glowMat.SetFloat("_Intensity",1.6f); SaveMaterial(glowMat);
            var ringMat=new Material(particleShader){name="M_BlastRing"}; ringMat.SetFloat("_Ring",1f); ringMat.SetFloat("_Intensity",1.4f); SaveMaterial(ringMat);
            var wave=UnityEngine.Object.FindAnyObjectByType<WaveController>(FindObjectsInactive.Include);
            if(!wave) throw new Exception("Scene has no WaveController");
            wave.blastGlowMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/Materials/M_BlastGlow.mat");
            wave.blastRingMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/Materials/M_BlastRing.mat");
            EditorUtility.SetDirty(wave);
            // ARCore defaults to fixed focus; autofocus shifts intrinsics under visual-inertial tracking.
            foreach(var cameraManager in UnityEngine.Object.FindObjectsByType<ARCameraManager>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            { cameraManager.autoFocusRequested=false; EditorUtility.SetDirty(cameraManager); }
        }

        static Material ContactShadowMaterial()
        {
            var shader=Shader.Find("RealityDefender/Contact Shadow"); if(!shader) throw new Exception("Contact shadow shader did not import");
            SaveMaterial(new Material(shader){name="M_ContactShadow"});
            return AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/Materials/M_ContactShadow.mat");
        }

        [Serializable] class AssemblySummary { public string scene,origin,renderer; public float baseDiameter,baseHeight,orbDiameter; public int targets; }

        static void ConfigureRenderer()
        {
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if (!pipeline) throw new Exception("Missing Mobile URP asset");
            pipeline.supportsHDR=false; pipeline.msaaSampleCount=1; pipeline.renderScale=1f;
            pipeline.shadowDistance=0; pipeline.supportsCameraDepthTexture=false; pipeline.supportsCameraOpaqueTexture=false;
            var so=new SerializedObject(pipeline);
            foreach(var n in new[]{"m_MainLightShadowsSupported","m_AdditionalLightShadowsSupported","m_SoftShadowsSupported"}) { var p=so.FindProperty(n); if(p!=null)p.boolValue=false; }
            var add=so.FindProperty("m_AdditionalLightsRenderingMode"); if(add!=null)add.intValue=0;
            so.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline=pipeline;
            for(int i=0;i<QualitySettings.names.Length;i++) { QualitySettings.SetQualityLevel(i,false); QualitySettings.renderPipeline=pipeline; QualitySettings.shadows=UnityEngine.ShadowQuality.Disable; QualitySettings.vSyncCount=0; }
            QualitySettings.SetQualityLevel(0,false);
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset");
            if(!renderer.rendererFeatures.Any(x=>x is ARBackgroundRendererFeature)) {
                var feature=ScriptableObject.CreateInstance<ARBackgroundRendererFeature>(); feature.name="AR Camera Background";
                AssetDatabase.AddObjectToAsset(feature,renderer); renderer.rendererFeatures.Add(feature); feature.Create();
            }
            renderer.SetDirty(); EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(pipeline);
        }

        static void ConfigureLayers()
        {
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers=tags.FindProperty("layers"); layers.GetArrayElementAtIndex(8).stringValue="Projectile"; layers.GetArrayElementAtIndex(9).stringValue="Target"; layers.GetArrayElementAtIndex(10).stringValue="Viewmodel";
            tags.ApplyModifiedPropertiesWithoutUndo();
            for(int i=0;i<32;i++) { Physics.IgnoreLayerCollision(8,i,i!=9); Physics.IgnoreLayerCollision(9,i,i!=8); }
            Time.fixedDeltaTime=1f/60f; Time.maximumDeltaTime=.1f;
            var timeSettings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset")[0]);
            timeSettings.FindProperty("Fixed Timestep").floatValue=1f/60f;
            timeSettings.FindProperty("Maximum Allowed Timestep").floatValue=.1f;
            timeSettings.ApplyModifiedPropertiesWithoutUndo();
            // Persist physics settings rather than depending on editor-only Physics calls.
            var physics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")[0]);
            var matrix=physics.FindProperty("m_LayerCollisionMatrix");
            if(matrix!=null && matrix.isArray) for(int i=0;i<32;i++) { uint row=uint.MaxValue; if(i==8)row=1u<<9; else if(i==9)row=1u<<8; else row&=~((1u<<8)|(1u<<9)); matrix.GetArrayElementAtIndex(i).longValue=row; }
            physics.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureImports()
        {
            string[] paths=AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Game_Assets",Root+"Art"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            foreach(var path in paths) {
                if(!(AssetImporter.GetAtPath(path) is TextureImporter ti))continue;
                bool normal=path.IndexOf("Normal",StringComparison.OrdinalIgnoreCase)>=0;
                bool data=normal || new[]{"Rough","Metal","_AO","Noise","Veins","Mask","Height"}.Any(s=>path.IndexOf(s,StringComparison.OrdinalIgnoreCase)>=0);
                ti.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                ti.sRGBTexture=!data; ti.mipmapEnabled=true; ti.maxTextureSize=1024;
                var settings=ti.GetPlatformTextureSettings("Android"); settings.overridden=true; settings.maxTextureSize=1024;
                settings.format=TextureImporterFormat.ETC2_RGBA8; settings.textureCompression=TextureImporterCompression.Compressed; ti.SetPlatformTextureSettings(settings);
                ti.SaveAndReimport();
            }
            foreach(string path in new[]{Root+"Art/Models/Railgun/RailgunUpper.fbx",Root+"Art/Models/EnergyOrb/EnergyOrbShell.fbx"}) {
                var importer=AssetImporter.GetAtPath(path) as ModelImporter; if(!importer)continue;
                importer.importCameras=false; importer.importLights=false; importer.importAnimation=false; importer.isReadable=false; importer.materialImportMode=ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
            }
        }

        static GameBase BuildBase()
        {
            var root=new GameObject("GameBase");
            var pedestal=ImportVisual("Assets/Game_Assets/Base/sci-fi-pedestal/source/pedestal_low.fbx",root.transform,"PedestalVisual");
            Normalize(pedestal,.59f,.057f,0);
            foreach(var r in pedestal.GetComponentsInChildren<Renderer>()) {
                r.sharedMaterials=r.sharedMaterials.Select(m=> {
                    bool grid=m && m.name.IndexOf("Grid",StringComparison.OrdinalIgnoreCase)>=0;
                    string prefix="Assets/Game_Assets/Base/sci-fi-pedestal/textures/pedestal_low_"+(grid?"Under_Grid":"Base");
                    var mat=Lit(grid?"M_PedestalGrid":"M_Pedestal",Color.white,.55f,.5f);
                    SetTexture(mat,"_BaseMap",prefix+"_AlbedoTransparency.png"); SetTexture(mat,"_BumpMap",prefix+"_Normal.png"); mat.EnableKeyword("_NORMALMAP");
                    SetTexture(mat,"_EmissionMap",Root+"Art/Textures/"+(grid?"PedestalGrid_Cyan":"Pedestal_Cyan")+".png");
                    mat.SetColor("_EmissionColor",new Color(.02f,.85f,1)); mat.EnableKeyword("_EMISSION"); return mat;
                }).ToArray();
            }
            var deck=ImportVisual(Root+"Art/Models/Platform/platform/Platform.fbx",root.transform,"PlatformVisual");
            Normalize(deck,.65f,.075f,.055f);
            var platformMat=Lit("M_Platform",new Color(.62f,.72f,.83f),.5f,.4f);
            SetTexture(platformMat,"_BaseMap","Assets/Game_Assets/Base/sci-fi-platform/textures/Base Color.png");
            SetTexture(platformMat,"_BumpMap","Assets/Game_Assets/Base/sci-fi-platform/textures/Normal.png"); platformMat.EnableKeyword("_NORMALMAP");
            foreach(var r in deck.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(platformMat,r.sharedMaterials.Length).ToArray();
            // Restrained unlit deck markings keep the authored mechanical silhouettes readable on a bright camera feed.
            Ring(root.transform,"Outer reactor ring",.258f,.1308f,.003f,cyan);
            Ring(root.transform,"Inner reactor ring",.119f,.1313f,.0028f,cyan);
            Ring(root.transform,"Reactor inset",.091f,.1316f,.0012f,cyan);
            Ring(root.transform,"Anchor footprint",.347f,.001f,.0012f,cyan);
            for(int i=0;i<12;i++) {
                float a=i*Mathf.PI/6;
                var vent=Cube("Cyan vent",root.transform,new Vector3(Mathf.Sin(a)*.302f,.086f,Mathf.Cos(a)*.302f),new Vector3(.048f,.007f,.003f),cyan);
                vent.transform.localRotation=Quaternion.Euler(0,i*30,0);
                if(i%3==0) { var accent=Cube("Amber safety insert",root.transform,new Vector3(Mathf.Sin(a)*.294f,.127f,Mathf.Cos(a)*.294f),new Vector3(.028f,.002f,.009f),amber); accent.transform.localRotation=Quaternion.Euler(0,i*30,0); }
            }
            var slots=new GameObject("TargetSlots").transform; slots.SetParent(root.transform,false);
            var gameBase=root.AddComponent<GameBase>(); gameBase.targetSlots=new Transform[3];
            for(int i=0;i<3;i++) { var t=new GameObject("TargetSlot_"+(i+1)).transform; t.SetParent(slots,false); t.localPosition=new Vector3((i-1)*.23f,i==1?.38f:.29f,i==1?.035f:0); gameBase.targetSlots[i]=t; Ring(root.transform,"Socket "+i,.032f,.133f,.0016f,cyan,new Vector3(t.localPosition.x,0,t.localPosition.z)); }
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"Prefabs/GameBase.prefab").GetComponent<GameBase>(); UnityEngine.Object.DestroyImmediate(root); return prefab;
        }

        static EnergyTarget BuildTarget(Material energy)
        {
            var root=new GameObject("EnergyTarget"); root.layer=9;
            var visual=ImportVisual(Root+"Art/Models/EnergyOrb/EnergyOrbShell.fbx",root.transform,"Source energy shell");
            Normalize(visual,.14f,.14f,-.07f);
            foreach(var r in visual.GetComponentsInChildren<Renderer>()) r.sharedMaterial=energy;
            var collider=root.AddComponent<SphereCollider>(); collider.radius=.07f; collider.isTrigger=false;
            root.AddComponent<EnergyTarget>();
            for(int i=0;i<3;i++) Arc(root.transform,"Cyan energy bracket",.083f,i*120+14,44,cyan);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"Prefabs/EnergyTarget.prefab").GetComponent<EnergyTarget>(); UnityEngine.Object.DestroyImmediate(root); return prefab;
        }

        static Projectile BuildProjectile()
        {
            var go=new GameObject("Projectile");go.layer=8;
            var sphere=go.AddComponent<SphereCollider>();sphere.radius=.012f;
            var body=go.AddComponent<Rigidbody>();body.useGravity=false;body.linearDamping=0;body.angularDamping=0;body.collisionDetectionMode=CollisionDetectionMode.Continuous;
            var v=GameObject.CreatePrimitive(PrimitiveType.Sphere);v.name="Pulse";v.transform.SetParent(go.transform,false);v.transform.localScale=Vector3.one*.023f;UnityEngine.Object.DestroyImmediate(v.GetComponent<Collider>());v.GetComponent<Renderer>().sharedMaterial=white;
            var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=white;trail.time=.065f;trail.startWidth=.012f;trail.endWidth=0;trail.minVertexDistance=.018f;trail.shadowCastingMode=ShadowCastingMode.Off;
            go.AddComponent<Projectile>(); var prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"Prefabs/Projectile.prefab").GetComponent<Projectile>();UnityEngine.Object.DestroyImmediate(go);return prefab;
        }

        static GameObject BuildRailgun()
        {
            var root=new GameObject("RailgunViewmodel");root.layer=10;
            var visual=ImportVisual(Root+"Art/Models/Railgun/RailgunUpper.fbx",root.transform,"Source barrel and upper housing");
            var mat=Lit("M_Railgun",new Color(.68f,.8f,.93f),.58f,.5f); SetTexture(mat,"_BaseMap","Assets/Game_Assets/Weapon/railgun-turret/textures/Railgun turret_Albedo.png");SetTexture(mat,"_BumpMap","Assets/Game_Assets/Weapon/railgun-turret/textures/Railgun turret_Normal.png");mat.EnableKeyword("_NORMALMAP");
            foreach(var r in visual.GetComponentsInChildren<Renderer>()){r.sharedMaterial=mat;r.gameObject.layer=10;r.shadowCastingMode=ShadowCastingMode.Off;}
            for(int i=0;i<2;i++)Cube("Cyan rail strip",root.transform,new Vector3((i==0?-1:1)*.035f,.026f,.015f),new Vector3(.006f,.003f,.22f),cyan);
            root.AddComponent<RailgunPresentation>();
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"Prefabs/RailgunViewmodel.prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
        }

        static ParticleSystem BuildImpact()
        {
            var root=new GameObject("EnergyImpact");var ps=root.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.duration=.4f;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=16;main.startLifetime=new ParticleSystem.MinMaxCurve(.2f,.38f);main.startSpeed=new ParticleSystem.MinMaxCurve(.2f,.65f);main.startSize=new ParticleSystem.MinMaxCurve(.009f,.023f);main.stopAction=ParticleSystemStopAction.Disable;
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(.2f,.85f,1),new Color(1,.15f,.95f));
            var em=ps.emission;em.rateOverTime=0;em.SetBursts(new[]{new ParticleSystem.Burst(0,16)});
            var sh=ps.shape;sh.shapeType=ParticleSystemShapeType.Sphere;sh.radius=.012f;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));
            var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.6f,.1f,1),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});color.color=gradient;
            var pr=root.GetComponent<ParticleSystemRenderer>();var mat=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));mat.name="M_Impact";mat.SetColor("_BaseColor",Color.white);SaveMaterial(mat);
            pr.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/Materials/M_Impact.mat");pr.renderMode=ParticleSystemRenderMode.Stretch;pr.lengthScale=2;pr.velocityScale=.08f;pr.shadowCastingMode=ShadowCastingMode.Off;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"Art/VFX/EnergyImpact.prefab").GetComponent<ParticleSystem>();UnityEngine.Object.DestroyImmediate(root);return prefab;
        }

        static void BuildScene(GameBase b,EnergyTarget target,Projectile projectile,GameObject railgun,ParticleSystem impact)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.57f,.67f,.82f);RenderSettings.skybox=null;
            var light=new GameObject("Soft key light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.35f;light.color=new Color(.83f,.91f,1);light.shadows=LightShadows.None;light.transform.rotation=Quaternion.Euler(43,-32,0);
            var app=new GameObject("Reality Defender");var wave=app.AddComponent<WaveController>();wave.targetPrefab=target;wave.projectilePrefab=projectile;wave.railgunPrefab=railgun;wave.impactPrefab=impact;
            var placement=app.AddComponent<PlacementController>();placement.wave=wave;placement.basePrefab=b;placement.planeMaterial=plane;
            var practiceEnv=new GameObject("Practice environment");var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Virtual training floor";floor.transform.SetParent(practiceEnv.transform,false);floor.transform.localScale=Vector3.one*5;UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
            var floorMat=new Material(Shader.Find("RealityDefender/Practice Floor")){name="M_PracticeFloor"};SaveMaterial(floorMat);floor.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/Materials/M_PracticeFloor.mat");
            var practiceCamera=MakeCamera("Practice Camera",null);practiceCamera.transform.SetPositionAndRotation(new Vector3(0,.85f,-1.2f),Quaternion.Euler(28,0,0));practiceCamera.gameObject.AddComponent<AudioListener>();practiceCamera.tag="MainCamera";
            placement.practiceCamera=practiceCamera;placement.practiceEnvironment=practiceEnv;
            var ar=new GameObject("AR Rig");ar.SetActive(false);
            var sessionGo=new GameObject("AR Session");sessionGo.transform.SetParent(ar.transform,false);var session=sessionGo.AddComponent<ARSession>();session.attemptUpdate=false;session.enabled=false;sessionGo.AddComponent<ARInputManager>();
            var originGo=new GameObject("XR Origin (Mobile AR)");originGo.transform.SetParent(ar.transform,false);var origin=originGo.AddComponent<XROrigin>();
            var offset=new GameObject("Camera Offset");offset.transform.SetParent(originGo.transform,false);origin.CameraFloorOffsetObject=offset;
            var camera=MakeCamera("AR Camera",offset.transform);camera.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();
            camera.gameObject.AddComponent<ARCameraManager>();camera.gameObject.AddComponent<ARCameraBackground>();
            var pose=camera.gameObject.AddComponent<TrackedPoseDriver>();
            var position=new InputAction("AR Position",InputActionType.Value,"<XRHMD>/centerEyePosition",expectedControlType:"Vector3");position.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rotation=new InputAction("AR Rotation",InputActionType.Value,"<XRHMD>/centerEyeRotation",expectedControlType:"Quaternion");rotation.AddBinding("<HandheldARInputDevice>/deviceRotation");
            var tracking=new InputAction("AR Tracking",InputActionType.Value,"<XRHMD>/trackingState",expectedControlType:"Integer");tracking.AddBinding("<HandheldARInputDevice>/trackingState");
            pose.positionInput=new InputActionProperty(position);pose.rotationInput=new InputActionProperty(rotation);pose.trackingStateInput=new InputActionProperty(tracking);
            origin.Camera=camera;origin.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.NotSpecified;origin.CameraYOffset=0f;
            var planes=originGo.AddComponent<ARPlaneManager>();planes.requestedDetectionMode=PlaneDetectionMode.Horizontal;
            placement.arRig=ar;placement.session=session;placement.origin=origin;placement.arCamera=camera;placement.planeManager=planes;placement.raycastManager=originGo.AddComponent<ARRaycastManager>();placement.anchorManager=originGo.AddComponent<ARAnchorManager>();
            var es=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var hud=app.AddComponent<RealityHud>();hud.wave=wave;hud.placement=placement;
            ConfigureArStability();
            EditorSceneManager.SaveScene(scene,Root+"Scenes/RealityDefender.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"Scenes/RealityDefender.unity",true)};
        }

        static Camera MakeCamera(string name,Transform parent)
        {
            var go=new GameObject(name);if(parent)go.transform.SetParent(parent,false);var c=go.AddComponent<Camera>();c.nearClipPlane=.025f;c.farClipPlane=50;c.fieldOfView=58;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.012f,.025f,.045f);c.allowHDR=false;c.allowMSAA=false;go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;return c;
        }
        static GameObject ImportVisual(string path,Transform parent,string name)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!source)throw new Exception("Missing model "+path);
            var pivot=new GameObject(name);pivot.transform.SetParent(parent,false);var model=UnityEngine.Object.Instantiate(source,pivot.transform);model.name=source.name;
            foreach(var c in model.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
            foreach(var r in model.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
            return pivot;
        }
        static void Normalize(GameObject go,float diameter,float height,float bottom)
        {
            var rs=go.GetComponentsInChildren<Renderer>();if(rs.Length==0)throw new Exception("No visible mesh "+go.name);
            Bounds b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            go.transform.localScale=new Vector3(diameter/Mathf.Max(b.size.x,b.size.z),height/b.size.y,diameter/Mathf.Max(b.size.x,b.size.z));
            b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            go.transform.localPosition+=new Vector3(-b.center.x,bottom-b.min.y,-b.center.z);
            Debug.Log("MODEL_BOUNDS "+go.name+" "+b.size);
        }
        static Material Lit(string name,Color color,float metal,float smooth)
        { var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);SaveMaterial(m);return AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/Materials/"+name+".mat"); }
        static Material Unlit(string name,Color color)
        { var m=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name=name};m.SetColor("_BaseColor",color);SaveMaterial(m);return AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/Materials/"+name+".mat"); }
        static void SaveMaterial(Material m)
        { string path=Root+"Art/Materials/"+m.name+".mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old){EditorUtility.CopySerialized(m,old);UnityEngine.Object.DestroyImmediate(m);}else AssetDatabase.CreateAsset(m,path); }
        static void SetTexture(Material mat,string property,string path) { var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(tex)mat.SetTexture(property,tex);EditorUtility.SetDirty(mat); }
        static GameObject Cube(string name,Transform parent,Vector3 pos,Vector3 scale,Material material)
        { var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=material;return g; }
        static void Ring(Transform parent,string name,float radius,float y,float width,Material mat,Vector3 offset=default)
        { var g=new GameObject(name);g.transform.SetParent(parent,false);var l=g.AddComponent<LineRenderer>();l.useWorldSpace=false;l.loop=true;l.positionCount=96;l.widthMultiplier=width;l.sharedMaterial=mat;l.shadowCastingMode=ShadowCastingMode.Off;for(int i=0;i<96;i++){float a=i*2*Mathf.PI/96;l.SetPosition(i,offset+new Vector3(Mathf.Sin(a)*radius,y,Mathf.Cos(a)*radius));} }
        static void Arc(Transform parent,string name,float radius,float start,float length,Material mat)
        { var g=new GameObject(name);g.transform.SetParent(parent,false);var l=g.AddComponent<LineRenderer>();l.useWorldSpace=false;l.positionCount=16;l.widthMultiplier=.0014f;l.sharedMaterial=mat;for(int i=0;i<16;i++){float a=(start+length*i/15f)*Mathf.Deg2Rad;l.SetPosition(i,new Vector3(Mathf.Sin(a)*radius,Mathf.Cos(a)*radius,0));} }
    }
}
