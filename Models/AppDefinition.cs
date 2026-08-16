using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using GPUAssign.Services;
using Microsoft.UI.Xaml.Media;

namespace GPUAssign.Models;

/// <summary>GPU preference level (abstract, device-independent)</summary>
public enum GpuPreference
{
    Default = 0,
    PowerSaving = 1,
    HighPerformance = 2
}

/// <summary>
/// How to locate the target EXE.
/// </summary>
public enum SearchMode
{
    /// <summary>searchPath = exact directory, exe = exact filename. Just verify existence.</summary>
    Fixed = 0,

    /// <summary>Recursively search searchPath for exe, select by version number / file version / modified date.</summary>
    LatestVersion = 1,

    /// <summary>searchPath may contain * / ? wildcards in directory segments; exe may also be a glob pattern.</summary>
    Glob = 2,

    /// <summary>Recursively search searchPath for files whose full path matches the regex in exe.</summary>
    Regex = 3
}

/// <summary>Sync status of an app entry</summary>
public enum SyncStatus
{
    Unknown,
    Synced,
    OutOfDate,
    NotFound,
    Error
}

/// <summary>
/// One managed application definition.
/// Users register searchPath + exe instead of a versioned full path.
/// </summary>
public class AppDefinition : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _name = string.Empty;
    private string? _currentExePath;
    private SyncStatus _syncStatus = SyncStatus.Unknown;
    private string? _syncMessage;
    private ImageSource? _iconSource;
    private GpuPreference _gpuPreference = GpuPreference.Default;

    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(NameInitial)); }
    }

    /// <summary>Display category</summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Base directory to search (env vars like %LOCALAPPDATA% are expanded).
    /// For Glob mode this may contain * / ? in directory segments.
    /// For Fixed mode this is the directory containing the exe.
    /// </summary>
    [JsonPropertyName("searchPath")]
    public string SearchPath { get; set; } = string.Empty;

    /// <summary>
    /// EXE filename. For Glob mode may contain * ?.
    /// For Regex mode this is the regex pattern applied to the full path.
    /// </summary>
    [JsonPropertyName("exe")]
    public string ExeName { get; set; } = string.Empty;

    /// <summary>How to locate the EXE.</summary>
    [JsonPropertyName("searchMode")]
    public SearchMode SearchMode { get; set; } = SearchMode.LatestVersion;

    /// <summary>Recurse into subdirectories (used by LatestVersion mode).</summary>
    [JsonPropertyName("recursive")]
    public bool Recursive { get; set; } = true;

    /// <summary>Desired GPU preference.</summary>
    [JsonPropertyName("gpu")]
    public GpuPreference GpuPreference
    {
        get => _gpuPreference;
        set
        {
            if (_gpuPreference != value)
            {
                _gpuPreference = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GpuIndex));
                OnPropertyChanged(nameof(GpuLabel));
            }
        }
    }

    /// <summary>EXE paths this app has been assigned to in the past.</summary>
    [JsonPropertyName("managedPaths")]
    public List<string> ManagedPaths { get; set; } = new();

    // ---- Runtime-only (not persisted) ----

    [JsonIgnore]
    public int GpuIndex
    {
        get => (int)GpuPreference;
        set
        {
            if ((int)GpuPreference != value && value >= 0)
            {
                GpuPreference = (GpuPreference)value;
            }
        }
    }

    [JsonIgnore]
    public string? CurrentExePath
    {
        get => _currentExePath;
        set { _currentExePath = value; OnPropertyChanged(); }
    }

    [JsonIgnore]
    public ImageSource? IconSource
    {
        get => _iconSource;
        set
        {
            _iconSource = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasIcon));
            OnPropertyChanged(nameof(HasNoIcon));
        }
    }

    [JsonIgnore]
    public bool HasIcon => _iconSource != null;

    [JsonIgnore]
    public bool HasNoIcon => _iconSource == null;

    [JsonIgnore]
    public SyncStatus SyncStatus
    {
        get => _syncStatus;
        set { _syncStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(SyncStatusLabel)); }
    }

    [JsonIgnore]
    public string? SyncMessage
    {
        get => _syncMessage;
        set { _syncMessage = value; OnPropertyChanged(); }
    }

    [JsonIgnore]
    public string NameInitial =>
        string.IsNullOrEmpty(_name) ? "?" : _name[0].ToString().ToUpperInvariant();

    [JsonIgnore]
    public string GpuLabel => GpuPreference switch
    {
        GpuPreference.HighPerformance => L.Get("gpu.high"),
        GpuPreference.PowerSaving     => L.Get("gpu.powerSaving"),
        _                             => L.Get("gpu.default")
    };

    [JsonIgnore]
    public string SyncStatusLabel => SyncStatus switch
    {
        SyncStatus.Synced    => L.Get("sync.synced"),
        SyncStatus.OutOfDate => L.Get("sync.outOfDate"),
        SyncStatus.NotFound  => L.Get("sync.notFound"),
        SyncStatus.Error     => L.Get("sync.error"),
        _                    => "─"
    };

    [JsonIgnore]
    public string SearchModeLabel => SearchMode switch
    {
        SearchMode.Fixed         => L.Get("searchMode.fixed"),
        SearchMode.LatestVersion => L.Get("searchMode.latestVersion"),
        SearchMode.Glob          => L.Get("searchMode.glob"),
        SearchMode.Regex         => L.Get("searchMode.regex"),
        _                        => SearchMode.ToString()
    };
}
