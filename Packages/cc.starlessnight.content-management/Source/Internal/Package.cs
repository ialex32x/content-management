namespace Iris.ContentManagement.Internal
{
    public interface IPackage
    {
        EPackageState state { get; }

        IAsset GetAsset(string assetPath);
    }
}