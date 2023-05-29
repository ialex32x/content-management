using UnityEngine;
using Iris.ContentManagement;

public class ExampleMain : MonoBehaviour
{
    async void Start()
    {
        ContentSystem.Startup(new()
        {
            useDownloader = false,
            useStreamingAssets = false,
            uriResolver = null,
        });

        //CASE 访问文件资源
        var file = ContentSystem.GetAsset("Assets/Examples/Config/test.txt");
        {
            using var stream = await file.LoadAsync<System.IO.Stream>();
            using var reader = new System.IO.StreamReader(stream);
            var text = reader.ReadToEnd();
            Debug.LogFormat("1st read: {0}", text);
        }
        {
            using var stream = await file.LoadAsync<System.IO.Stream>();
            using var reader = new System.IO.StreamReader(stream);
            var text = reader.ReadToEnd();
            Debug.LogFormat("2nd read: {0}", text);
        }

        //CASE 访问unity资源 
        var prefab = ContentSystem.GetAsset("Assets/Examples/Prefabs/Cube 1.prefab");
        await prefab.LoadAsync();

        var instance = Object.Instantiate<UnityEngine.GameObject>(prefab);
        await System.Threading.Tasks.Task.Delay(2000);
        Object.DestroyImmediate(instance);

        //CASE 访问无效资源
        var nonexistence = ContentSystem.GetAsset("Assets/nonexistence.asset");
        var nonexistence_value = await nonexistence.LoadAsync();
        if (nonexistence_value == null)
        {
            Debug.LogErrorFormat("load nonexistent asset: {0}", nonexistence);
        }

        ContentSystem.Shutdown();
    }
}
