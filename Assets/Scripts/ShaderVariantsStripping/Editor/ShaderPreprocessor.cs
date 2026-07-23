using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShaderVariantStripping.Editor
{
    public sealed class ShaderPreprocessor : IPreprocessShaders
    {
        private static readonly string[] s_StrippingTarget =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Hidden/Universal Render Pipeline/UberPost",
            // add additional shader names to be stripped
        };
        
        private static readonly string[] s_StrippingWhitelist =
        {
            "Hidden/Universal Render Pipeline/CameraMotionBlur",
            "Hidden/Universal/CoreBlit",
            // add additional shader names to be whitelisted
        };

        public int callbackOrder => 100;  // after the built-in urp preprocessor

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            // PrintProcessShaderInfo("", shader, snippet, data);
            // return;
            // if (!s_StrippingTarget.Contains(shader.name))
            // {
            //     // Skip variant stripping if the shader is not included in the target list
            //     return;
            // }

            if (s_StrippingWhitelist.Contains(shader.name))
            {
                Debug.Log($"Skip stripping of the whitelisted shader: {shader.name}");
                return;
            }
            
            var compiledShaders = AssetDatabase.LoadAssetAtPath<CompiledShaders>("Assets/Scripts/ShaderVariantsStripping/Editor/CompiledShaders_iOS_Profiler.asset");
            if (compiledShaders == null)
            {
                Debug.LogError("Can't find the CompiledShaders asset");
                return;
            }

            if (!compiledShaders.shouldStrip)
                return;

            var compiledShader = compiledShaders.Shaders.FirstOrDefault(s => s.ShaderName == shader.name);

            if (compiledShader == null)
            {
                // Skip variant stripping if the shader is not collected
                Debug.LogWarning($"Not collected shader:{shader.name}. skipping...");
                return;
            }

            for (int i = data.Count - 1; i >= 0; --i)
            {
                var sortedKeywords = data[i].shaderKeywordSet.GetShaderKeywords()
                    .Select(s => s.name)
                    .Where(k => !string.IsNullOrEmpty(k))
                    .OrderBy(k => k)
                    .ToArray();

                if (!compiledShader.ContainsVariant(shader.name, snippet.passName, compiledShaders.checkStage ? snippet.shaderType.ToString().ToLower() : null, sortedKeywords))
                {
                    // Strip this, it is not in collected in CompiledShaders list
                    PrintProcessShaderInfo("(Stripped)", shader, snippet, new List<ShaderCompilerData>{data[i]});
                    data.RemoveAt(i);
                }
                else
                {
                    PrintProcessShaderInfo("(Built)", shader, snippet, new List<ShaderCompilerData>{data[i]});
                }
            }
        }

        private const string k_ShaderVariantsFilePath = "Assets/NewShaderVariants.shadervariants.shadervariants";

        // The .shadervariants YAML stores each shader as a {fileID, guid} reference,
        // and each variant only as (keywords, passType). Pass name and shader stage
        // are NOT serialized in the file — they only exist at compile time via
        // ShaderSnippetData (snippet.passName / snippet.shaderType) in OnProcessShader.
        // For stripping, match on shader name + PassType + keywords.
        [MenuItem("Tools/Shader Variant Stripping/Print ShaderVariants File Info")]
        public static void PrintShaderVariantsFileInfo()
        {
            if (!File.Exists(k_ShaderVariantsFilePath))
            {
                Debug.LogError($"ShaderVariants file not found: {k_ShaderVariantsFilePath}");
                return;
            }

            string[] lines = File.ReadAllLines(k_ShaderVariantsFilePath);

            var shaderRefRegex = new Regex(@"first:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-fA-F]+)");
            var shaderNameCache = new Dictionary<string, string>();

            StringBuilder sb = null;
            string keywordsBuffer = null;
            int shaderCount = 0;
            int totalVariantCount = 0;
            int variantCountForShader = 0;

            void FlushShaderLog()
            {
                if (sb == null)
                {
                    return;
                }
                sb.Append($"Variant Count: {variantCountForShader.ToString()}\n");
                sb.Append("====================================================\n");
                Debug.Log(sb.ToString());
            }

            foreach (string rawLine in lines)
            {
                Match shaderRef = shaderRefRegex.Match(rawLine);
                if (shaderRef.Success)
                {
                    FlushShaderLog();

                    long fileID = long.Parse(shaderRef.Groups[1].Value);
                    string guid = shaderRef.Groups[2].Value;
                    string cacheKey = $"{guid}:{fileID.ToString()}";
                    if (!shaderNameCache.TryGetValue(cacheKey, out string shaderName))
                    {
                        shaderName = ResolveShaderName(guid, fileID);
                        shaderNameCache.Add(cacheKey, shaderName);
                    }

                    ++shaderCount;
                    variantCountForShader = 0;
                    keywordsBuffer = null;
                    sb = new StringBuilder();
                    sb.Append("============== ShaderVariantCollection Entry =======\n");
                    sb.Append($"Shader Name: {shaderName} (guid: {guid}, fileID: {fileID.ToString()})\n");
                    continue;
                }

                string trimmed = rawLine.Trim();

                if (trimmed.StartsWith("- keywords:"))
                {
                    keywordsBuffer = trimmed.Substring("- keywords:".Length).Trim();
                }
                else if (trimmed.StartsWith("passType:"))
                {
                    int passTypeValue = int.Parse(trimmed.Substring("passType:".Length).Trim());
                    var passType = (PassType)passTypeValue;
                    string keywords = string.IsNullOrEmpty(keywordsBuffer) ? "<no keyword>" : keywordsBuffer;

                    // PassName / ShaderType(Stage) are not stored in .shadervariants files
                    sb?.Append($"PassType(LightMode): {passType.ToString()}, PassName: <n/a>, Stage: <n/a>, Keywords: {keywords}\n");

                    ++variantCountForShader;
                    ++totalVariantCount;
                    keywordsBuffer = null;
                }
                else if (keywordsBuffer != null)
                {
                    // Long keyword lists wrap onto continuation lines in the YAML
                    keywordsBuffer = $"{keywordsBuffer} {trimmed}";
                }
            }

            FlushShaderLog();

            Debug.Log($"[{Path.GetFileName(k_ShaderVariantsFilePath)}] Total Shaders: {shaderCount.ToString()}, Total Variants: {totalVariantCount.ToString()}");
        }

        private static string ResolveShaderName(string guid, long fileID)
        {
            // Built-in shaders (guid 0000000000000000f000000000000000) resolve to
            // "Resources/unity_builtin_extra"; project shaders resolve to their asset path.
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                return "<unknown shader>";
            }

            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is Shader shader
                    && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shader, out _, out long localId)
                    && localId == fileID)
                {
                    return shader.name;
                }
            }

            return $"<shader not found at {assetPath}>";
        }

        private const string k_GraphicsStateCollectionFilePath = "Assets/GraphicsStateCollections/GraphicsStateCollection_OSXEditor_Metal.graphicsstate";

        // The .graphicsstate file is JSON. Each m_VariantInfoMap entry stores
        // shaderName, keywordNames, subShaderIndex and passIndex directly.
        // Pass name is not serialized, but it can be resolved from
        // subShaderIndex/passIndex via the editor-only ShaderData API.
        // Stage is not serialized either — a graphics state is a full pipeline
        // state, so a kept variant applies to every linked stage of the pass.
        [MenuItem("Tools/Shader Variant Stripping/Print GraphicsStateCollection File Info")]
        public static void PrintGraphicsStateCollectionFileInfo()
        {
            if (!File.Exists(k_GraphicsStateCollectionFilePath))
            {
                Debug.LogError($"GraphicsStateCollection file not found: {k_GraphicsStateCollectionFilePath}");
                return;
            }

            string json = File.ReadAllText(k_GraphicsStateCollectionFilePath);
            var collection = JsonUtility.FromJson<GraphicsStateCollectionJson>(json);

            if (collection?.m_VariantInfoMap == null)
            {
                Debug.LogError($"Failed to parse GraphicsStateCollection file: {k_GraphicsStateCollectionFilePath}");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("============== GraphicsStateCollection ==============\n");
            sb.Append($"File: {Path.GetFileName(k_GraphicsStateCollectionFilePath)}\n");
            sb.Append($"RuntimePlatform: {(RuntimePlatform)collection.m_RuntimePlatform}, ");
            sb.Append($"GraphicsDevice: {(GraphicsDeviceType)collection.m_DeviceRenderer}, ");
            sb.Append($"QualityLevel: {collection.m_QualityLevelName}\n");
            sb.Append($"Variant Count: {collection.m_VariantInfoMap.Count.ToString()}\n");
            sb.Append("====================================================\n");
            Debug.Log(sb.ToString());

            foreach (VariantMapEntryJson entry in collection.m_VariantInfoMap)
            {
                VariantInfoJson variant = entry.second;
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(variant.shaderAssetGUID));
                if (shader == null)
                {
                    shader = Shader.Find(variant.shaderName);
                }

                string passName = ResolvePassName(shader, variant.subShaderIndex, variant.passIndex);
                string keywords = string.IsNullOrEmpty(variant.keywordNames) ? "<no keyword>" : variant.keywordNames;

                sb = new StringBuilder();
                sb.Append("============== GraphicsState Variant ===============\n");
                sb.Append($"Shader Name: {variant.shaderName}\n");
                sb.Append($"PassName: {passName}, SubShaderIndex: {variant.subShaderIndex.ToString()}, PassIndex: {variant.passIndex.ToString()}\n");
                // Stage is not stored; a pipeline state covers all linked stages (vertex/fragment) of the pass
                sb.Append($"Stage: <n/a>, Keywords: {keywords}\n");
                sb.Append($"Hash: {entry.first.Hash}, GraphicsState Count: {(variant.graphicsStateInfoSet?.Count ?? 0).ToString()}\n");
                sb.Append("====================================================\n");
                Debug.Log(sb.ToString());
            }
        }

        private static string ResolvePassName(Shader shader, int subShaderIndex, int passIndex)
        {
            if (shader == null)
            {
                return "<shader not loaded>";
            }

            var shaderData = ShaderUtil.GetShaderData(shader);
            if (shaderData == null || subShaderIndex < 0 || subShaderIndex >= shaderData.SubshaderCount)
            {
                return "<n/a>";
            }

            var subShader = shaderData.GetSubshader(subShaderIndex);
            if (subShader == null || passIndex < 0 || passIndex >= subShader.PassCount)
            {
                return "<n/a>";
            }

            return subShader.GetPass(passIndex).Name;
        }

        [Serializable]
        private class GraphicsStateCollectionJson
        {
            public int m_DeviceRenderer;
            public int m_RuntimePlatform;
            public string m_QualityLevelName;
            public List<VariantMapEntryJson> m_VariantInfoMap;
        }

        [Serializable]
        private class VariantMapEntryJson
        {
            public VariantKeyJson first;
            public VariantInfoJson second;
        }

        [Serializable]
        private class VariantKeyJson
        {
            public string Hash;
        }

        [Serializable]
        private class VariantInfoJson
        {
            public string shaderName;
            public string shaderAssetGUID;
            public string keywordNames;
            public int subShaderIndex;
            public int passIndex;
            public List<GraphicsStateInfoJson> graphicsStateInfoSet;
        }

        [Serializable]
        private class GraphicsStateInfoJson
        {
            public int subPassIndex;
        }

        private void PrintProcessShaderInfo(string logType, Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"============== OnProcessShader {logType} =====================\n");
            sb.Append(
                $"Name: {shader.name}, ShaderType(Stage): {snippet.shaderType.ToString().ToLower()}, PassType(LightMode): {snippet.passType}, PassName:{snippet.passName}, SubShaderIndex:{snippet.pass.SubshaderIndex.ToString()}, PassIndex: {snippet.pass.PassIndex.ToString()}\n");

            foreach (ShaderCompilerData scd in data)
            {
                ShaderKeyword[] shaderKeywords = scd.shaderKeywordSet.GetShaderKeywords();
                sb.Append($"Keywords: {string.Join(" ", shaderKeywords)}\n");
            }
            sb.Append("====================================================\n");
            Debug.Log(sb.ToString());
        }
    }
}
