using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using CommunityToolkit.WinUI;

namespace FestiveAdventCalendar;

public sealed partial class MainPage : Page, INotifyPropertyChanged
{
    // Private Fields
    private readonly Random _random = new Random();
    private readonly DispatcherTimer _timer;
    private List<Tip> _tips = new(); // Initialize to an empty list
    private bool _isRunningSnowfall = true;

    private string _currentDateTimeFormatted = string.Empty; // Default to empty
    private int _daysLeft;
    private int _hoursLeft;
    private int _minutesLeft;
    private int _secondsLeft;

    private const string TipStateKey = "FestiveAdventCalendarTips";
    private static readonly DateTimeOffset TestDate = new(2024, 12, 23, 0, 0, 0, TimeSpan.Zero);
    private readonly bool _useTestDate = false;
    private DateTimeOffset EffectiveDate => _useTestDate ? TestDate : DateTimeOffset.Now;

    // Public Properties
    public ObservableCollection<CalendarDay> Days { get; } = new();

    public string CurrentDateTimeFormatted
    {
        get => _currentDateTimeFormatted;
        set
        {
            _currentDateTimeFormatted = value;
            OnPropertyChanged(nameof(CurrentDateTimeFormatted));
        }
    }

    public int DaysLeft
    {
        get => _daysLeft;
        set
        {
            _daysLeft = value;
            OnPropertyChanged(nameof(DaysLeft));
        }
    }

    public int HoursLeft
    {
        get => _hoursLeft;
        set
        {
            _hoursLeft = value;
            OnPropertyChanged(nameof(HoursLeft));
        }
    }

    public int MinutesLeft
    {
        get => _minutesLeft;
        set
        {
            _minutesLeft = value;
            OnPropertyChanged(nameof(MinutesLeft));
        }
    }

    public int SecondsLeft
    {
        get => _secondsLeft;
        set
        {
            _secondsLeft = value;
            OnPropertyChanged(nameof(SecondsLeft));
        }
    }

    public MainPage()
    {
        InitializeComponent();

        _tips = new List<Tip>(); // Initialize _tips
        _currentDateTimeFormatted = string.Empty; // Initialize to an empty string

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        SetupCountdownTimer();

        SnowCanvas.Loaded += (_, _) => StartSnowfall();
        Unloaded += (_, _) =>
        {
            _isRunningSnowfall = false; // Stop the snowfall process
            SnowCanvas.Children.Clear(); // Clear snowflakes to avoid lingering operations
        };

        _ = InitializeAsync();
        PopulateCalendar();
    }

    // Event Handlers
    private void DayButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is CalendarDay day)
        {
            if (day.DayNumber <= EffectiveDate.Day)
            {
                // Find the tip for the day
                var tip = _tips.FirstOrDefault(t => t.Day == day.DayNumber)?.TipContent;

                if (!string.IsNullOrEmpty(tip))
                {
                    ShowTipForDay(day.DayNumber, tip);
                    day.IsTipOpened = true;
                    SaveTipState(day.DayNumber, true);
                }
                else
                {
                    ShowWaitMessage(day.DayNumber);
                }
            }
            else
            {
                ShowWaitMessage(day.DayNumber);
            }
        }
    }

    private async void Countdown_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        ResetTipStates();

        foreach (var day in Days)
        {
            day.IsTipOpened = false;
        }

        await ShowDialogAsync(
            "🎁 Surprise Gift Unwrapped! 🎁",
            "🥚 You've discovered a hidden Easter Egg! 🥚\n✨ All tips have been magically reset for testing. Enjoy your festive fun! 🎉",
            "Ho Ho OK! 🎅"
        );
    }

    // Public Events
    public event PropertyChangedEventHandler? PropertyChanged; // Allow null

    // Protected Methods
    private void OnPropertyChanged(string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Private Methods
    private async Task InitializeAsync()
    {
        await LoadTipsAsync();
        LoadTipStates();
    }

    private void SetupCountdownTimer()
    {
        UpdateCountdown(this, EventArgs.Empty);
        _timer.Tick += UpdateCountdown;
        _timer.Start();
    }

    private void UpdateCountdown(object? sender, object? e)
    {
        DateTimeOffset targetDate = new DateTimeOffset(EffectiveDate.Year, 12, 25, 0, 0, 0, EffectiveDate.Offset);
        TimeSpan timeLeft = targetDate - EffectiveDate;

        UpdateProperty(() => CurrentDateTimeFormatted =
            $"Today: {EffectiveDate:dddd, MMMM d, yyyy - h:mm tt} (UTC{EffectiveDate.Offset.Hours:+00;-00}:{EffectiveDate.Offset.Minutes:00})");
        UpdateProperty(() => DaysLeft = (int)timeLeft.TotalDays);
        UpdateProperty(() => HoursLeft = timeLeft.Hours);
        UpdateProperty(() => MinutesLeft = timeLeft.Minutes);
        UpdateProperty(() => SecondsLeft = timeLeft.Seconds);
    }

    private void UpdateProperty(Action updateAction)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            updateAction();
            OnPropertyChanged();
        });
    }

    private void PopulateCalendar()
    {
        foreach (var item in GetCalendarItems())
        {
            Days.Add(item);
            CalendarGrid.Children.Add(CreateCalendarButton(item));
        }
    }

    private Button CreateCalendarButton(CalendarDay item)
    {
        var button = new Button
        {
            Style = (Style)Resources["CalendarDayButtonStyle"],
            DataContext = item
        };
        button.Click += DayButton_Click;

        Grid.SetRow(button, item.Row);
        Grid.SetColumn(button, item.Column);
        Grid.SetRowSpan(button, item.RowSpan);
        Grid.SetColumnSpan(button, item.ColumnSpan);

        return button;
    }

    private async Task LoadTipsAsync()
    {
        try
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///AppData/CSharpTips.json"));
            string json = await FileIO.ReadTextAsync(file);
            _tips = JsonSerializer.Deserialize<List<Tip>>(json) ?? new List<Tip>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading tips: {ex.Message}");
        }
    }

    private void LoadTipStates()
    {
        string savedState = ApplicationData.Current.LocalSettings.Values[TipStateKey] as string;
        if (string.IsNullOrEmpty(savedState)) return;

        var tipStates = JsonSerializer.Deserialize<Dictionary<int, bool>>(savedState) ?? new();
        foreach (var (dayNumber, isOpened) in tipStates)
        {
            var day = Days.FirstOrDefault(d => d.DayNumber == dayNumber);
            if (day != null)
            {
                day.IsTipOpened = isOpened; // Directly set the property.
            }
        }
    }

    private void SaveTipState(int dayNumber, bool isOpened)
    {
        // Get the saved state as a string
        string savedState = ApplicationData.Current.LocalSettings.Values[TipStateKey] as string ?? string.Empty;

        // Deserialize existing states or create a new dictionary if the saved state is invalid
        var tipStates = !string.IsNullOrWhiteSpace(savedState)
            ? JsonSerializer.Deserialize<Dictionary<int, bool>>(savedState) ?? new Dictionary<int, bool>()
            : new Dictionary<int, bool>();

        // Update the state for the specific day
        tipStates[dayNumber] = isOpened;

        // Save the updated state back to local settings
        ApplicationData.Current.LocalSettings.Values[TipStateKey] = JsonSerializer.Serialize(tipStates);
    }


    private async void ShowTipForDay(int dayNumber, string tip)
    {
        await DispatcherQueue.EnqueueAsync(async () =>
        {
            var closeButtonText = dayNumber == 25 ? "Enjoy the Holidays! 🎉" : "Unwrap More 🎁";

            var dialog = new ContentDialog
            {
                Title = $"🎄 C# Festive Tip for Day {dayNumber} 🎁",
                Content = $"{tip}\n\n🎅 Ho Ho Ho! Keep coding merrily along!",
                CloseButtonText = closeButtonText,
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        });
    }

    private async void ShowWaitMessage(int dayNumber)
    {
        await DispatcherQueue.EnqueueAsync(async () =>
        {
            DateTime targetDay = new DateTime(EffectiveDate.Year, 12, dayNumber);
            var daysLeft = (targetDay.Date - EffectiveDate.Date).Days;

            string title = daysLeft >= 10 ? "🎅 Hold Your Sleigh! 🎄" : "🎄 Almost There! 🎁";
            string message = $"The gift for Day {dayNumber} isn't ready yet!\nYou need to wait {daysLeft} more day{(daysLeft > 1 ? "s" : "")} until it's time to unwrap this C# tip!";

            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "🎁 Got it! 🎄",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        });
    }

    private async Task ShowDialogAsync(string title, string content, string closeButtonText)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = closeButtonText,
            XamlRoot = XamlRoot
        };

        await DispatcherQueue.EnqueueAsync(() => dialog.ShowAsync());
    }

    // Start the snowfall animation, generating snowflakes in batches.
    private async void StartSnowfall()
    {
        // Ensure DispatcherQueue is initialized properly
        if (Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread() == null)
        {
            System.Diagnostics.Debug.WriteLine("DispatcherQueue is not available. Snowfall cannot be started.");
            return;
        }

        int totalSnowflakes = 100;
        int flakesPerBatch = 5;

        await Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().EnqueueAsync(async () =>
        {
            for (int i = 0; i < totalSnowflakes; i += flakesPerBatch)
            {
                for (int j = 0; j < flakesPerBatch; j++)
                {
                    var snowFlake = CreateSnowflake();
                    if (snowFlake != null)
                    {
                        SnowCanvas.Children.Add(snowFlake);
                        AnimateSnowflake(snowFlake); // Fire-and-forget animation.
                    }
                }

                // Stagger snowflake generation.
                await Task.Delay(500);
            }
        });
    }

    // Create an individual snowflake with random properties.
    private Ellipse? CreateSnowflake()
    {
        int size = _random.Next(3, 8);

        // Ensure valid dimensions for the canvas.
        if (SnowCanvas.ActualWidth <= 0 || SnowCanvas.ActualHeight <= 0)
        {
            System.Diagnostics.Debug.WriteLine("SnowCanvas dimensions are not valid.");
            return null;
        }

        var snowflake = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new SolidColorBrush(Colors.White),
            Opacity = _random.NextDouble() * 0.8 + 0.2 // Random opacity for natural effect.
        };

        // Random starting position for snowflake.
        double leftPosition = _random.Next(0, (int)SnowCanvas.ActualWidth);
        double topPosition = -size;

        Canvas.SetLeft(snowflake, leftPosition);
        Canvas.SetTop(snowflake, topPosition);

        return snowflake;
    }

    // Animate a snowflake falling down the screen.
    private async void AnimateSnowflake(Ellipse snowFlake)
    {
        double duration = _random.Next(10000, 20000);

        while (_isRunningSnowfall)
        {
            // Ensure UI access using DispatcherQueue and check for valid canvas and snowflake
            if (Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread() == null || snowFlake == null || SnowCanvas == null || !this.IsLoaded)
            {
                return; // Exit if DispatcherQueue, snowflake, or canvas is no longer valid.
            }

            double fullHeight = Math.Max(XamlRoot.Size.Height, SnowCanvas.ActualHeight);
            double startTop = Canvas.GetTop(snowFlake);
            double endTop = fullHeight + snowFlake.Height;

            double startLeft = Canvas.GetLeft(snowFlake);
            double maxDrift = _random.Next(-20, 20);

            // Simulate falling with drift.
            for (double t = 0; t < 1; t += 0.01)
            {
                if (!_isRunningSnowfall) return;

                await Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().EnqueueAsync(() =>
                {
                    if (snowFlake == null || SnowCanvas == null || !this.IsLoaded) return;

                    double newTop = startTop + (endTop - startTop) * t;
                    Canvas.SetTop(snowFlake, newTop);

                    // Increase the drift by multiplying maxDrift by a factor (e.g., 3.0)
                    double drift = startLeft + maxDrift * Math.Sin(t * Math.PI); // Add lateral drift
                    Canvas.SetLeft(snowFlake, drift);
                });

                await Task.Delay(TimeSpan.FromMilliseconds(duration / 100));
            }

            // Reset snowflake position after it falls.
            await Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().EnqueueAsync(() =>
            {
                if (SnowCanvas == null || snowFlake == null)
                {
                    return;
                }
                Canvas.SetTop(snowFlake, -snowFlake.Height);
                Canvas.SetLeft(snowFlake, _random.Next(0, (int)SnowCanvas.ActualWidth));
            });
        }
    }

    private ObservableCollection<CalendarDay> GetCalendarItems()
    {
        return new()
        {
            new CalendarDay
            {
                DayNumber = 1,
                WideTextSize = 60,
                WideIconSize = 40,
                NarrowTextSize = 44,
                NarrowIconSize = 20,
                IconHorizontalAlignment = HorizontalAlignment.Left,
                IconVerticalAlignment = VerticalAlignment.Top,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Center,
                Row = 0,
                Column = 0
            },
            new CalendarDay
            {
                DayNumber = 2,
                WideTextSize = 60,
                WideIconSize = 38,
                NarrowTextSize = 39,
                NarrowIconSize = 35,
                IconHorizontalAlignment = HorizontalAlignment.Right,
                IconVerticalAlignment = VerticalAlignment.Top,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Bottom,
                Row = 1,
                Column = 1
            },
            new CalendarDay
            {
                DayNumber = 3,
                WideTextSize = 80,
                WideIconSize = 50,
                NarrowTextSize = 60,
                NarrowIconSize = 29,
                IconHorizontalAlignment = HorizontalAlignment.Left,
                IconVerticalAlignment = VerticalAlignment.Top,
                TextHorizontalAlignment = HorizontalAlignment.Right,
                TextVerticalAlignment = VerticalAlignment.Bottom,
                Row = 2,
                Column = 3,
                ColumnSpan = 2
            },
            new CalendarDay
            {
                DayNumber = 4,
                WideTextSize = 70,
                WideIconSize = 38,
                NarrowTextSize = 40,
                NarrowIconSize = 28,
                IconHorizontalAlignment = HorizontalAlignment.Right,
                IconVerticalAlignment = VerticalAlignment.Top,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Bottom,
                Row = 4,
                Column = 0
            },
            new CalendarDay
            {
                DayNumber = 5,
                WideTextSize = 90,
                WideIconSize = 25,
                NarrowTextSize = 45,
                NarrowIconSize = 32,
                IconHorizontalAlignment = HorizontalAlignment.Right,
                IconVerticalAlignment = VerticalAlignment.Top,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Bottom,
                Row = 5,
                Column = 4
            },
            new CalendarDay
            {
                DayNumber = 6,
                WideTextSize = 80,
                WideIconSize = 32,
                NarrowTextSize = 36,
                NarrowIconSize = 28,
                IconHorizontalAlignment = HorizontalAlignment.Left,
                IconVerticalAlignment = VerticalAlignment.Bottom,
                TextHorizontalAlignment = HorizontalAlignment.Right,
                TextVerticalAlignment = VerticalAlignment.Top,
                Row = 3,
                Column = 0
            },
            new CalendarDay
            {
                DayNumber = 7,
                WideTextSize = 90,
                WideIconSize = 42,
                NarrowTextSize = 39,
                NarrowIconSize = 36,
                IconHorizontalAlignment = HorizontalAlignment.Right,
                IconVerticalAlignment = VerticalAlignment.Top,
                TextHorizontalAlignment = HorizontalAlignment.Left,
                TextVerticalAlignment = VerticalAlignment.Bottom,
                Row = 0,
                Column = 3
            },
            new CalendarDay
            {
                DayNumber = 8,
                WideTextSize = 90,
                WideIconSize = 26,
                NarrowTextSize = 32,
                NarrowIconSize = 21,
                IconHorizontalAlignment = HorizontalAlignment.Right,
                IconVerticalAlignment = VerticalAlignment.Bottom,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Center,
                Row = 4,
                Column = 1
            },
            new CalendarDay
            {
                DayNumber = 9,
                WideTextSize = 90,
                WideIconSize = 16,
                NarrowTextSize = 38,
                NarrowIconSize = 25,
                IconHorizontalAlignment = HorizontalAlignment.Left,
                IconVerticalAlignment = VerticalAlignment.Top,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Bottom,
                Row = 2,
                Column = 2
            },
            new CalendarDay
            {
                DayNumber = 10,
                WideTextSize = 55,
                WideIconSize = 22,
                NarrowTextSize = 42,
                NarrowIconSize = 29,
                IconHorizontalAlignment = HorizontalAlignment.Right,
                IconVerticalAlignment = VerticalAlignment.Bottom,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Top,
                Row = 2,
                Column = 5
            },
            new CalendarDay
            {
                DayNumber = 11,
                WideTextSize = 60,
                WideIconSize = 27,
                NarrowTextSize = 40,
                NarrowIconSize = 32,
                IconHorizontalAlignment = HorizontalAlignment.Left,
                IconVerticalAlignment = VerticalAlignment.Top,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Bottom,
                Row = 3,
                Column = 1
            },
            new CalendarDay
            {
                DayNumber = 12,
                WideTextSize = 60,
                WideIconSize = 15,
                NarrowTextSize = 43,
                NarrowIconSize = 26,
                IconHorizontalAlignment = HorizontalAlignment.Right,
                IconVerticalAlignment = VerticalAlignment.Top,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Center,
                Row = 1,
                Column = 3
            },
            new CalendarDay
            {
                DayNumber = 13,
                WideTextSize = 90,
                WideIconSize = 18,
                NarrowTextSize = 44,
                NarrowIconSize = 30,
                IconHorizontalAlignment = HorizontalAlignment.Right,
                IconVerticalAlignment = VerticalAlignment.Top,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Center,
                Row = 3,
                Column = 4,
                ColumnSpan = 2
            },
            new CalendarDay
            {
                DayNumber = 14,
                WideTextSize = 60,
                WideIconSize = 42,
                NarrowTextSize = 38,
                NarrowIconSize = 32,
                IconHorizontalAlignment = HorizontalAlignment.Right,
                IconVerticalAlignment = VerticalAlignment.Bottom,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Center,
                Row = 0,
                Column = 2,
                RowSpan = 2
            },
            new CalendarDay
            {
                DayNumber = 15,
                WideTextSize = 60,
                WideIconSize = 35,
                NarrowTextSize = 44,
                NarrowIconSize = 27,
                IconHorizontalAlignment = HorizontalAlignment.Left,
                IconVerticalAlignment = VerticalAlignment.Bottom,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Top,
                Row = 3,
                Column = 3
            },
            new CalendarDay
            {
                DayNumber = 16,
                WideTextSize = 50,
                WideIconSize = 18,
                NarrowTextSize = 42,
                NarrowIconSize = 30,
                IconHorizontalAlignment = HorizontalAlignment.Right,
                IconVerticalAlignment = VerticalAlignment.Top,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Bottom,
                Row = 0,
                Column = 1
            },
            new CalendarDay
            {
                DayNumber = 17,
                WideTextSize = 50,
                WideIconSize = 35,
                NarrowTextSize = 43,
                NarrowIconSize = 28,
                IconHorizontalAlignment = HorizontalAlignment.Left,
                IconVerticalAlignment = VerticalAlignment.Bottom,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Top,
                Row = 0,
                Column = 6
            },
            new CalendarDay
            {
                DayNumber = 18,
                WideTextSize = 80,
                WideIconSize = 38,
                NarrowTextSize = 70,
                NarrowIconSize = 31,
                IconHorizontalAlignment = HorizontalAlignment.Right,
                IconVerticalAlignment = VerticalAlignment.Bottom,
                TextHorizontalAlignment = HorizontalAlignment.Left,
                TextVerticalAlignment = VerticalAlignment.Top,
                Row = 4,
                Column = 2,
                ColumnSpan = 2
            },
            new CalendarDay
            {
                DayNumber = 19,
                WideTextSize = 45,
                WideIconSize = 25,
                NarrowTextSize = 40,
                NarrowIconSize = 28,
                IconHorizontalAlignment = HorizontalAlignment.Left,
                IconVerticalAlignment = VerticalAlignment.Top,
                TextHorizontalAlignment = HorizontalAlignment.Right,
                TextVerticalAlignment = VerticalAlignment.Bottom,
                Row = 1,
                Column = 6
            },
            new CalendarDay
            {
                DayNumber = 20,
                WideTextSize = 45,
                WideIconSize = 22,
                NarrowTextSize = 43,
                NarrowIconSize = 19,
                IconHorizontalAlignment = HorizontalAlignment.Left,
                IconVerticalAlignment = VerticalAlignment.Bottom,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Center,
                Row = 2,
                Column = 1
            },
            new CalendarDay
            {
                DayNumber = 21,
                WideTextSize = 60,
                WideIconSize = 40,
                NarrowTextSize = 44,
                NarrowIconSize = 30,
                IconHorizontalAlignment = HorizontalAlignment.Right,
                IconVerticalAlignment = VerticalAlignment.Bottom,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Center,
                Row = 2,
                Column = 6,
                RowSpan = 3
            },
            new CalendarDay
            {
                DayNumber = 22,
                WideTextSize = 35,
                WideIconSize = 32,
                NarrowTextSize = 39,
                NarrowIconSize = 20,
                IconHorizontalAlignment = HorizontalAlignment.Right,
                IconVerticalAlignment = VerticalAlignment.Bottom,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Center,
                Row = 3,
                Column = 2
            },
            new CalendarDay
            {
                DayNumber = 23,
                WideTextSize = 38,
                WideIconSize = 22,
                NarrowTextSize = 40,
                NarrowIconSize = 25,
                IconHorizontalAlignment = HorizontalAlignment.Left,
                IconVerticalAlignment = VerticalAlignment.Bottom,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Top,
                Row = 4,
                Column = 5
            },
            new CalendarDay
            {
                DayNumber = 24,
                WideTextSize = 60,
                WideIconSize = 26,
                NarrowTextSize = 42,
                NarrowIconSize = 30,
                IconHorizontalAlignment = HorizontalAlignment.Left,
                IconVerticalAlignment = VerticalAlignment.Top,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Center,
                Row = 1,
                Column = 0,
                RowSpan = 2
            },
            new CalendarDay
            {
                DayNumber = 25,
                WideTextSize = 120,
                WideIconSize = 60,
                NarrowTextSize = 100,
                NarrowIconSize = 45,
                IconHorizontalAlignment = HorizontalAlignment.Right,
                IconVerticalAlignment = VerticalAlignment.Bottom,
                TextHorizontalAlignment = HorizontalAlignment.Center,
                TextVerticalAlignment = VerticalAlignment.Center,
                Row = 0,
                Column = 4,
                ColumnSpan = 2,
                RowSpan = 2
            }
        };
    }

    private void ResetTipStates()
    {
        // Reset to an empty dictionary
        ApplicationData.Current.LocalSettings.Values[TipStateKey] = JsonSerializer.Serialize(new Dictionary<int, bool>());
    }
}
