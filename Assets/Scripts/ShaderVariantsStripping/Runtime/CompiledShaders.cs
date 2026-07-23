using System;
using System.Collections.Generic;
using System.Security.Policy;
using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine.Rendering;
#endif

namespace ShaderVariantStripping
{
    [CreateAssetMenu(fileName = nameof(CompiledShaders), menuName = nameof(CompiledShaders))]
    public class CompiledShaders : ScriptableObject
    {
        public List<CompiledShaderData> Shaders = new();
        public bool shouldStrip = true;
        public bool checkStage = false;
        public int totalShaders, totalVariants;

        public void UpdateCounts()
        {
            totalShaders = Shaders.Count;
            totalVariants = 0;
            foreach (CompiledShaderData data in Shaders)
            {
                totalVariants += data.Variants.Count;
            }
        }
    }

    [Serializable]
    public class CompiledShaderData
    {
        public string ShaderName;
        public List<CompiledVariantData> Variants = new();

#if UNITY_EDITOR
        public bool ContainsVariant(string shaderName, string pass, string stage, string[] keywords)
        {
            if (ShaderName != shaderName)
                return false;
            
            // debug
            if(shaderName.Equals("Boat Attack/UI/Halftone Fade"))
            {
                Debug.Log("Boat Attack/UI/Halftone Fade");
            }
            
            if(stage == null)
                return Variants.Any(v => v.EqualsTo(pass, keywords));
            else
                return Variants.Any(v => v.EqualsTo(pass, stage, keywords));
        }
#endif
    }

    [Serializable]
    public class CompiledVariantData
    {
        public string Pass; 
        public string Stage;
        public string[] Keywords;   // sorted
        private static readonly string[] k_NoPassNames = new[] { "unnamed", "<unnamed>", "<Unnamed Pass 0>", "<Unnamed Pass 1>", "<Unnamed Pass 2>" }; // 2019.x uses: <unnamed>, whilst 2020.x uses unnamed

#if UNITY_EDITOR
        public bool EqualsTo(string pass, string[] keywords)
        {
            if (!CheckPassString(pass))
                return false;

            return Enumerable.SequenceEqual(Keywords, keywords);
        }

        public bool EqualsTo(string pass, string stage, string[] keywords)
        {
            if (!CheckPassString(pass))
                return false;

            if (!CheckStageString(stage))
                return false;

            return Enumerable.SequenceEqual(Keywords, keywords);
        }

        private bool CheckPassString(string pass)
        {
            var passMatch = Pass.Equals(pass, StringComparison.OrdinalIgnoreCase);
            if (!passMatch)
            {
                // var isUnnamed = k_NoPassNames.Contains(Pass);
                // passMatch = isUnnamed && string.IsNullOrEmpty(pass);
                passMatch = k_NoPassNames.Contains(Pass) &&
                            (k_NoPassNames.Contains(pass) || string.IsNullOrEmpty(pass));
            }

            return passMatch;
        }

        private bool CheckStageString(string stage)
        {
            return Stage.Equals(stage, StringComparison.OrdinalIgnoreCase);
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(CompiledShaders))]
    public class CompiledShadersEditor : Editor
    {
        const string k_InternalErrorShader = "Hidden/InternalErrorShader";
        const string k_NoKeywords = "<no keywords>";
        static readonly string k_StringSeparator = ", ";
        static readonly Dictionary<string, string> k_StageNameMap = new Dictionary<string, string>()
        {
            { "all", "vertex" },       // GLES* / OpenGLCore
            { "pixel", "fragment" }    // Metal
        };

        // Older Unity versions use the first string in their log, new versions use the second.
        // Rather than trying to identify the specific version when the change occurred, we'll just check both.
        static readonly string[] k_CompiledShaderPrefixes = { "Compiled shader: ", "Uploaded shader variant to the GPU driver: " };

        bool m_AppendVariants = false;
        ShaderVariantCollection m_Svc;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Collect compiled shaders from profiler"))
            {
                CollectFromProfilerRawData(((CompiledShaders)target).Shaders);
                EditorUtility.SetDirty(target);
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Drag & Drop the log file here to load compiled shaders.", MessageType.Info);
            Rect dropArea = GUILayoutUtility.GetLastRect();
            HandleDragAndDrop(dropArea);

            EditorGUILayout.Space();
            m_Svc = (ShaderVariantCollection)EditorGUILayout.ObjectField("ShaderVariantCollection", m_Svc, typeof(ShaderVariantCollection), false);
            m_AppendVariants = GUILayout.Toggle(m_AppendVariants, "Append variants");
            using (new EditorGUI.DisabledScope(m_Svc == null))
            {
                if (GUILayout.Button("Export to ShaderVariantCollection"))
                {
                    if (!m_AppendVariants)
                    {
                        m_Svc.Clear();
                    }

                    ExportVariantsToSvc(m_Svc, ((CompiledShaders)target).Shaders);
                }
            }
        }

        void HandleDragAndDrop(Rect dropArea)
        {
            Event evt = Event.current;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
                        {
                            var logFile = DragAndDrop.paths[0];
                            var shaders = ((CompiledShaders)target).Shaders;    // Shaders: List<CompiledShaderData>
                            ParsePlayerLog(logFile, shaders);
                            ((CompiledShaders)target).UpdateCounts();
                            EditorUtility.SetDirty(target);
                        }
                    }
                    Event.current.Use();
                    break;
            }
        }

        public static void CollectFromProfilerRawData(List<CompiledShaderData> shaders)
        {
            for (int f = ProfilerDriver.firstFrameIndex; f < ProfilerDriver.lastFrameIndex; f++)
            {
                using (var hfdv = ProfilerDriver.GetHierarchyFrameDataView(f, 0,
                           HierarchyFrameDataView.ViewModes.HideEditorOnlySamples, 0,
                           true))
                {
                    // start searching for Shader.CreateGPUProgram calls from
                    // the root item id in the Hierarchy Frame Data View
                    FindShaderCallInChildren(hfdv, hfdv.GetRootItemID(), shaders);
                }
            }
        }

        static void FindShaderCallInChildren(HierarchyFrameDataView fdv, int id, List<CompiledShaderData> shaders)
        {
            if (fdv.GetItemName(id) == "Shader.CreateGPUProgram")
            {
                // if we are a Shader.CreateGPUProgram marker, get marker metadata info to populate the VariantInfo struct
                int markerId = fdv.GetItemMarkerID(id);
                FrameDataView.MarkerMetadataInfo[] markerInfos = fdv.GetMarkerMetadataInfo(markerId);

                for (int i = 0; i < fdv.GetItemMergedSamplesCount(id); i++)
                {
                    string[] parts = new string[4];

                    for (int m = 0; m < fdv.GetItemMergedSamplesMetadataCount(id, i); m++)
                    {
                        switch (markerInfos[m].name)
                        {
                            case "Shader":
                                parts[0] = fdv.GetItemMergedSamplesMetadata(id, i, m);
                                // debug
                                // if (parts[0].Contains("BlitCopy"))
                                // {
                                //     Debug.Log($"Found BlitCopy shader: {fdv.frameIndex}");
                                // }
                                break;
                            case "Pass":
                                parts[1] = fdv.GetItemMergedSamplesMetadata(id, i, m);
                                break;
                            case "Stage":
                                parts[2] = fdv.GetItemMergedSamplesMetadata(id, i, m);
                                break;
                            case "Keywords":
                                parts[3] = fdv.GetItemMergedSamplesMetadata(id, i, m);
                                break;
                            default:
                                break;
                        }
                    }

                    if (parts.Any(p => p == null))
                    {
                        Debug.LogError("Malformed shader compilation log info: " + id);
                        continue;
                    }

                    InsertCompiledShaderData(shaders,
                        shaderName: parts[0],
                        pass: parts[1],
                        stage: parts[2].ToLower(),
                        keywordsString: parts[3]);
                }
            }
            else if (fdv.HasItemChildren(id))
            {
                // else, look for other markers, maybe we'll find another Shader.CreateGPUProgram somewhere.
                List<int> children = new List<int>();
                fdv.GetItemChildren(id, children);
                foreach (var childId in children)
                {
                    FindShaderCallInChildren(fdv, childId, shaders);
                }
            }
        }

        public static bool ParsePlayerLog(string logFile, List<CompiledShaderData> shaders)
        {
            var lines = GetCompiledShaderLines(logFile);

            if (lines == null)
                return false;

            foreach (var line in lines)
            {
                // ios 
                // "Uploaded shader variant to the GPU driver: Hidden/Internal-GUITexture (instance 0x3E), pass: <Unnamed Pass 0>, stage: vertex, keywords <no keywords>, time: 1.5 ms"
                // Android 
                // "Uploaded shader variant to the GPU driver: Hidden/Internal-GUITexture (instance 0x3E), pass: <Unnamed Pass 0>, keywords <no keywords>, time: 7.6 ms"
                // "Uploaded shader variant to the GPU driver: Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass) (instance 0x184), pass: ForwardLit, stage: all, keywords INSTANCING_ON LIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS, time: 2.7 ms"
                
                var parts = line.Split(new[] { ", pass: ", ", keywords " }, StringSplitOptions.None);
                if (parts.Length != 3)
                {
                    Debug.LogError("Malformed shader compilation log info: " + line);
                    continue;
                }
                
                // shader name filtering
                var shaderName = parts[0];
                int idx = shaderName.IndexOf(" (instance");
                if (idx >= 0)
                    shaderName = shaderName.Remove(idx, shaderName.Length - idx).Trim();
                
                // check if stage info is included
                var passAndStage = parts[1];
                var pass = "";
                var stage = ""; // default
                idx = passAndStage.IndexOf(", stage: ");
                if (idx >= 0)
                {
                    pass = passAndStage.Remove(idx, passAndStage.Length - idx).Trim();
                    stage = ((passAndStage.Split(", stage: ", StringSplitOptions.None))[1]).Trim().ToLower();
                }
                else
                {
                    pass = parts[1];
                }

                // delete time info
                var keywords = parts[2];
                idx = keywords.IndexOf(", time: ");
                if (idx >= 0)
                    keywords = keywords.Remove(idx, keywords.Length - idx).Trim();

                InsertCompiledShaderData(shaders, 
                    shaderName: shaderName,
                    pass: pass,
                    stage: stage,
                    keywordsString: keywords); 
            }
            return true;
        }

        static void InsertCompiledShaderData(List<CompiledShaderData> shaders, string shaderName, string pass, string stage, string keywordsString)
        {
            var keywords = SplitKeywords(keywordsString, " ").OrderBy(k => k).ToArray();

            // fix-up stage to be consistent with built variants stage
            // AOS: "all" -> "vertex"
            if (k_StageNameMap.ContainsKey(stage))
                stage = k_StageNameMap[stage];

            // add this variant
            var shader = shaders.FirstOrDefault(s => s.ShaderName == shaderName);

            if (shader == null)
            {
                shader = new CompiledShaderData() { ShaderName = shaderName };
                shaders.Add(shader);
            }

            if (!shader.Variants.Any(v => v.EqualsTo(pass, stage, keywords)))
            {
                shader.Variants.Add(new CompiledVariantData
                {
                    Pass = pass,
                    Stage = stage,
                    Keywords = keywords
                });
            }
            else
            {
                Debug.LogWarning($"[InsertCompiledShaderData]Duplicated CompiledVariantData: {shaderName}, {pass}, {stage}, {string.Join(" ", keywords)}");
            }
        }

        static string[] GetCompiledShaderLines(string logFile)
        {
            var compilationLines = new List<string>();

            try
            {
                using (var file = new StreamReader(logFile))
                {
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        for (int i = 0; i < k_CompiledShaderPrefixes.Length; ++i)
                        {
                            var compilationLogIndex = line.IndexOf(k_CompiledShaderPrefixes[i], StringComparison.Ordinal);
                            if (compilationLogIndex >= 0)
                            {
                                compilationLines.Add(line.Substring(compilationLogIndex + k_CompiledShaderPrefixes[i].Length));
                                break;
                            }
                        }
                    }
                }

                return compilationLines.ToArray();
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
                return null;
            }
        }

        static string[] SplitKeywords(string keywordsString, string separator = null)
        {
            if (keywordsString.Equals(k_NoKeywords))
                return new string[] { };
            return SplitStrings(keywordsString, separator);
        }

        static string[] SplitStrings(string combinedString, string separator = null)
        {
            return combinedString.Split(new[] { separator ?? k_StringSeparator }, StringSplitOptions.None);
        }

        public static void ExportVariantsToSvc(ShaderVariantCollection svc, List<CompiledShaderData> shaders)
        {
            var lightModeTagId = new ShaderTagId("LightMode");

            foreach (var compiledShaderData in shaders)
            {
                if (compiledShaderData == null || compiledShaderData.ShaderName == k_InternalErrorShader)
                    continue;

                var shader = Shader.Find(compiledShaderData.ShaderName);

                if (shader == null)
                    continue;

                // Loop through subshaders and passes to get the specific data we want
                for (int subshader = 0; subshader < shader.subshaderCount; subshader++)
                {
                    for (int passIdx = 0; passIdx < shader.GetPassCountInSubshader(subshader); passIdx++)
                    {
                        var shaderData = ShaderUtil.GetShaderData(shader);
                        var passData = shaderData.GetSubshader(subshader).GetPass(passIdx);
                        var variantDatas = compiledShaderData.Variants.Where(v => v.Pass == passData.Name);

                        foreach (var variantData in variantDatas)
                        {
                            ShaderTagId lightMode = passData.FindTagValue(lightModeTagId);
                            // META light mode is only used in the editor
                            if (lightMode.name == "META")
                                continue;

                            var passType = GetPassType(lightMode.name);

                            // Add the variant to the collection
                            try
                            {
                                svc.Add(new ShaderVariantCollection.ShaderVariant(shader, passType, variantData.Keywords));
                            }
                            catch (ArgumentException ex)
                            {
                                Debug.LogError($"{compiledShaderData.ShaderName}, {variantData.Pass}, {lightMode.name}, {passType}");
                            }
                        }
                    }
                }
            }

            AssetDatabase.SaveAssetIfDirty(svc);
        }

        static PassType GetPassType(string passName)
        {
            return passName switch
            {
                "" => PassType.Normal,
                "Always" => PassType.ScriptableRenderPipelineDefaultUnlit,
                "ForwardBase" => PassType.ForwardBase,
                "ForwardAdd" => PassType.ForwardAdd,
                "Deferred" => PassType.Deferred,
                "ShadowCaster" => PassType.ShadowCaster,
                "SHADOWCASTER" => PassType.ShadowCaster,
                "MotionVectors" or "MOTIONVECTORS" => PassType.MotionVectors,
                // These next five are legacy
                "Vertex" => PassType.Vertex,
                "VertexLM" => PassType.VertexLM,
                "VertexLMRGBM" => PassType.VertexLM,
                "PrePassBase" => PassType.LightPrePassBase,
                "PrePassFinal" => PassType.LightPrePassFinal,
                // The default, unknown case
                _ => PassType.ScriptableRenderPipeline
            };
        }
    }
#endif
}
