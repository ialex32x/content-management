namespace Iris.ContentManagement.Editor
{
    // static, shared, base(StreamingAssets) etc.
    public struct AssetTags
    {
        public uint value;
    }

    // win, mac, ios, android...
    public struct AssetPlatforms
    {
        public uint value;
    }

    public struct AssetBuildState
    {
        public string assetRef;
        public Utility.SIndex package;
        public AssetTags tags;
        public AssetPlatforms platforms;

        public bool isExternal => string.IsNullOrEmpty(UnityEditor.AssetDatabase.GUIDToAssetPath(assetRef));
    }
}