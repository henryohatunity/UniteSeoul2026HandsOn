using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AddressablesTest : MonoBehaviour
{
    public string[] assetKeys;

    public string fileUrl;
    public string fileHash;

    public void InitAddressables()
    {
        StartCoroutine(InitializeAddressablesAsync());
    }
    
    private IEnumerator InitializeAddressablesAsync()
    {
        AsyncOperationHandle<IResourceLocator> handle = Addressables.InitializeAsync(false);
        yield return handle;
        IResourceLocator resourceLocator = handle.Result;
        Debug.Log($"Keys: {string.Join(", ", resourceLocator.Keys)}");

        BuildAssetKeyArrayFromResourceLocator(resourceLocator);
        CheckDownloadSize(assetKeys);

        /*
        // Download dependencies...
        Addressables.GetDownloadSizeAsync(resourceLocator.Keys).Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded && op.Result > 0)
            {
                Debug.Log($"GetDownloadSizeAsync total size: {op.Result}");
                Addressables.DownloadDependenciesAsync(resourceLocator.Keys, Addressables.MergeMode.None, true)
                        .Completed +=
                    (opDownload) =>
                    {
                        Debug.Log("Download done");
                    };
            }
        };
        */
        Addressables.Release(handle);
        
        // OR...
        // BuildAssetKeyArrayFromResourceLocator(resourceLocator);
        // CheckDownloadSize(assetKeys);
    }
    
    private void BuildAssetKeyArrayFromResourceLocator(IResourceLocator resourceLocator)
    {
        object[] temp = resourceLocator.Keys.ToArray();
        assetKeys = new string[temp.Length];
        for (int i = 0; i < temp.Length; i++)
            assetKeys[i] = temp[i].ToString();
    }
    
    private void CheckDownloadSize(string[] keysToCheck)
    {
        for (int i = 0; i < keysToCheck.Length; i++)
        {
            if(keysToCheck[i].Equals("0") || keysToCheck[i].Equals("1"))  continue;
            // Debug.Log("checking " + keysToCheck[i]);
            string thisKey = keysToCheck[i];
            Addressables.GetDownloadSizeAsync(thisKey).Completed += (handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    Debug.Log($"{thisKey}: Download size:{handle.Result}");
                }
                Addressables.Release(handle);
            });
        }
        // Addressables.DownloadDependenciesAsync(resourceLocator.Keys, Addressables.MergeMode.None, true);
    }

    public void CheckDownloadSize()
    {
        assetKeys = new string[] {"boat_renegade", "boat_interceptor_Remote" };
        StartCoroutine(_CheckDownloadsize());
    }

    private IEnumerator _CheckDownloadsize()
    {
        foreach (string assetKey in assetKeys)
        {
            var op = Addressables.GetDownloadSizeAsync(assetKey);
            while (!op.IsDone)
                yield return null;
            Debug.Log($"{assetKey}:{op.Result}");
        }
    }

    public void ClearCacheAll()
    {
        Debug.Log($"Cache Path: {Caching.defaultCache.path}");
        Debug.Log($"Cache Read Only: {Caching.defaultCache.readOnly}");
        Debug.Log($"Cache Ready: {Caching.ready}");
        Caching.ClearCache();
    }

    public void CheckIfCached()
    {
        string url = null;
        string hash = null;
        foreach (IResourceLocator resourceLocator in Addressables.ResourceLocators)
        {
            // Debug.Log($"ResourceLocatorId:{resourceLocator.LocatorId}");
            foreach (object objectKey in resourceLocator.Keys)
            {
                if (objectKey is string)
                {
                    string assetKeyStr = (string)objectKey;
                    if (!string.IsNullOrEmpty(assetKeyStr))
                    {
                        // Debug.Log($"Key:{objectKey}");
                        if (assetKeyStr.Contains(".bundle") && assetKeyStr.Contains("boats_remote"))
                        {
                            url = "https://feeltheforceterraindata.s3.ap-northeast-1.amazonaws.com/"+assetKeyStr;
                            string[] temp = assetKeyStr.Remove(assetKeyStr.IndexOf(".bundle")).Split('_');
                            hash = temp[temp.Length - 1];
                            break;
                        }
                    }
                }
            }
        }
        if(!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(hash))
            Debug.Log($"Is {url}:{hash} Cached:{Caching.IsVersionCached(url, Hash128.Parse(hash))}");
    }
}
