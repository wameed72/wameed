using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ErrorDoctor.Core.Data
{
    public record ConnectionResolution(string? ConnectionString, string? Server, IReadOnlyList<string> Tried)
    {
        public bool Found => ConnectionString is not null;
    }

    /// <summary>
    /// Picks the first reachable SQL Server so the app works with zero configuration:
    /// the configured connection is tried first, then the common local SQL Server setups
    /// (LocalDB, default local instance, SQLEXPRESS). The chosen target database is created
    /// later by <see cref="DatabaseInitializer"/>.
    /// </summary>
    public static class SqlServerConnectionResolver
    {
        private const string Database = "ErrorDoctor";

        public static IReadOnlyList<string> DefaultCandidates(string? configured)
        {
            var servers = new[]
            {
                @"(localdb)\MSSQLLocalDB",
                "localhost",
                @"localhost\SQLEXPRESS",
                @".\SQLEXPRESS",
            };

            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(configured))
            {
                candidates.Add(configured!);
            }

            foreach (var server in servers)
            {
                candidates.Add($"Server={server};Database={Database};Trusted_Connection=True;TrustServerCertificate=True");
            }

            return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Returns the first candidate whose server accepts a connection, or an empty result.
        /// Connectivity is checked against <c>master</c> with a short timeout so missing servers fail fast.
        /// </summary>
        public static async Task<ConnectionResolution> ResolveAsync(
            IEnumerable<string> candidates,
            CancellationToken cancellationToken = default)
        {
            var tried = new List<string>();

            foreach (var candidate in candidates)
            {
                string server;
                string probe;
                try
                {
                    var builder = new SqlConnectionStringBuilder(candidate)
                    {
                        InitialCatalog = "master",
                        ConnectTimeout = 3,
                    };
                    server = builder.DataSource;
                    probe = builder.ConnectionString;
                }
                catch (ArgumentException)
                {
                    continue;
                }

                tried.Add(server);

                try
                {
                    await using var connection = new SqlConnection(probe);
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    return new ConnectionResolution(candidate, server, tried);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // Server unreachable; try the next candidate.
                }
            }

            return new ConnectionResolution(null, null, tried);
        }
    }
}
