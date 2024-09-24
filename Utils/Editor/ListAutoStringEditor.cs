using System.Collections;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using HandyControl.Controls;
using MFAWPF.Controls;
using MFAWPF.Utils.Converters;
using MFAWPF.Views;

namespace MFAWPF.Utils.Editor;

public class ListAutoStringEditor : PropertyEditorBase
{
    public override FrameworkElement CreateElement(PropertyItem propertyItem)
    {
        var autoListControl = new CustomAutoListControl
        {
            MinHeight = 50, TaskDialogDataList = GetItemsSource(propertyItem) ?? new ObservableCollection<string>(),
            DisplayMemberPath = GetDisplayMemberPath(propertyItem)
        };


        return autoListControl;
    }

    // 实现抽象方法，返回 ItemsProperty 作为绑定的 DependencyProperty
    public override DependencyProperty GetDependencyProperty() => CustomAutoListControl.ItemsProperty;

    // 动态设置 ItemsSource，根据字段名称定制选项
    private IEnumerable? GetItemsSource(PropertyItem propertyItem)
    {
        if (propertyItem.PropertyName.Equals("next"))
        {
            return MainWindow.TaskDialog?.Data?.DataList;
        }

        if (propertyItem.PropertyName.Equals("on_error"))
        {
            return MainWindow.TaskDialog?.Data?.DataList;
        }

        if (propertyItem.PropertyName.Equals("interrupt"))
        {
            return MainWindow.TaskDialog?.Data?.DataList;
        }

        if (propertyItem.PropertyName.Equals("roi"))
        {
            return MainWindow.TaskDialog?.Data?.DataList;
        }

        if (propertyItem.PropertyName.Contains("color"))
        {
            return MainWindow.TaskDialog?.Data?.Colors;
        }

        return new ObservableCollection<string>();
    }

    // 根据属性的字段名称，动态返回不同的 DisplayMemberPath
    private string GetDisplayMemberPath(PropertyItem propertyItem)
    {
        if (propertyItem.PropertyName.Equals("next"))
        {
            return "Name";
        }

        if (propertyItem.PropertyName.Equals("on_error"))
        {
            return "Name";
        }

        if (propertyItem.PropertyName.Equals("interrupt"))
        {
            return "Name";
        }

        if (propertyItem.PropertyName.Equals("roi"))
        {
            return "Name";
        }

        if (propertyItem.PropertyName.Contains("color"))
        {
            return "Name";
        }

        return string.Empty; // 默认的 DisplayMemberPath
    }


    protected override IValueConverter GetConverter(PropertyItem propertyItem) => new ListStringConverter();
}