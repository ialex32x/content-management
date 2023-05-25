using UnityEngine;
using Iris.ContentManagement;

public class ExampleMain : MonoBehaviour
{
    async void Start()
    {
        ContentSystem.Startup();

        //TODO 怎么处理 stream 被外部 dispose 的问题 ()
        //TODO asset/stream 的区别处理
        var file = ContentSystem.GetAsset("Assets/Examples/Config/test.txt");
        var stream = await file.LoadAsync<System.IO.Stream>();
        var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8, false, 512, true);
        var text = reader.ReadToEnd();
        Debug.Log(text);
        
        var prefab = ContentSystem.GetAsset("Assets/Examples/Prefabs/Cube 1.prefab");
        await prefab.LoadAsync();

        var instance = Object.Instantiate<UnityEngine.GameObject>(prefab);
        await System.Threading.Tasks.Task.Delay(2000);
        Object.DestroyImmediate(instance);

        ContentSystem.Shutdown();
    }
}
