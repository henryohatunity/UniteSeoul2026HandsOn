
using System.Collections.Generic;
using System.Text;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
public class MyShaderStripping : IPreprocessShaders
{
    public int callbackOrder { get; }
    
    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        // StringBuilder sb = new StringBuilder();
        // for(int i = data.Count - 1; i >= 0; i--)
        // {
        //     ShaderKeyword[] keywords = data[i].shaderKeywordSet.GetShaderKeywords();
        //     for (int j = 0; j < keywords.Length; j++)
        //     {
        //         sb.Append(keywords[j].name);
        //         sb.Append(" ");
        //     }
        //
        //     Debug.Log($"OnProcessShader {shader.name}, variants:{sb.ToString()}");
        //     sb.Clear();
        // }
    }
}
#endif
