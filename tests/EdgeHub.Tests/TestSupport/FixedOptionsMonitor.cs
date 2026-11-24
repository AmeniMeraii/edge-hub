using Microsoft.Extensions.Options;

namespace EdgeHub.Tests.TestSupport;

/// <summary>Options that never change, for classes that read <see cref="IOptionsMonitor{TOptions}"/>.</summary>
internal sealed class FixedOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue => value;

    public T Get(string? name) => value;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
