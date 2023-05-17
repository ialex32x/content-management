using System.Security.Cryptography;
using System.IO;

namespace Iris.ContentManagement.Utility
{
    public interface ITransform
    {
        byte[] Transform(byte[] input, int offset, int count);
    }

    public class DefaultTransform : ITransform
    {
        private ICryptoTransform _decryptor;

        public DefaultTransform(byte[] key, byte[] iv)
        {
            var algo = Rijndael.Create();
            algo.Padding = PaddingMode.Zeros;
            _decryptor = algo.CreateDecryptor(key, iv);
        }

        public byte[] Transform(byte[] input, int offset, int count)
        {
            return _decryptor.TransformFinalBlock(input, offset, count);
        }

        public static void Encrypt(Stream outStream, Stream inStream, byte[] key, byte[] iv, int unsafeChunkSize)
        {
            var transform = new DefaultTransform(key, iv);
            var chunkSize = ChunkedStream.GetChunkSize(unsafeChunkSize);
            var buffer = new byte[chunkSize];
            var read = 0;

            while (true)
            {
                read = inStream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    break;
                }

                var outBuffer = transform.Transform(buffer, 0, read);
                outStream.Write(outBuffer, 0, outBuffer.Length);
            }
        }

        public static Stream Decrypt(Stream inStream, byte[] key, byte[] iv, int rsize, int chunkSize)
        {
            return new ChunkedStream(new DefaultTransform(key, iv), inStream, rsize, chunkSize);
        }
    }
}
