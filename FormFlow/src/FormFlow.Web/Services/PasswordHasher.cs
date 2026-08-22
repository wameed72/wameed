using System;
using System.Security.Cryptography;

namespace FormFlow.Web.Services
{
    /// <summary>PBKDF2 (SHA256, 100k iterations) password hashing with a per user random salt.</summary>
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100_000;

        public static (string Hash, string Salt) Create(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("كلمة المرور مطلوبة", nameof(password));
            }

            var salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            return (Convert.ToBase64String(Derive(password, salt)), Convert.ToBase64String(salt));
        }

        public static bool Verify(string password, string hash, string salt)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(salt))
            {
                return false;
            }

            byte[] saltBytes;
            byte[] expected;
            try
            {
                saltBytes = Convert.FromBase64String(salt);
                expected = Convert.FromBase64String(hash);
            }
            catch (FormatException)
            {
                return false;
            }

            var actual = Derive(password, saltBytes);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        private static byte[] Derive(string password, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(HashSize);
        }
    }
}
