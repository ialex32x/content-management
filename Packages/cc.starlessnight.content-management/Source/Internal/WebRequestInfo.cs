namespace Iris.ContentManagement.Internal
{
    public readonly struct WebRequestInfo : System.IEquatable<WebRequestInfo>
    {
        public readonly int id;
        public readonly string name;
        public readonly uint expectedSize;

        public override string ToString() => $"{id} {name}";

        public WebRequestInfo(int id, string name, uint expectedSize)
        {
            this.id = id;
            this.name = name;
            this.expectedSize = expectedSize;
        }

        public bool Equals(in WebRequestInfo other) => this == other;

        public bool Equals(WebRequestInfo other) => this == other;

        public override bool Equals(object obj) => obj is WebRequestInfo other && this == other;

        public override int GetHashCode() => id;

        public static bool operator ==(WebRequestInfo a, WebRequestInfo b) => a.id == b.id && a.name == b.name && a.expectedSize == b.expectedSize;

        public static bool operator !=(WebRequestInfo a, WebRequestInfo b) => a.id != b.id || a.name != b.name || a.expectedSize != b.expectedSize;

    }
}