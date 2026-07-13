
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

public class AssetCachingTest : MonoBehaviour
{
    // public AssetReference boatPrefab;
    public string assetKey;
    public string assetKeyRemote;
    private AsyncOperationHandle<GameObject> asyncOperationHandle;

    public GameObject loadedGameObject;
    private List<GameObject> instantiatedBoats;

    public void Instantiate(string assetKey)
    {
        StartCoroutine(_Instantiate(assetKey));
    }

    private IEnumerator _Instantiate(string assetKey)
    {
        // Addressables.LoadAssetAsync<GameObject>(assetKey);
        // var handle = Addressables.DownloadDependenciesAsync(assetKey);
        // while (!handle.IsDone)
        //     yield return null;
        // Addressables.Release(handle);
        
        // Addressables.InstantiateAsync(assetKey, new Vector3(0, 0, 2.5f), Quaternion.identity).Completed += op =>
        // {
        //     asyncOperationHandle = op;
        // };
        Addressables.InstantiateAsync(assetKey, Random.onUnitSphere * 2.0f, Quaternion.identity).Completed += op =>
        {
            asyncOperationHandle = op;
        };
        
        yield return null;
    }
    
    // public void InstantiateRemote()
    // {
    //     StartCoroutine(_InstantiateRemote());
    // }
    //
    // private IEnumerator _InstantiateRemote()
    // {
    //     Addressables.LoadAssetAsync<GameObject>(assetKeyRemote);
    //     var handle = Addressables.DownloadDependenciesAsync(assetKeyRemote);
    //     while (!handle.IsDone)
    //         yield return null;
    //     Addressables.Release(handle);
    //     
    //     Addressables.InstantiateAsync(assetKeyRemote, new Vector3(0, 0, 2.5f), Quaternion.identity).Completed += op =>
    //     {
    //         asyncOperationHandle = op;
    //     };
    // }

    public void Destroy()
    {
        Addressables.ReleaseInstance(asyncOperationHandle);
    }

    public void UnloadUnusedAssets()
    {
        StartCoroutine(_UnloadUnusedAssets());
    }

    private IEnumerator _UnloadUnusedAssets()
    {
        var op = Resources.UnloadUnusedAssets();
        while (!op.isDone)
        {
            yield return null;
        }
        Debug.Log("Resources.UnloadUnusedAssets() Done");
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void CheckIfLoaded()
    {
        StringBuilder sb = new StringBuilder();
        foreach (AssetBundle ab in AssetBundle.GetAllLoadedAssetBundles())
        {
            sb.Append($"AssetBundle:{ab.name}\n");
            string[] assetNames = ab.GetAllAssetNames();
            foreach (string assetName in assetNames)
            {
                sb.Append(assetName);
                sb.Append("\n");
            }

            sb.Append("===================================");
        }
        Debug.Log(sb.ToString());
    }

    public void UnloadAllAssetBundles()
    {
        AssetBundle.UnloadAllAssetBundles(true);
    }
    
    
    
    public void LoadAsset()
    {
        Addressables.LoadAssetAsync<GameObject>(assetKey).Completed += op =>
        {
            instantiatedBoats = new List<GameObject>();
            asyncOperationHandle = op;
            // loadedGameObject = op.Result;
            Debug.Log("Loading done");
        };
    }

    public void InstantiateLoadedGameObject()
    {
        // instantiatedBoats.Add(GameObject.Instantiate<GameObject>(loadedGameObject,
        //     new Vector3(0, 0, 2.5f) + Random.onUnitSphere, Quaternion.identity));
        instantiatedBoats.Add(GameObject.Instantiate<GameObject>(asyncOperationHandle.Result,
            new Vector3(0, 0, 2.5f) + Random.onUnitSphere, Quaternion.identity));
    }

    public void ReleaseAsyncOpHandle()
    {
        if (asyncOperationHandle.IsValid())
        {
            foreach(GameObject go in instantiatedBoats)
                GameObject.Destroy(go);

            Addressables.Release(asyncOperationHandle);
            Debug.Log("asyncOperationHandle is released");
        }
        else
        {
            Debug.LogError("asyncOperationHandle is not valid");
        }
    }

    public void ReleaseAssetBundleData()
    {
        if (asyncOperationHandle.IsValid())
        {
            Addressables.Release(asyncOperationHandle);
            Debug.Log("asyncOperationHandle is released");
        }
    }

    public void CheckInstantiatedBoats()
    {
        foreach (GameObject go in instantiatedBoats)
        {
            if(go == null)
                Debug.Log("null");
            else
                Debug.Log(go.name);
        }
    }
}
