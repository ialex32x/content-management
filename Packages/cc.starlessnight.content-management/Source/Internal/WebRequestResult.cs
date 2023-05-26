namespace Iris.ContentManagement.Internal
{
    public readonly struct WebRequestResult
    {
        public readonly bool isValid;
        public readonly WebRequestInfo info;
        public readonly System.Net.HttpStatusCode statusCode;

        public WebRequestResult(in WebRequestInfo info)
        {
            this.isValid = false;
            this.info = info;
            this.statusCode = System.Net.HttpStatusCode.BadRequest;
        }

        public WebRequestResult(in WebRequestInfo info, System.Net.HttpStatusCode statusCode)
        {
            this.isValid = true;
            this.info = info;
            this.statusCode = statusCode;
        }
    }
    
    public delegate void WebRequestAction(WebRequestResult result);
}