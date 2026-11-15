using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ClassIsland.Models;
using ClassIsland.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.ViewModels.SettingsPages;

public sealed class VersionRollbackSettingsViewModel : ObservableRecipient
{
    private string _statusText = "";
    private bool _isBusy;

    public ObservableCollection<VersionSnapshot> Snapshots { get; } = [];

    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public bool HasSnapshots => Snapshots.Count > 0;

    public string CurrentVersionText => VersionRollbackService.CurrentVersionText;
    public string CurrentAppDirectory => VersionRollbackService.CurrentAppDirectory;
    public string SnapshotRoot => VersionRollbackService.SnapshotRoot;

    public void Refresh()
    {
        Snapshots.Clear();
        foreach (var snapshot in VersionRollbackService.ListSnapshots())
        {
            Snapshots.Add(snapshot);
        }

        OnPropertyChanged(nameof(HasSnapshots));
        OnPropertyChanged(nameof(CurrentVersionText));
        OnPropertyChanged(nameof(CurrentAppDirectory));
    }

    public async Task CreateSnapshotAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "正在创建快照…";
        try
        {
            var snapshot = await VersionRollbackService.CreateSnapshotAsync(new Progress<string>(text => StatusText = text));
            Refresh();
            StatusText = $"已创建快照：{snapshot.DisplayText}";
        }
        catch (System.Exception exception)
        {
            StatusText = "创建快照失败：" + exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void DeleteSnapshot(VersionSnapshot snapshot)
    {
        try
        {
            VersionRollbackService.DeleteSnapshot(snapshot);
            Refresh();
            StatusText = "已删除快照：" + snapshot.Name;
        }
        catch (System.Exception exception)
        {
            StatusText = "删除快照失败：" + exception.Message;
        }
    }
}
