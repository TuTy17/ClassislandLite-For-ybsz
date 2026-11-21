using System.ComponentModel;
using System.Reactive.Linq;
using System.Windows;
using Avalonia;
using Avalonia.Controls;
using ClassIsland.Core.Models.AttachedSettings;
using ClassIsland.Shared;
using ClassIsland.Shared.Abstraction.Models;
using ClassIsland.Shared.ComponentModels;
using ClassIsland.Shared.Models.Profile;

namespace ClassIsland.Core.Controls.LessonsControls;


public abstract class LessonControlBase : UserControl
{
    public static readonly StyledProperty<string> GroupRemainingTextProperty =
        AvaloniaProperty.Register<LessonControlBase, string>(nameof(GroupRemainingText), "");

    public string GroupRemainingText
    {
        get => GetValue(GroupRemainingTextProperty);
        set => SetValue(GroupRemainingTextProperty, value);
    }

    public static readonly StyledProperty<ClassInfo> ClassInfoProperty = AvaloniaProperty.Register<LessonControlBase, ClassInfo>(
        nameof(ClassInfo), new ClassInfo());
    
    public ClassInfo ClassInfo
    {
        get => GetValue(ClassInfoProperty);
        set => SetValue(ClassInfoProperty, value);
    
    }

    public static readonly AttachedProperty<ClassPlan?> ClassPlanProperty =
        AvaloniaProperty.RegisterAttached<LessonControlBase, Control, ClassPlan?>("ClassPlan", inherits: true);

    public static void SetClassPlan(Control obj, ClassPlan? value) => obj.SetValue(ClassPlanProperty, value);
    public static ClassPlan? GetClassPlan(Control obj) => obj.GetValue(ClassPlanProperty);
    

    public static readonly StyledProperty<ObservableDictionary<Guid, Subject>> SubjectsProperty = AvaloniaProperty.Register<LessonControlBase, ObservableDictionary<Guid, Subject>>(
        nameof(Subjects));

    public ObservableDictionary<Guid, Subject> Subjects
    {
        get => GetValue(SubjectsProperty);
        set => SetValue(SubjectsProperty, value);
    }

    public static readonly StyledProperty<TimeLayoutItem?> CurrentTimeLayoutItemProperty = AvaloniaProperty.Register<LessonControlBase, TimeLayoutItem?>(
        nameof(CurrentTimeLayoutItem));

    public TimeLayoutItem? CurrentTimeLayoutItem
    {
        get => GetValue(CurrentTimeLayoutItemProperty);
        set => SetValue(CurrentTimeLayoutItemProperty, value);
    }
    
    public static readonly AttachedProperty<ILessonControlSettings> DefaultLessonControlSettingsProperty =
        AvaloniaProperty.RegisterAttached<LessonControlBase, Control, ILessonControlSettings>("DefaultLessonControlSettings", new LessonControlAttachedSettings(), true);

    public static void SetDefaultLessonControlSettings(Control obj, ILessonControlSettings value) => obj.SetValue(DefaultLessonControlSettingsProperty, value);
    public static ILessonControlSettings GetDefaultLessonControlSettings(Control obj) => obj.GetValue(DefaultLessonControlSettingsProperty);

    public LessonControlBase()
    {
        this.GetObservable(ClassPlanProperty).Subscribe(_ => UpdateClassInfo());
        this.GetObservable(CurrentTimeLayoutItemProperty).Subscribe(_ => UpdateClassInfo());
        this.GetObservable(SubjectsProperty).Subscribe(_ => UpdateClassInfo());
        Unloaded += (_, _) => SubscribeSubject(null);
    }

    private Subject? _subscribedSubject;

    /// <summary>
    /// 跟踪当前科目，让"重复出现时合并显示"的勾选可以立即生效。
    /// </summary>
    private void SubscribeSubject(Subject? subject)
    {
        if (ReferenceEquals(_subscribedSubject, subject))
        {
            return;
        }

        if (_subscribedSubject != null)
        {
            _subscribedSubject.PropertyChanged -= SubjectOnPropertyChanged;
        }

        _subscribedSubject = subject;
        if (subject != null)
        {
            subject.PropertyChanged += SubjectOnPropertyChanged;
        }
    }

    private void SubjectOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Subject.MergeRepeatedLessons))
        {
            UpdateClassInfo();
        }
    }

    private void UpdateClassInfo()
    {
        GroupRemainingText = "";
        var classPlan = GetClassPlan(this);
        if (classPlan == null || CurrentTimeLayoutItem == null || classPlan.TimeLayout == null)
        {
            SubscribeSubject(null);
            return;
        }
        var subjectIndex = GetSubjectIndex(classPlan.TimeLayout.Layouts.IndexOf(CurrentTimeLayoutItem));
        if (subjectIndex >= classPlan.Classes.Count || subjectIndex < 0)
        {
            SubscribeSubject(null);
            return;
        }

        ClassInfo = classPlan.Classes[subjectIndex];
        Subject? currentSubject = null;
        Subjects?.TryGetValue(ClassInfo.SubjectId, out currentSubject);
        SubscribeSubject(currentSubject);
        UpdateGroupRemainingText(classPlan, subjectIndex);
        return;

        int GetSubjectIndex(int index)
        {
            if (index == -1)
                return -1;
            var k = classPlan.TimeLayout.Layouts[index];
            var l = classPlan.TimeLayout.Layouts.Where(t => t.TimeType == 0).ToList();
            var i = l.IndexOf(k);
            return i;
        }
    }

    private void UpdateGroupRemainingText(ClassPlan classPlan, int subjectIndex)
    {
        if (subjectIndex < 0 ||
            subjectIndex >= classPlan.Classes.Count ||
            classPlan.Classes[subjectIndex].SubjectId == Guid.Empty)
        {
            return;
        }

        var subjectId = classPlan.Classes[subjectIndex].SubjectId;

        // 只有勾选了"重复出现时合并显示"的科目才合并，其它科目逐节显示。
        if (Subjects == null ||
            !Subjects.TryGetValue(subjectId, out var subject) ||
            !subject.MergeRepeatedLessons)
        {
            return;
        }

        var groupStart = subjectIndex;
        var groupEnd = subjectIndex;

        while (groupStart > 0 && IsSameGroup(classPlan.Classes[groupStart - 1], subjectId))
        {
            groupStart--;
        }

        while (groupEnd + 1 < classPlan.Classes.Count &&
               IsSameGroup(classPlan.Classes[groupEnd + 1], subjectId))
        {
            groupEnd++;
        }

        var remaining = groupEnd - subjectIndex + 1;
        if (remaining > 1)
        {
            GroupRemainingText = $"×{remaining}";
        }
    }

    private static bool IsSameGroup(ClassInfo classInfo, Guid subjectId)
    {
        return classInfo.IsEnabled &&
               classInfo.SubjectId != Guid.Empty &&
               classInfo.SubjectId == subjectId;
    }
}
