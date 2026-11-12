using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RealityDefender.Editor
{
    /// <summary>Creates the editable target graph with Shader Graph's own graph/node serializer.</summary>
    public static class EnergyShaderBuilder
    {
        public const string GraphPath = "Assets/RealityDefender/Art/Shaders/SG_EnergyOrb.shadergraph";
        public const string TexturePath = "Assets/RealityDefender/Art/Textures/EnergyVeins.png";
        public const string MaterialPath = "Assets/RealityDefender/Art/Materials/M_EnergyOrb.mat";
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        static Assembly shaderGraph;
        static object graph;
        static object category;

        [MenuItem("Reality Defender/Rebuild Energy Shader Graph")]
        public static void BuildMenu() => Build();

        public static Material Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GraphPath));
            Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            GenerateVeinTexture();
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 128;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            var noiseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

            shaderGraph = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetType("UnityEditor.ShaderGraph.GraphData") != null);
            graph = New("GraphData");
            Call(graph, "AddContexts");
            Set(graph, "path", "Reality Defender");
            var universalAssembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget") != null);
            var targetType = universalAssembly.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget");
            var target = Activator.CreateInstance(targetType, true);
            Call(target, "TrySetActiveSubTarget", universalAssembly.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalUnlitSubTarget"));
            SetEnum(target, "surfaceType", "Opaque");
            SetEnum(target, "renderFace", "Front");
            Set(target, "alphaClip", false);
            var targets = Array.CreateInstance(Type("Target"), 1);
            targets.SetValue(target, 0);
            var blockFields = Type("BlockFields");
            var descriptors = new[] {
                blockFields.GetNestedType("VertexDescription", Flags).GetField("Position", Flags).GetValue(null),
                blockFields.GetNestedType("VertexDescription", Flags).GetField("Normal", Flags).GetValue(null),
                blockFields.GetNestedType("VertexDescription", Flags).GetField("Tangent", Flags).GetValue(null),
                blockFields.GetNestedType("SurfaceDescription", Flags).GetField("BaseColor", Flags).GetValue(null)
            };
            var descriptorArray = Array.CreateInstance(Type("BlockFieldDescriptor"), descriptors.Length);
            for (int i = 0; i < descriptors.Length; i++) descriptorArray.SetValue(descriptors[i], i);
            Call(graph, "InitializeOutputs", targets, descriptorArray);
            category = New("CategoryData");
            Set(category, "name", "Energy surface");
            Call(graph, "AddCategory", category);

            object scrollGroup = Group("01  SCROLLING VEINS / UV0 + TIME", -1250, -420);
            object bodyGroup = Group("02  COBALT CORE + MAGENTA VEINS", -140, -420);
            object rimGroup = Group("03  WORLD-SPACE FRESNEL RIM", 570, 360);
            object finishGroup = Group("04  ENERGY OUTPUT / OPAQUE UNLIT", 1420, -80);

            var uv = Node("UVNode", -1200, -350, scrollGroup);
            var tiling = Property("Vector2ShaderProperty", "Tiling", "_Tiling", new Vector4(2, 2, 0, 0), -1200, -100, scrollGroup);
            var tile = Node("MultiplyNode", -950, -350, scrollGroup);
            Edge(uv, 0, tile, 0); Edge(tiling, 0, tile, 1);
            var time = Node("TimeNode", -1200, 130, scrollGroup);
            var scroll = Property("Vector2ShaderProperty", "Scroll", "_Scroll", new Vector4(.18f, .10f, 0, 0), -1200, 380, scrollGroup);
            var timeScroll = Node("MultiplyNode", -950, 130, scrollGroup);
            Edge(time, 0, timeScroll, 0); Edge(scroll, 0, timeScroll, 1);
            var add = Node("AddNode", -680, -160, scrollGroup);
            Edge(tile, 2, add, 0); Edge(timeScroll, 2, add, 1);
            var noiseProperty = New("Internal.Texture2DShaderProperty");
            Set(noiseProperty, "displayName", "Noise"); Set(noiseProperty, "overrideReferenceName", "_Noise");
            var serializableTexture = Get(noiseProperty, "value"); Set(serializableTexture, "texture", noiseTexture);
            Call(graph, "AddGraphInput", noiseProperty, -1); Call(category, "InsertItemIntoCategory", noiseProperty, -1);
            var noise = Node("PropertyNode", -680, 140, scrollGroup); Set(noise, "property", noiseProperty);
            var sample = Node("SampleTexture2DNode", -420, -160, scrollGroup);
            Edge(noise, 0, sample, 1); Edge(add, 2, sample, 2);

            var soft = Node("SmoothstepNode", -90, -350, bodyGroup);
            SlotValue(soft, 0, .22f); SlotValue(soft, 1, .75f); Edge(sample, 4, soft, 2);
            var dark = Property("ColorShaderProperty", "CoreDark", "_CoreDark", new Color(.008f,.018f,.09f,1), -90, -60, bodyGroup);
            var bright = Property("ColorShaderProperty", "CoreBright", "_CoreBright", new Color(.09f,.035f,.38f,1), -90, 160, bodyGroup);
            var core = Node("LerpNode", 160, -150, bodyGroup);
            Edge(dark, 0, core, 0); Edge(bright, 0, core, 1); Edge(soft, 3, core, 2);
            var ridge = Node("SmoothstepNode", 160, -470, bodyGroup);
            SlotValue(ridge, 0, .74f); SlotValue(ridge, 1, .96f); Edge(sample, 4, ridge, 2);
            var vein = Property("ColorShaderProperty", "VeinColor", "_VeinColor", new Color(.95f,.08f,1,1), 160, 120, bodyGroup);
            var body = Node("LerpNode", 430, -150, bodyGroup);
            Edge(core, 3, body, 0); Edge(vein, 0, body, 1); Edge(ridge, 3, body, 2);

            var rimPower = Property("Vector1ShaderProperty", "RimPower", "_RimPower", 2.2f, 620, 420, rimGroup);
            var fresnel = Node("FresnelNode", 870, 420, rimGroup);
            Edge(rimPower, 0, fresnel, 2);
            var strength = Property("Vector1ShaderProperty", "RimStrength", "_RimStrength", 1.0f, 620, 650, rimGroup);
            var rimMultiply = Node("MultiplyNode", 1120, 420, rimGroup);
            Edge(fresnel, 3, rimMultiply, 0); Edge(strength, 0, rimMultiply, 1);
            var saturate = Node("SaturateNode", 1370, 420, rimGroup);
            Edge(rimMultiply, 2, saturate, 0);
            var rimColor = Property("ColorShaderProperty", "RimColor", "_RimColor", new Color(.04f,.88f,1,1), 1470, 100, finishGroup);
            var final = Node("LerpNode", 1720, -10, finishGroup);
            Edge(body, 3, final, 0); Edge(rimColor, 0, final, 1); Edge(saturate, 1, final, 2);
            var fragment = Get(graph, "fragmentContext");
            Set(fragment, "position", new Vector2(2040, -10));
            Set(Get(graph, "vertexContext"), "position", new Vector2(2040, -350));
            var blocks = (IEnumerable)Get(fragment, "blocks");
            object baseColor = null;
            foreach (object reference in blocks)
            {
                var block = Get(reference, "value");
                if (block != null && ((string)Get(block, "name")).Contains("BaseColor")) { baseColor = block; break; }
            }
            if (baseColor == null) throw new InvalidOperationException("Shader Graph did not create Fragment Base Color.");
            Edge(final, 3, baseColor, 0);
            Call(graph, "ValidateGraph");
            string serialized = (string)Type("Serialization.MultiJson").GetMethod("Serialize", Flags).Invoke(null, new[] {graph});
            File.WriteAllText(GraphPath, serialized);
            AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
            if (!shader) throw new InvalidOperationException("Energy Shader Graph did not import a shader.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
            else material.shader = shader;
            material.SetTexture("_Noise", noiseTexture);
            material.SetVector("_Tiling", new Vector4(2, 2, 0, 0));
            material.SetVector("_Scroll", new Vector4(.18f, .10f, 0, 0));
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log("Reality Defender: created editable SG_EnergyOrb with 29 connected standard nodes, one noise sample, scrolling UVs and world-space Fresnel.");
            return material;
        }

        static object Group(string name, float x, float y)
        {
            var group = New("GroupData"); Set(group, "title", name); Set(group, "position", new Vector2(x,y));
            Call(graph, "CreateGroup", group); return group;
        }
        static object Node(string type, float x, float y, object group)
        {
            object node = New(type);
            object state = Get(node, "drawState"); Set(state, "position", new Rect(x,y,200,150)); Set(state, "expanded", true); Set(node, "drawState", state);
            Call(graph, "AddNode", node, false);
            if (group != null) Call(graph, "SetGroup", node, group);
            return node;
        }
        static object Property(string type, string label, string reference, object value, float x, float y, object group)
        {
            var property = New("Internal."+type);
            Set(property, "displayName", label); Set(property, "overrideReferenceName", reference); Set(property, "value", value);
            Call(graph, "AddGraphInput", property, -1); Call(category, "InsertItemIntoCategory", property, -1);
            var node = Node("PropertyNode", x,y,group); Set(node, "property", property); return node;
        }
        static void Edge(object from, int output, object to, int input) => Call(graph, "Connect", Call(from,"GetSlotReference",output), Call(to,"GetSlotReference",input));
        static void SlotValue(object node, int id, float value)
        {
            var method = node.GetType().GetMethods(Flags).First(m => m.Name == "FindSlot" && m.IsGenericMethodDefinition);
            var slot = method.MakeGenericMethod(Type("MaterialSlot")).Invoke(node,new object[]{id});
            var property = FindProperty(slot.GetType(), "value");
            object converted = property.PropertyType == typeof(Vector4) ? (object)new Vector4(value,value,value,value) : value;
            property.SetValue(slot, converted);
        }
        static Type Type(string name) => shaderGraph.GetType("UnityEditor.ShaderGraph."+name,true);
        static object New(string name) => Activator.CreateInstance(Type(name),true);
        static PropertyInfo FindProperty(Type type, string name)
        {
            for (; type != null; type = type.BaseType) { var p=type.GetProperty(name,Flags | BindingFlags.DeclaredOnly); if (p!=null) return p; }
            throw new MissingMemberException(name);
        }
        static object Get(object obj, string name) => FindProperty(obj.GetType(), name).GetValue(obj);
        static void Set(object obj, string name, object value) => FindProperty(obj.GetType(), name).SetValue(obj,value);
        static void SetEnum(object obj, string name, string value) { var p=FindProperty(obj.GetType(),name); p.SetValue(obj,Enum.Parse(p.PropertyType,value)); }
        static object Call(object obj, string name, params object[] args)
        {
            var method = obj.GetType().GetMethods(Flags).FirstOrDefault(m => m.Name==name && !m.IsGenericMethodDefinition && m.GetParameters().Length==args.Length && m.GetParameters().Select((p,i)=>args[i]==null || p.ParameterType.IsInstanceOfType(args[i])).All(v=>v));
            if (method==null) throw new MissingMethodException(obj.GetType().FullName,name);
            return method.Invoke(obj,args);
        }

        static void GenerateVeinTexture()
        {
            const int size=128, count=37;
            var random=new System.Random(82741);
            var seeds=new Vector2[count];
            for (int i=0;i<count;i++) seeds[i]=new Vector2((float)random.NextDouble(),(float)random.NextDouble());
            var texture=new Texture2D(size,size,TextureFormat.RGB24,false,true);
            var pixels=new Color32[size*size];
            for (int y=0;y<size;y++) for (int x=0;x<size;x++)
            {
                float u=x/(float)size, v=y/(float)size;
                float wu=u+.032f*Mathf.Sin(v*Mathf.PI*8+.6f*Mathf.Sin(u*Mathf.PI*4));
                float wv=v+.03f*Mathf.Sin(u*Mathf.PI*6+.8f*Mathf.Sin(v*Mathf.PI*4));
                float first=10,second=10;
                foreach (var seed in seeds)
                {
                    float dx=Mathf.Abs(wu-seed.x); dx=Mathf.Abs(dx-Mathf.Round(dx));
                    float dy=Mathf.Abs(wv-seed.y); dy=Mathf.Abs(dy-Mathf.Round(dy));
                    float distance=Mathf.Sqrt(dx*dx+dy*dy);
                    if(distance<first){second=first;first=distance;}else if(distance<second)second=distance;
                }
                float ridge=Mathf.Exp(-(second-first)*80f);
                float softness=.15f+.2f*(.5f+.5f*Mathf.Sin(u*Mathf.PI*4+Mathf.Cos(v*Mathf.PI*6)));
                byte value=(byte)(255*Mathf.Clamp01(softness+.86f*ridge));
                pixels[y*size+x]=new Color32(value,value,value,255);
            }
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(TexturePath,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
