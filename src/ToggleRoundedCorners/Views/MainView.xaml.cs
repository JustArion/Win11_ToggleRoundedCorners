using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Dawn.Apps.ToggleRoundedCorners;

using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows.Controls.Primitives;
using DynamicData.Binding;
using Tools;
using ViewModels;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainView
{
    public MainView(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        DisableSelections();

        ListBoxAlternateColors();
        ScrollLatestInfoView();
        
        ClearCacheButton.Click += OnClearCacheButtonClick;

        var uiUpdater = new QueuedUpdater(Dispatcher, 100);

        // TestOutputBox();
        InsertProgressBarOnAsyncWaits(viewModel, uiUpdater);

        viewModel.SubscribeCompletion(str => uiUpdater.QueueUpdate(() => WriteCompletion(str)));
        viewModel.SubscribeProgress(str => uiUpdater.QueueUpdate(() => WriteLine(str)));
        viewModel.SubscribeError(str => uiUpdater.QueueUpdate(() => WriteLineError(str)));
        viewModel.Initialize();

        
        #if RELEASE
        Topmost = true;
        #endif
    }

    private void OnClearCacheButtonClick(object sender, RoutedEventArgs e) => LoggerListBox.Items.Clear();

    private void InsertProgressBarOnAsyncWaits(MainViewModel viewModel, QueuedUpdater uiUpdater)
    {
        var progress = new ProgressBar
        {
            Margin = new Thickness(0, 5, 0, 5),
            IsIndeterminate = true
        };
        var lbi = new ListBoxItem
        {
            Content = progress
        };
        
        viewModel.WhenPropertyChanged(x => x.OperationNotInProgress).Subscribe(value =>
        {
            if (value.Value)
            {
                uiUpdater.QueueUpdate(() =>
                {
                    LoggerListBox.Items.Remove(lbi);
                });
            }
            else
            {
                uiUpdater.QueueUpdate(() =>
                {
                    LoggerListBox.Items.Add(lbi);
                });
            }
        });
    }

    private void DisableSelections()
    {
        LoggerListBox.SelectionMode = SelectionMode.Single;
        LoggerListBox.SelectionChanged += (_, _) => LoggerListBox.UnselectAll();
    }

    private void TestOutputBox()
    {
        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                WriteLine("Test WriteLine");

                await Task.Delay(1000);
                WriteLine("Delayed Test WriteLine");

                await Task.Delay(1000);

                WriteLineError("Test Error");
                WriteLine("Random Message");

                // Create a button
                var button = new Button
                {
                    Content = "Click me",
                    Margin = new Thickness(5, 2, 5, 2),
                    Style = (Style)FindResource("OptionButton"),
                    Height = 20
                };
                button.Click += (_, _) => { WriteLine("The GM is acting sus"); };
                // Add the button to the window
                LoggerListBox.Items.Add(button);
            }
#pragma warning disable CS0168 // Variable is declared but never used
            catch (Exception e)
            {
                Debugger.Break();
            }
#pragma warning restore CS0168 // Variable is declared but never used
        });
    }


    private void ScrollLatestInfoView()
    {
        var itemContainer = LoggerListBox.ItemContainerGenerator;
        itemContainer.ItemsChanged += (_, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Add)
                ScrollIntoView();
        };
    }

    private const int FONT_SIZE = 16;
    private const int LEFT_PADDING = 10;

    private void ListBoxAlternateColors()
    {
        var lightGray = new SolidColorBrush(Color.FromArgb(5, 200, 200, 200));
        var itemContainer = LoggerListBox.ItemContainerGenerator;
        itemContainer.StatusChanged += (_, _) =>
        {
            if (itemContainer.Status != GeneratorStatus.ContainersGenerated)
                return;

            for (var i = 0; i < LoggerListBox.Items.Count; i++)
            {
                var container = (ListBoxItem?)itemContainer.ContainerFromIndex(i);
                if (container?.Content is TextBlock && container.Background == Brushes.Transparent) 
                    container.Background = i % 2 == 0 ? Brushes.Transparent : lightGray;
            }
        };
    }

    private static readonly FontFamily ListBoxFamily = new("Segoe UI");

    private static TextBlock GenerateFromTemplate()
    {
        return new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontFamily = ListBoxFamily,
            FontSize = FONT_SIZE,
            FontWeight = FontWeights.Light,
            Padding = new Thickness(LEFT_PADDING, 3, 0, 3)
        };
    }
    
    private void WriteCompletion(string msg)
    {
        var tb = GenerateFromTemplate();
        tb.Text = msg;
        tb.Background = (SolidColorBrush)FindResource("CompletionMessageColor");
        LoggerListBox.Items.Add(new ListBoxItem
        {
            Content = tb
        });
    }

    private void WriteLine(string msg)
    {
        var tb = GenerateFromTemplate();
        tb.Text = msg;
        LoggerListBox.Items.Add(new ListBoxItem
        {
            Content = tb
        });
    }


    private void ScrollIntoView()
    {
        const int LAST_FROM = 1;

        LoggerListBox.ScrollIntoView(LoggerListBox.Items.Count > LAST_FROM
            ? LoggerListBox.Items[^LAST_FROM]
            : LoggerListBox.Items[^1]);
    }

    private void WriteLineError(string msg)
    {
        var tb = GenerateFromTemplate();
        tb.Text = msg;
        tb.Background = (SolidColorBrush)FindResource("ErrorMessageColor");
        LoggerListBox.Items.Add(new ListBoxItem
        {
            Content = tb
        });
    }
}