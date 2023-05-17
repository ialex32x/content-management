namespace Iris.ContentManagement.Internal
{
    public interface IUriResolver
    {
        string GetUriString(string entryName);

        /// <summary>
        /// User-Agent for the header of requests. It can be null as default.
        /// @example: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/113.0.0.0 Safari/537.36 Edg/113.0.1774.35
        /// </summary>
        string GetUserAgent();
    }
}

