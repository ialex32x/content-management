
namespace Iris.ContentManagement
{
    using Iris.ContentManagement.Internal;
    
    // 带版本校验的内容包管理器
    public interface IContentManager
    {
        //TODO 待定
        IAsset GetAsset(string assetPath);

        void Shutdown();
    }
}