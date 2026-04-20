using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HermesDesktop.Models;
using HermesDesktop.Services;
using Microsoft.Extensions.Logging;

namespace HermesDesktop.ViewModels;

public partial class SkillBrowserViewModel : ObservableObject
{
    private readonly IRemoteScriptExecutor _executor;
    private readonly MainViewModel _mainVm;
    private readonly ILogger<SkillBrowserViewModel> _logger;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _filterQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<SkillItem> _skills = new();

    [ObservableProperty]
    private ObservableCollection<SkillItem> _filteredSkills = new();

    [ObservableProperty]
    private SkillItem? _selectedSkill;

    [ObservableProperty]
    private string? _selectedSkillMarkdown;

    [ObservableProperty]
    private bool _isLoadingDetail;

    public SkillBrowserViewModel(
        IRemoteScriptExecutor executor,
        MainViewModel mainVm,
        ILogger<SkillBrowserViewModel> logger)
    {
        _executor = executor;
        _mainVm = mainVm;
        _logger = logger;

        _ = LoadSkillsAsync();
    }

    partial void OnFilterQueryChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedSkillChanged(SkillItem? value)
    {
        if (value != null)
            _ = LoadSkillDetailAsync(value);
        else
            SelectedSkillMarkdown = null;
    }

    private async Task LoadSkillDetailAsync(SkillItem skill)
    {
        if (_mainVm.ActiveConnection == null || skill.RelativePath == null) return;

        try
        {
            IsLoadingDetail = true;
            SelectedSkillMarkdown = null;

            var json = await _executor.ExecuteRawAsync(
                _mainVm.ActiveConnection, "read_skill_detail.py",
                new() { ["relative_path"] = skill.RelativePath });

            var result = System.Text.Json.JsonSerializer.Deserialize<SkillDetailResponse>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.Ok == true)
                SelectedSkillMarkdown = result.MarkdownContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load skill detail");
        }
        finally
        {
            IsLoadingDetail = false;
        }
    }

    [RelayCommand]
    private async Task LoadSkillsAsync()
    {
        if (_mainVm.ActiveConnection == null) return;

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var json = await _executor.ExecuteRawAsync(
                _mainVm.ActiveConnection, "discover_skills.py");

            var result = System.Text.Json.JsonSerializer.Deserialize<SkillListResponse>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null || !result.Ok)
            {
                ErrorMessage = result?.Error ?? "加载技能失败";
                return;
            }

            Skills = new ObservableCollection<SkillItem>(result.Items ?? new());
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(FilterQuery))
        {
            FilteredSkills = new ObservableCollection<SkillItem>(Skills);
        }
        else
        {
            var q = FilterQuery.ToLowerInvariant();
            FilteredSkills = new ObservableCollection<SkillItem>(
                Skills.Where(s =>
                    (s.Name?.ToLower().Contains(q) ?? false) ||
                    (s.Category?.ToLower().Contains(q) ?? false) ||
                    (s.Description?.ToLower().Contains(q) ?? false)));
        }
    }
}

public class SkillItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("relative_path")]
    public string? RelativePath { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    public string DisplayName => Name ?? Slug ?? Id;
    public string DisplayCategory => Category ?? "未分类";
}

public class SkillListResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("items")]
    public List<SkillItem>? Items { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class SkillDetailResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("markdown_content")]
    public string? MarkdownContent { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
