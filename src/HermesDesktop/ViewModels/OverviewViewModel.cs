using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HermesDesktop.Models;
using HermesDesktop.Services;
using Microsoft.Extensions.Logging;

namespace HermesDesktop.ViewModels;

public partial class OverviewViewModel : ObservableObject
{
    private readonly IRemoteHermesService _hermesService;
    private readonly MainViewModel _mainVm;
    private readonly ILogger<OverviewViewModel> _logger;

    [ObservableProperty]
    private HermesOverview? _overview;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public OverviewViewModel(
        IRemoteHermesService hermesService,
        MainViewModel mainVm,
        ILogger<OverviewViewModel> logger)
    {
        _hermesService = hermesService;
        _mainVm = mainVm;
        _logger = logger;

        _ = LoadOverviewAsync();
    }

    [RelayCommand]
    private async Task LoadOverviewAsync()
    {
        if (_mainVm.ActiveConnection == null)
        {
            ErrorMessage = "当前没有活动连接，请先选择一个连接。";
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;
            Overview = await _hermesService.GetOverviewAsync(_mainVm.ActiveConnection);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to load overview");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
