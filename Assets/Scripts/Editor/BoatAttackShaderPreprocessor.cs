using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;


#if UNITY_EDITOR
public class BoatAttackShaderPreprocessor : IPreprocessShaders
{
#if UNITY_IOS
    private static readonly string s_svcPath = "Assets/ShaderVariants_iOS.shadervariants";
#endif
    private static ShaderVariantCollection s_svc;
    private static readonly string[] s_shadersDontStrip = 
    {
        "Hidden/CubeBlur",
        "Hidden/CubeCopy",
        "Hidden/CubeBlend",
        "Sprite/Default",
        "UI/Default",
        "Hidden/VideoComposite",
        "Hidden/VideoDecode",
        "Hidden/Compositing",
    };

    public int callbackOrder { get; }

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet,
        IList<ShaderCompilerData> data)
    {
        // return;
        
#if UNITY_IOS
        foreach (string dontstripShader in s_shadersDontStrip)
        {
            if (dontstripShader.Contains(shader.name))
                return;
        }

        s_svc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(s_svcPath);
        List<string> stringKeywordsStrings = new();

        for (int i = data.Count - 1; i >= 0; i--)
        {
            stringKeywordsStrings.Clear();
            ShaderKeyword[] shaderKeywords =
                data[i].shaderKeywordSet.GetShaderKeywords();

            foreach (ShaderKeyword shaderKeyword in shaderKeywords)
            {
                string shaderKeywordString = shaderKeyword.name;

                if (!string.IsNullOrEmpty(shaderKeywordString))
                {
                    stringKeywordsStrings.Add(shaderKeywordString);
                }
            }

            if (shader.name.Contains("VR"))
                continue;

            ShaderVariantCollection.ShaderVariant variant = new (shader,
                snippet.passType, stringKeywordsStrings.ToArray());

            if (!s_svc.Contains(variant))
            {
                // Strip this, it is not in our SVC
                data.RemoveAt(i);
            }
        }
#endif
    }
}
#endif
