# cc.starlessnight.content-management
WIP
Unity2021.3+

# Features (Planned)
* scriptable packaging 
* editor mode with content management
* enumerable content library
* packaging post-process support (encryption/compression)
* delta update support
* multi-threaded block download
* asynchronously load/unload
* serializable soft object path on prefab
* built-in http mode emulation in editor

# TODO

# Experimental

Read File Stream
```cs
var file = ContentSystem.GetAsset("Assets/Examples/Config/test.txt");
var stream = await file.LoadAsync<System.IO.Stream>();
var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8, false, 512, true);
var text = reader.ReadToEnd();
Debug.Log(text);
```

Load Unity Asset
```cs
var prefab = ContentSystem.GetAsset("Assets/Examples/Prefabs/Cube 1.prefab");
await prefab.LoadAsync();

var instance = Object.Instantiate<UnityEngine.GameObject>(prefab);
await System.Threading.Tasks.Task.Delay(2000);
Object.DestroyImmediate(instance);
```

# License
MIT
