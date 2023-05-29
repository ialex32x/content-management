namespace Iris.ContentManagement.Internal
{
    public interface IManagedStream
    {
        System.IO.Stream Open(string assetPath);
    }
}