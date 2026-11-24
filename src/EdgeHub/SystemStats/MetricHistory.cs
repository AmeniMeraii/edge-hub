namespace EdgeHub.SystemStats;

/// <summary>The last values of one measure, oldest first, for the screen's small charts.</summary>
public sealed class MetricHistory
{
    private readonly double[] _buffer;
    private int _start;
    private int _count;

    public MetricHistory(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _buffer = new double[capacity];
    }

    public IReadOnlyList<double> Values
    {
        get
        {
            var values = new double[_count];
            for (int i = 0; i < _count; i++)
            {
                values[i] = _buffer[(_start + i) % _buffer.Length];
            }

            return values;
        }
    }

    public double? Latest => _count == 0 ? null : _buffer[(_start + _count - 1) % _buffer.Length];

    public void Add(double value)
    {
        if (_count < _buffer.Length)
        {
            _buffer[(_start + _count) % _buffer.Length] = value;
            _count++;
        }
        else
        {
            _buffer[_start] = value;
            _start = (_start + 1) % _buffer.Length;
        }
    }
}
