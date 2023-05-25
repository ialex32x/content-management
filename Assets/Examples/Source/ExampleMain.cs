using UnityEngine;
using Iris.ContentManagement;

public class ExampleMain : MonoBehaviour
{
    async void Start()
    {
        //TODO 怎么处理 stream 被外部 dispose 的问题 ()
        // var file = ContentSystem.Get().GetAsset("Assets/Examples/Config/test.txt");
        // var stream = await file.LoadAsync<System.IO.Stream>();
        // var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8, false, 512, true);
        // var text = reader.ReadToEnd();
        // Debug.Log(text);
        
        var prefab = ContentSystem.Get().GetAsset("Assets/Examples/Prefabs/Cube 1.prefab");
        await prefab.LoadAsync();

        var instance = Object.Instantiate<UnityEngine.GameObject>(prefab);
        await System.Threading.Tasks.Task.Delay(2000);
        Object.DestroyImmediate(instance);

        ContentSystem.Get().Shutdown();
    }
}
