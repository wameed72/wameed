using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorDoctor.Core.Models
{

/// <summary>
/// Single-row table tracking the state of the last database update.
/// </summary>
public class SyncMetadata
{
    public int Id { get; set; }

    public DateTime? LastSyncUtc { get; set; }

    public string? LastManifestVersion { get; set; }

    public int LastEntryCount { get; set; }

    /// <summary>
    /// Outcome of the last sync attempt (Success, Offline, Error).
    /// </summary>
    public string LastStatus { get; set; } = "Never";

    public string? LastMessage { get; set; }
}
}
