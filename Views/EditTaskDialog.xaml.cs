﻿using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MFAWPF.Controls;
using MFAWPF.Utils;
using MFAWPF.ViewModels;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Tools.Command;
using Microsoft.Win32;
using Newtonsoft.Json;
using Attribute = MFAWPF.Utils.Attribute;
using ScrollViewer = System.Windows.Controls.ScrollViewer;

namespace MFAWPF.Views;

public partial class EditTaskDialog
{
    private List<TaskModel> tasks;
    public EditTaskDialogViewModel? Data { get; set; }

    public EditTaskDialog()
    {
        InitializeComponent();
        tasks = new List<TaskModel>();
        Data = DataContext as EditTaskDialogViewModel;
        if (Data is not null)
            Data.Dialog = this;
    }


    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        MainWindow.TaskDialog = null;
        if (MainWindow.Data is not null)
            MainWindow.Data.Idle = true;
    }

    private void List_KeyDown(object sender, KeyEventArgs e)
    {
        if (Data is not { CurrentTask: not null, DataList: not null } || e is not { Key: Key.Delete })
            return;

        var itemToDelete = Data.CurrentTask;
        int index = Data.DataList?.IndexOf(itemToDelete) ?? -1;
        if (index == -1)
            return;

        Data?.DataList?.Remove(itemToDelete);
        Data?.UndoStack.Push(new RelayCommand(_ => Data?.DataList?.Insert(index, itemToDelete)));
    }

    public void Save(object? sender, RoutedEventArgs? e)
    {
        if (Data?.DataList?.Count(t => !string.IsNullOrWhiteSpace(t.Name) && t.Name.Equals(TaskName.Text)) > 1)
        {
            Growls.Error(string.Format("DuplicateTaskNameError".GetLocalizationString(), TaskName.Text));
            return;
        }

        if (Data?.CurrentTask?.Task is not null)
        {
            // Data.CurrentTask.Task.Reset();
            Data.CurrentTask.Task.Name = TaskName.Text;

            // foreach (var button in Parts.Children.OfType<AttributeButton>())
            // {
            //     if (button.Attribute is not null)
            //     {
            //         Data.CurrentTask.Task.Set(button.Attribute);
            //     }
            // }

            Data.CurrentTask.Task = Data.CurrentTask.Task;
            _chartDialog?.UpdateGraph();
            Growl.SuccessGlobal("SaveSuccessMessage".GetLocalizationString());
        }
        else
        {
            Growls.ErrorGlobal("SaveFailureMessage".GetLocalizationString());
        }
    }

    private TaskFlowChartDialog? _chartDialog;

    private void ShowChart(object sender, RoutedEventArgs e)
    {
        _chartDialog = new TaskFlowChartDialog(this);
        _chartDialog.Show();
    }

    private string PipelineFilePath = MaaProcessor.ResourcePipelineFilePath;

    private void Load(object sender, RoutedEventArgs e)
    {
        OpenFileDialog openFileDialog = new OpenFileDialog
        {
            Title = "LoadPipelineTitle".GetLocalizationString(),
            Filter = "JSONFilter".GetLocalizationString()
        };

        if (openFileDialog.ShowDialog() == true)
        {
            string filePath = openFileDialog.FileName;
            string fileName = Path.GetFileName(filePath);
            PipelineFilePath = filePath.Replace(fileName, "");
            PipelineFileName.Text = fileName;
            try
            {
                string jsonText = File.ReadAllText(filePath);
                Dictionary<string, TaskModel>? taskDictionary =
                    JsonConvert.DeserializeObject<Dictionary<string, TaskModel>>(jsonText);
                Data?.DataList?.Clear();
                if (taskDictionary == null || taskDictionary.Count == 0)
                    return;
                foreach (var VARIABLE in taskDictionary)
                {
                    VARIABLE.Value.Name = VARIABLE.Key;
                    Data?.DataList?.Add(new TaskItemViewModel()
                    {
                        Task = VARIABLE.Value
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Growls.ErrorGlobal(string.Format("LoadPipelineErrorMessage".GetLocalizationString(), ex.Message));
            }
        }
    }


    private void ScrollListBoxToBottom()
    {
        if (ListBoxDemo.Items.Count > 0)
        {
            var lastItem = ListBoxDemo.Items;
            ListBoxDemo.ScrollIntoView(lastItem);
        }
    }

    private void AddTask(object sender, RoutedEventArgs e)
    {
        Data?.DataList?.Add(new TaskItemViewModel
        {
            Task = new TaskModel()
        });
        ScrollListBoxToBottom();
        _chartDialog?.UpdateGraph();
    }

    private void TaskSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        TaskItemViewModel? taskItemViewModel = ListBoxDemo.SelectedValue as TaskItemViewModel;
        if (Data != null)
            Data.CurrentTask = taskItemViewModel;
    }

    public void Save_Pipeline(object? sender, RoutedEventArgs? e)
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };
        Dictionary<string, TaskModel> taskModels = new();
        foreach (var VARIABLE in ListBoxDemo.ItemsSource)
        {
            if (VARIABLE is TaskItemViewModel taskItemViewModel)
            {
                if (taskItemViewModel.Task != null &&
                    !taskModels.TryAdd(taskItemViewModel.Name, taskItemViewModel.Task))
                {
                    Growls.WarningGlobal("SavePipelineWarning".GetLocalizationString());
                    return;
                }
            }
        }

        SaveFileDialog saveFileDialog = new SaveFileDialog
        {
            Filter = "JSONFilter".GetLocalizationString(),
            DefaultExt = ".json",
            AddExtension = true
        };

        bool? result = saveFileDialog.ShowDialog();

        if (result == true)
        {
            string filePath = saveFileDialog.FileName;
            string jsonString = JsonConvert.SerializeObject(taskModels, settings);

            File.WriteAllText(filePath, jsonString);
            Growl.SuccessGlobal("SavePipelineSuccess".GetLocalizationString());
        }
    }

    private void OnSearchTask(object? sender, FunctionEventArgs<string> e)
    {
        var searchText = e.Info?.ToLower() ?? string.Empty;
        var filteredTasks = Data?.DataList?.Where(t =>
            t.Task?.Name != null && t.Task.Name.ToLower().Contains(searchText) ||
            t.Task?.Recognition != null && t.Task.Recognition.ToLower().Contains(searchText) ||
            t.Task?.Action != null && t.Task.Action.ToLower().Contains(searchText) ||
            t.Task?.Next != null && t.Task.Next.Any(n => n.ToLower().Contains(searchText))
        ).ToList();

        ListBoxDemo.ItemsSource = filteredTasks;
    }

    private void ClearTask(object sender, RoutedEventArgs e)
    {
        Data?.DataList?.Clear();
    }

    private void Cut(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem
            {
                Parent: ContextMenu
                {
                    PlacementTarget: ListBoxItem { DataContext: TaskItemViewModel taskItemViewModel } item
                }
            })
        {
            Clipboard.SetDataObject(taskItemViewModel.ToString());
            if (ItemsControl.ItemsControlFromItemContainer(item) is ListBox listBox)
            {
                // 获取选中项的索引
                var index = listBox.Items.IndexOf(item.DataContext);
                Data?.DataList?.RemoveAt(index);
                Data?.UndoStack.Push(new RelayCommand(_ => Data?.DataList?.Insert(index, taskItemViewModel)));
            }
        }
    }

    private void Copy(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem
            {
                Parent: ContextMenu
                {
                    PlacementTarget: ListBoxItem { DataContext: TaskItemViewModel taskItemViewModel }
                }
            })
        {
            Clipboard.SetDataObject(taskItemViewModel.ToString());
        }
    }

    private void PasteAbove(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem
            {
                Parent: ContextMenu
                {
                    PlacementTarget: ListBoxItem { DataContext: TaskItemViewModel taskItemViewModel } item
                }
            } &&
            ItemsControl.ItemsControlFromItemContainer(item) is ListBox listBox)
        {
            // 获取选中项的索引
            int index = listBox.Items.IndexOf(taskItemViewModel);
            var iData = Clipboard.GetDataObject();
            if (iData?.GetDataPresent(DataFormats.Text) == true)
            {
                try
                {
                    Dictionary<string, TaskModel>? taskModels =
                        JsonConvert.DeserializeObject<Dictionary<string, TaskModel>>(
                            (string)iData.GetData(DataFormats.Text));
                    if (taskModels == null || taskModels.Count == 0)
                        return;
                    foreach (var VARIABLE in taskModels)
                    {
                        VARIABLE.Value.Name = VARIABLE.Key;
                        var newItem = new TaskItemViewModel()
                        {
                            Name = VARIABLE.Key, Task = VARIABLE.Value
                        };
                        Data?.DataList?.Insert(index, newItem);
                        Data?.UndoStack?.Push(new RelayCommand(_ => Data?.DataList?.Remove(newItem)));
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
            else
            {
                Growls.ErrorGlobal("ClipboardDataError".GetLocalizationString());
            }
        }
    }

    private void PasteBelow(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem
            {
                Parent: ContextMenu
                {
                    PlacementTarget: ListBoxItem { DataContext: TaskItemViewModel taskItemViewModel } item
                }
            } &&
            ItemsControl.ItemsControlFromItemContainer(item) is ListBox listBox)
        {
            // 获取选中项的索引
            var index = listBox.Items.IndexOf(item.DataContext);
            var iData = Clipboard.GetDataObject();
            if (iData.GetDataPresent(DataFormats.Text))
            {
                try
                {
                    var taskModels =
                        JsonConvert.DeserializeObject<Dictionary<string, TaskModel>>(
                            (string)iData.GetData(DataFormats.Text));
                    if (taskModels == null || taskModels.Count == 0)
                        return;
                    foreach (var VARIABLE in taskModels)
                    {
                        VARIABLE.Value.Name = VARIABLE.Key;
                        var newItem = new TaskItemViewModel()
                        {
                            Name = VARIABLE.Key, Task = VARIABLE.Value
                        };
                        Data?.DataList?.Insert(index + 1, newItem);
                        Data?.UndoStack?.Push(new RelayCommand(_ => Data?.DataList?.Remove(newItem)));
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
            else
            {
                Growls.ErrorGlobal("ClipboardDataError".GetLocalizationString());
            }
        }
    }

    private void Delete(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem
            {
                Parent: ContextMenu
                {
                    PlacementTarget: ListBoxItem { DataContext: TaskItemViewModel taskItemViewModel } item
                }
            } &&
            ItemsControl.ItemsControlFromItemContainer(item) is ListBox listBox)
        {
            // 获取选中项的索引
            int index = listBox.Items.IndexOf(item.DataContext);
            Data?.DataList?.RemoveAt(index);
            Data?.UndoStack.Push(new RelayCommand(_ => Data?.DataList?.Insert(index, taskItemViewModel)));
        }
    }

    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scrollViewer = sender as ScrollViewer;
        if (scrollViewer == null) return;
        var scrollChange = e.Delta * 0.5; // 根据滚动幅度调整的比例系数
        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - scrollChange);

        e.Handled = true;
    }

    private void SelectionRegion(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.ConnectToMAA();
        var image = MaaProcessor.Instance.GetBitmapImage();
        if (image != null)
        {
            SelectionRegionDialog imageDialog = new SelectionRegionDialog(image);
            if (imageDialog.ShowDialog() == true)
            {
                if (Data?.CurrentTask?.Task != null)
                {
                    if (imageDialog.IsRoi)
                    {
                        if (imageDialog.Output != null)
                        {
                            Data.CurrentTask.Task.Roi = imageDialog.Output;
                        }
                    }
                    else
                    {
                        if (imageDialog.Output != null)
                        {
                            Data.CurrentTask.Task.Target = imageDialog.Output;
                        }
                    }

                    Data.CurrentTask = Data.CurrentTask;
                }
            }
        }
    }

    private void Screenshot(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.ConnectToMAA();
        var image = MaaProcessor.Instance.GetBitmapImage();
        if (image != null)
        {
            CropImageDialog imageDialog = new CropImageDialog(image);
            if (imageDialog.ShowDialog() == true)
            {
                if (Data?.CurrentTask?.Task != null)
                {
                    if (Data.CurrentTask.Task.Template == null && imageDialog.Output != null)
                    {
                        Data.CurrentTask.Task.Template = new List<string> { imageDialog.Output };
                    }
                    else
                    {
                        if (Data.CurrentTask.Task.Template is List<string> ls)
                        {
                            if (imageDialog.Output != null)
                                ls.Add(imageDialog.Output);
                            Data.CurrentTask.Task.Template = ls.ToList();
                        }
                    }

                    Data.CurrentTask = Data.CurrentTask;
                }
            }
        }
    }

    private void Swipe(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.ConnectToMAA();
        var image = MaaProcessor.Instance.GetBitmapImage();
        if (image != null)
        {
            SwipeDialog imageDialog = new SwipeDialog(image);
            if (imageDialog.ShowDialog() == true)
            {
                if (Data?.CurrentTask?.Task != null)
                {
                    Data.CurrentTask.Task.Begin = imageDialog.OutputBegin;
                    Data.CurrentTask.Task.End = imageDialog.OutputEnd;
                    Data.CurrentTask = Data.CurrentTask;
                }
            }
        }
    }

    private void ColorExtraction(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.ConnectToMAA();
        var image = MaaProcessor.Instance.GetBitmapImage();
        if (image != null)
        {
            ColorExtractionDialog imageDialog = new ColorExtractionDialog(image);
            if (imageDialog.ShowDialog() == true)
            {
                if (Data?.CurrentTask?.Task != null)
                {
                    Data.CurrentTask.Task.Upper = imageDialog.OutputUpper;
                    Data.CurrentTask.Task.Lower = imageDialog.OutputLower;
                    Data.CurrentTask = Data.CurrentTask;
                }
            }
        }
    }

    private void RecognitionText(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.ConnectToMAA();
        var image = MaaProcessor.Instance.GetBitmapImage();
        if (image != null)
        {
            RecognitionTextDialog imageDialog = new RecognitionTextDialog(image);
            if (imageDialog.ShowDialog() == true)
            {
                if (Data?.CurrentTask?.Task != null && imageDialog.Output != null)
                {
                    string text = OCRHelper.ReadTextFromMAATasker(
                        imageDialog.Output[0], imageDialog.Output[1],
                        imageDialog.Output[2], imageDialog.Output[3]);
                    if (Data.CurrentTask.Task.Expected == null)
                    {
                        Data.CurrentTask.Task.Expected = new List<string> { text };
                    }
                    else
                    {
                        if (Data.CurrentTask.Task.Expected is List<string> ls)
                        {
                            ls.Add(text);
                            Data.CurrentTask.Task.Expected = ls;
                        }
                    }

                    Data.CurrentTask = Data.CurrentTask;
                }
            }
        }
    }
}