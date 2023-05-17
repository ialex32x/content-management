using System.Collections.Generic;

namespace Iris.ContentManagement.Editor
{
    using UnityEditor;

    public class ContentBuilder
    {
        private class GeneratedPackage
        {
            public ContentRegistry.PackageBuildHandle package;
            public HashSet<ContentRegistry.AssetBuildHandle> assets = new();
            public Utility.Checksum checksum;
        }

        private Dictionary<string, GeneratedPackage> assetBundles = new();

        public void BuildAssetBundles(ContentRegistry builder)
        {
            var builds = new AssetBundleBuild[assetBundles.Count];
            var outputPath = "Build/Packages/Staging/Win64";
            var options = BuildAssetBundleOptions.AssetBundleStripUnityVersion | BuildAssetBundleOptions.DeterministicAssetBundle | BuildAssetBundleOptions.ChunkBasedCompression;
            var target = BuildTarget.StandaloneWindows64;

            var packageIndex = 0;
            foreach (var kv in assetBundles)
            {
                var generatedPackage = kv.Value;
                if (builder.TryGetPackageState(generatedPackage.package, out var package, out var collection))
                {
                    var build = new AssetBundleBuild();
                    build.assetBundleName = $"{collection.name}_{package.name}{collection.extension}";
                    var assetPaths = new string[generatedPackage.assets.Count];
                    var assetIndex = 0;
                    foreach (var asset in generatedPackage.assets)
                    {
                        builder.TryGetAssetState(asset, out var assetState);

                        assetPaths[assetIndex++] = assetState.assetRef;
                    }
                    build.assetNames = assetPaths;
                    build.addressableNames = assetPaths;
                    builds[packageIndex++] = build;
                }
            }
            BuildPipeline.BuildAssetBundles(outputPath, builds, options, target);
            //TODO encrypt source into the next stage 
            //TODO calculate checksum
        }

        public void BuildArchives()
        {
            //TODO generate zip files
            //TODO encrypt source into the next stage 
            //TODO calculate checksum
        }
    }
}