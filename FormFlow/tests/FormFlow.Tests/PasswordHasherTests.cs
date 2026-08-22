using FormFlow.Web.Services;
using Xunit;

namespace FormFlow.Tests
{
    public class PasswordHasherTests
    {
        [Fact]
        public void Verify_AcceptsCorrectPasswordOnly()
        {
            var (hash, salt) = PasswordHasher.Create("Super@123");

            Assert.True(PasswordHasher.Verify("Super@123", hash, salt));
            Assert.False(PasswordHasher.Verify("super@123", hash, salt));
            Assert.False(PasswordHasher.Verify("Super@123", hash, "bm90LWEtc2FsdA=="));
        }

        [Fact]
        public void Create_UsesRandomSaltPerCall()
        {
            var first = PasswordHasher.Create("same-password");
            var second = PasswordHasher.Create("same-password");

            Assert.NotEqual(first.Salt, second.Salt);
            Assert.NotEqual(first.Hash, second.Hash);
        }
    }
}
