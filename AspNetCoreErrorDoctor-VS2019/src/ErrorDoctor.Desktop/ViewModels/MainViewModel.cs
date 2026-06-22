using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ErrorDoctor.Core.Data;
using ErrorDoctor.Core.Matching;
using ErrorDoctor.Core.Sync;
using ErrorDoctor.Core.Sync.Sources;
using ErrorDoctor.Desktop.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ErrorDoctor.Desktop.ViewModels
{

public class MainViewModel : INotifyPropertyChanged
{
    private readonly AppConfig _config;
    private DbContextFactory _dbFactory;
    private readonly ErrorMatcher _matcher = new();
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    private string _errorText = string.Empty;
    private string _statusMessage = "جارٍ التهيئة...";
    private string _databaseInfo = string.Empty;
    private bool _isBusy;
    private bool _hasSearched;

    public MainViewModel(AppConfig config)
    {
        _config = config;
        _dbFactory = new DbContextFactory(config.ConnectionString);

        DiagnoseCommand = new AsyncRelayCommand(DiagnoseAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(ErrorText));
        CheckUpdatesCommand = new AsyncRelayCommand(() => SyncAsync(force: true), () => !IsBusy);
    }

    public ObservableCollection<MatchItemViewModel> Results { get; } = new();

    public AsyncRelayCommand DiagnoseCommand { get; }

    public AsyncRelayCommand CheckUpdatesCommand { get; }

    public string ErrorText
    {
        get => _errorText;
        set
        {
            if (Set(ref _errorText, value))
            {
                DiagnoseCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public string DatabaseInfo
    {
        get => _databaseInfo;
        set => Set(ref _databaseInfo, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (Set(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                DiagnoseCommand.RaiseCanExecuteChanged();
                CheckUpdatesCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public bool HasSearched
    {
        get => _hasSearched;
        set
        {
            if (Set(ref _hasSearched, value))
            {
                OnPropertyChanged(nameof(ShowNoResults));
            }
        }
    }

    public bool ShowNoResults => HasSearched && Results.Count == 0;

    /// <summary>
    /// Creates/seeds the database on first run, then checks for an update if the network is available.
    /// </summary>
    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "جارٍ البحث عن SQL Server وتجهيز قاعدة البيانات...";
            var resolution = await SqlServerConnectionResolver.ResolveAsync(
                SqlServerConnectionResolver.DefaultCandidates(_config.ConnectionString));
            if (!resolution.Found)
            {
                StatusMessage =
                    $"تعذّر الاتصال بأي SQL Server. جُرّبت: {string.Join("، ", resolution.Tried)}. " +
                    "ثبّت SQL Server أو LocalDB، أو اضبط سلسلة الاتصال في appsettings.json.";
                return;
            }

            _dbFactory = new DbContextFactory(resolution.ConnectionString!);

            await using (var db = _dbFactory.Create())
            {
                await DatabaseInitializer.InitializeAsync(db);
            }

            await RefreshDatabaseInfoAsync();
            StatusMessage = $"جاهز (الخادم: {resolution.Server}). الصق نص الخطأ ثم اضغط (تشخيص).";

            await AutoSyncIfDueAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذّر الاتصال بقاعدة البيانات: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AutoSyncIfDueAsync()
    {
        if (!_config.HasAnyUpdateSource)
        {
            return;
        }

        try
        {
            await using var db = _dbFactory.Create();
            var service = new SyncService(db, BuildManifestSource());
            if (await service.NeedsSyncAsync(_config.UpdateInterval))
            {
                var result = await service.SyncAsync();
                ApplySyncResult(result);
                await RefreshDatabaseInfoAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذّر التحديث التلقائي (سيتم استخدام البيانات المحلية): {ex.Message}";
        }
    }

    private async Task SyncAsync(bool force)
    {
        if (!_config.HasAnyUpdateSource)
        {
            StatusMessage = "لا توجد مصادر تحديث مفعّلة. فعّل Stack Overflow أو GitHub في appsettings.json.";
            return;
        }

        IsBusy = true;
        StatusMessage = "جارٍ جلب الأخطاء والحلول من المنصات الموثوقة (Stack Overflow وGitHub)...";
        try
        {
            await using var db = _dbFactory.Create();
            var service = new SyncService(db, BuildManifestSource());
            var result = await service.SyncAsync(force);
            ApplySyncResult(result);
            await RefreshDatabaseInfoAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"فشل التحديث: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Builds the live update source that aggregates the configured trusted platforms
    /// (Stack Overflow, official .NET GitHub repos) plus an optional self-hosted manifest.
    /// </summary>
    private IManifestSource BuildManifestSource()
    {
        var sources = new List<IErrorSource>();

        if (_config.EnableStackOverflow)
        {
            sources.Add(new StackOverflowSource(
                HttpClient, _config.MaxStackOverflowQuestions, _config.StackOverflowTag, _config.StackAppsKey));
        }

        if (_config.EnableGitHub)
        {
            sources.Add(new GitHubIssuesSource(HttpClient, token: _config.GitHubToken));
        }

        if (!string.IsNullOrWhiteSpace(_config.ManifestUrl))
        {
            sources.Add(new ManifestErrorSource(
                new HttpManifestSource(HttpClient, _config.ManifestUrl), "Custom manifest"));
        }

        return new AggregateManifestSource(sources);
    }

    private void ApplySyncResult(SyncResult result)
    {
        StatusMessage = result.Status switch
        {
            SyncStatus.Success => $"تم التحديث: {result.Added} جديد، {result.Updated} مُحدَّث.",
            SyncStatus.UpToDate => "قاعدة البيانات محدّثة بالفعل.",
            SyncStatus.Offline => "لا يوجد اتصال بالإنترنت — يتم استخدام البيانات المحلية.",
            _ => $"تعذّر التحديث: {result.Message}",
        };
    }

    private async Task DiagnoseAsync()
    {
        IsBusy = true;
        Results.Clear();
        try
        {
            await using var db = _dbFactory.Create();
            var entries = await db.ErrorEntries.AsNoTracking().ToListAsync();
            var matches = _matcher.Match(ErrorText, entries, maxResults: 8);

            foreach (var match in matches)
            {
                Results.Add(new MatchItemViewModel(match));
            }

            HasSearched = true;
            StatusMessage = Results.Count > 0
                ? $"تم العثور على {Results.Count} نتيجة محتملة."
                : "لم يتم العثور على تطابق. جرّب لصق نص الخطأ كاملاً أو حدّث قاعدة البيانات.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"حدث خطأ أثناء التشخيص: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(ShowNoResults));
        }
    }

    private async Task RefreshDatabaseInfoAsync()
    {
        await using var db = _dbFactory.Create();
        var count = await db.ErrorEntries.CountAsync();
        var meta = await db.SyncMetadata.AsNoTracking().FirstOrDefaultAsync();
        var last = meta?.LastSyncUtc is { } t ? t.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "لم يتم بعد";
        DatabaseInfo = $"عدد الأخطاء في القاعدة: {count}  |  آخر تحديث: {last}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
}
