using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GPUAssign.Models;

/// <summary>Root object stored in apps.json (user's personal list)</summary>
public class AppConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("autoCleanup")]
    public bool AutoCleanup { get; set; } = false;

    [JsonPropertyName("autoSyncOnStartup")]
    public bool AutoSyncOnStartup { get; set; } = true;

    /// <summary>Parallelism degree for EXE scanning (default: 4, 1-16)</summary>
    [JsonPropertyName("maxDegreeOfParallelism")]
    public int MaxDegreeOfParallelism { get; set; } = 4;

    /// <summary>"System" | "Light" | "Dark"</summary>
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "System";

    /// <summary>"auto" | "ja-JP" | "en-US"</summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "auto";

    [JsonPropertyName("apps")]
    public List<AppDefinition> Apps { get; set; } = new();
}
