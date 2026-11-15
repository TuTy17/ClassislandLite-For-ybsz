using Avalonia;
using Avalonia.Controls;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Services;

namespace ClassIsland.Controls.Components;

[ComponentInfo("7E2A9C41-5B3D-4F8E-9A6C-1D2E3F4A5B6C", "收起按钮", "\uE96E", "收起主界面；收起后在屏幕顶部显示一个不显眼的弹出按钮。")]
public partial class CollapseButtonComponent : ComponentBase
{
    private readonly CollapseHotspotService _hotspotService;
    private bool _isRegistered;

    public CollapseButtonComponent(CollapseHotspotService hotspotService)
    {
        _hotspotService = hotspotService;
        InitializeComponent();
        AttachedToVisualTree += (_, _) => UpdateAnchor();
        LayoutUpdated += (_, _) => UpdateAnchor();
        DetachedFromVisualTree += (_, _) =>
        {
            _isRegistered = false;
            _hotspotService.Unregister(this);
        };
    }

    private void UpdateAnchor()
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }

        var point = this.TranslatePoint(new Point(Bounds.Width - 4, Bounds.Height / 2), topLevel);
        if (point == null)
        {
            return;
        }

        _isRegistered = true;
        _hotspotService.Register(this, topLevel.PointToScreen(point.Value));
    }
}