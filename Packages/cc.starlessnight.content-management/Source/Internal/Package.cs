namespace Iris.ContentManagement.Internal
{
    public interface IPackage
    {
        IAsset GetAsset(string assetPath);
    }
}