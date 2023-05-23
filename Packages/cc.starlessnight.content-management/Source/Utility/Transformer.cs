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

        public static ITransform GetDecryptor(byte[] key, byte[] iv)
        {
            var algo = Rijndael.Create();
            algo.Padding = PaddingMode.Zeros;
            return new DefaultTransform() { _decryptor = algo.CreateDecryptor(key, iv) };
        }

        public static ITransform GetEncryptor(byte[] key, byte[] iv)
        {
            var algo = Rijndael.Create();
            algo.Padding = PaddingMode.Zeros;
            return new DefaultTransform() { _decryptor = algo.CreateEncryptor(key, iv) };
        }

        public byte[] Transform(byte[] input, int offset, int count)
        {
            return _decryptor.TransformFinalBlock(input, offset, count);
        }

        public static Stream Decrypt(Stream inStream, byte[] key, byte[] iv, int rsize, int chunkSize)
        {
            return new ChunkedStream(DefaultTransform.GetDecryptor(key, iv), inStream, rsize, chunkSize);
        }
    }
}
