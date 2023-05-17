
using System;

namespace Iris.ContentManagement
{
    public readonly struct ContentDigest : IEquatable<ContentDigest>
    {
        public readonly uint size;
        public readonly Utility.Checksum checksum;

        public ContentDigest(uint size, Utility.Checksum checksum)
        {
            this.size = size;
            this.checksum = checksum;
        }

        public bool Equals(in ContentDigest other) => this == other;

        public bool Equals(ContentDigest other) => this == other;

        public override bool Equals(object obj) => obj is ContentDigest other && this == other;

        public override int GetHashCode() => (int)(size ^ checksum);

        public override string ToString() => $"{size} ({checksum})";

        public static bool operator ==(ContentDigest a, ContentDigest b) => a.size == b.size && a.checksum == b.checksum;

        public static bool operator !=(ContentDigest a, ContentDigest b) => a.size != b.size || a.checksum != b.checksum;
    }
}
