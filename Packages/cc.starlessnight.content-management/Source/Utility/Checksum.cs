using System.IO;

namespace Iris.ContentManagement.Utility
{
    public readonly struct Checksum : System.IEquatable<Checksum>
    {
        private const ushort Polynomial = 0xA001;
        private static readonly ushort[] Table = new ushort[256];

        private readonly ushort _value;

        public static implicit operator ushort(Checksum crc) => crc._value;

        public static implicit operator uint(Checksum crc) => crc._value;
        public static implicit operator int(Checksum crc) => crc._value;

        public static implicit operator Checksum(ushort value) => new(value);

        public Checksum(ushort value)
        {
            _value = value;
        }

        public bool Equals(in Checksum other) => this == other;

        public bool Equals(Checksum other) => this == other;

        public override bool Equals(object obj) => obj is Checksum other && this == other;

        public override int GetHashCode() => _value;

        public static bool operator ==(Checksum a, Checksum b) => a._value == b._value;

        public static bool operator !=(Checksum a, Checksum b) => a._value != b._value;

        public static Checksum Parse(string text) => ushort.Parse(text, System.Globalization.NumberStyles.HexNumber);

        public static bool TryParse(string text, out Checksum value)
        {
            var r = ushort.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var n);
            value = n;
            return r;
            // var r = ushort.TryParse(text, out var n);
            // value = n;
            // return r;
        }

        public override string ToString() => _value.ToString("X4");
        // public override string ToString() => _value.ToString();

        public static unsafe ushort ComputeChecksum(Stream stream, ushort value = 0)
        {
            const int BufferSize = 256;
            var crisp = stackalloc byte[BufferSize];
            var span = new System.Span<byte>(crisp, BufferSize);
            var read = stream.Read(span);
            while (read > 0)
            {
                value = ComputeChecksum(span, 0, read, value);
                read = stream.Read(span);
            }
            return value;
        }

        public static ushort ComputeChecksum(byte[] bytes) => ComputeChecksum(bytes, 0, bytes.Length, 0);

        public static ushort ComputeChecksum(byte[] bytes, int offset, int count, ushort checksum)
        {
            for (var i = 0; i < count; ++i)
            {
                var index = (byte)(checksum ^ bytes[i + offset]);
                checksum = (ushort)((checksum >> 8) ^ Table[index]);
            }
            return checksum;
        }

        public static ushort ComputeChecksum(System.Span<byte> bytes, int offset, int count, ushort checksum)
        {
            for (var i = 0; i < count; ++i)
            {
                var index = (byte)(checksum ^ bytes[i + offset]);
                checksum = (ushort)((checksum >> 8) ^ Table[index]);
            }
            return checksum;
        }

        static Checksum()
        {
            ushort value;
            ushort temp;
            for (ushort i = 0; i < Table.Length; ++i)
            {
                value = 0;
                temp = i;
                for (byte j = 0; j < 8; ++j)
                {
                    if (((value ^ temp) & 0x0001) != 0)
                    {
                        value = (ushort)((value >> 1) ^ Polynomial);
                    }
                    else
                    {
                        value >>= 1;
                    }
                    temp >>= 1;
                }
                Table[i] = value;
            }
        }
    }
}