using ClassIsland.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.ViewModels.SettingsPages;

public sealed class RemoteMessageSettingsViewModel : ObservableRecipient
{
    private string _statusMessage = "";

    public SettingsService SettingsService { get; }
    public RemoteMessageService RemoteMessageService { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RemoteMessageSettingsViewModel(SettingsService settingsService, RemoteMessageService remoteMessageService)
    {
        SettingsService = settingsService;
        RemoteMessageService = remoteMessageService;
    }

    public void Refresh()
    {
        StatusMessage = RemoteMessageService.StatusText;
    }
}
