using System.ComponentModel;

namespace FestiveAdventCalendar;

public class CalendarDay : INotifyPropertyChanged
{
    public int DayNumber { get; set; }
    public int Row { get; set; }
    public int Column { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColumnSpan { get; set; } = 1;
    public double WideIconSize { get; set; }
    public double WideTextSize { get; set; }
    public double NarrowIconSize { get; set; }
    public double NarrowTextSize { get; set; }
    public HorizontalAlignment IconHorizontalAlignment { get; set; }
    public VerticalAlignment IconVerticalAlignment { get; set; }
    public HorizontalAlignment TextHorizontalAlignment { get; set; }
    public VerticalAlignment TextVerticalAlignment { get; set; }

    private bool _isTipOpened;
    public bool IsTipOpened
    {
        get => _isTipOpened;
        set
        {
            if (_isTipOpened != value)
            {
                _isTipOpened = value;
                OnPropertyChanged(nameof(IsTipOpened));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
