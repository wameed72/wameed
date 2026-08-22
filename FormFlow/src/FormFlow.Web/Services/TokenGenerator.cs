using System;
using System.Security.Cryptography;
using System.Text;

namespace FormFlow.Web.Services
{
    public static class TokenGenerator
    {
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        /// <summary>Url safe token used for public form links.</summary>
        public static string NewToken(int length = 22) => Random(length);

        /// <summary>Short code (e.g. FRM-7KQ2X4) given to the employee after submitting.</summary>
        public static string NewTrackingCode() => "FRM-" + Random(6);

        private static string Random(int length)
        {
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            var bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            var builder = new StringBuilder(length);
            foreach (var b in bytes)
            {
                builder.Append(Alphabet[b % Alphabet.Length]);
            }

            return builder.ToString();
        }
    }
}
