using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class CustomMenuItem
{
    [MenuItem("CustomMenu/Open Scene/main_menu", false, 1)]
    static void OpenScene1()
    {
        EditorSceneManager.OpenScene("Assets/scenes/main_menu.unity");
    }
    
    [MenuItem("CustomMenu/Open Scene/level_Island", false, 2)]
    static void OpenScene2()
    {
        EditorSceneManager.OpenScene("Assets/scenes/_levels/level_Island.unity");
    }
    
    [MenuItem("CustomMenu/Open Scene/demo_Island", false, 3)]
    static void OpenScene3()
    {
        EditorSceneManager.OpenScene("Assets/scenes/demo_Island.unity");
    }
}
