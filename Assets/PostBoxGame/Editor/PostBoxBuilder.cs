using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PostBoxGame;

namespace PostBoxGameEditor
{
    [InitializeOnLoad]
    public static class PostBoxBuilder
    {
        const string ConfigPath="Assets/PostBoxGame/Resources/PostBoxConfig.asset";
        const string ScenePath="Assets/PostBoxGame/Scenes/PostBox.unity";
        static PostBoxBuilder(){EditorApplication.delayCall+=EnsureConfig;}
        static void EnsureConfig()
        {
            if(AssetDatabase.LoadAssetAtPath<PostBoxConfig>(ConfigPath)!=null)return;
            System.IO.Directory.CreateDirectory("Assets/PostBoxGame/Resources");
            AssetDatabase.CreateAsset(PostBoxConfig.MakeDefault(),ConfigPath);
            AssetDatabase.SaveAssets();
        }
        [MenuItem("PostBox Game/Build / Rebuild Prototype")]
        public static void Build()
        {
            EnsureConfig();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("PostBox Game");
            root.AddComponent<PostBoxGame.PostBoxGame>().config=AssetDatabase.LoadAssetAtPath<PostBoxConfig>(ConfigPath);
            System.IO.Directory.CreateDirectory("Assets/PostBoxGame/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/PostBoxGame/Prefabs/PostBox Game.prefab");
            EditorSceneManager.SaveScene(scene,ScenePath);
            var scenes=EditorBuildSettings.scenes;
            bool found=false;foreach(var entry in scenes)if(entry.path==ScenePath)found=true;
            if(!found){var result=new EditorBuildSettingsScene[scenes.Length+1];result[0]=new EditorBuildSettingsScene(ScenePath,true);scenes.CopyTo(result,1);EditorBuildSettings.scenes=result;}
            AssetDatabase.SaveAssets();
            Debug.Log("PostBox prototype built: "+ScenePath);
        }
    }
}
