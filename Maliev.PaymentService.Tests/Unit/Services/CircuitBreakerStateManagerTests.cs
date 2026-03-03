using Maliev.PaymentService.Infrastructure.Resilience;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Services;

public class CircuitBreakerStateManagerTests
{
    private readonly CircuitBreakerStateManager _manager;

    public CircuitBreakerStateManagerTests()
    {
        _manager = new CircuitBreakerStateManager();
    }

    [Fact]
    public void RecordStateChange_NewProvider_ShouldCreateState()
    {
        var timestamp = DateTime.UtcNow;
        _manager.RecordStateChange("stripe", true, timestamp);

        var state = _manager.GetState("stripe");

        Assert.NotNull(state);
        Assert.Equal("stripe", state.ProviderName);
        Assert.True(state.IsOpen);
        Assert.Equal(timestamp, state.LastStateChange);
        Assert.Equal(1, state.StateChangeCount);
    }

    [Fact]
    public void RecordStateChange_ExistingProvider_ShouldIncrementCount()
    {
        var timestamp1 = DateTime.UtcNow.AddHours(-1);
        var timestamp2 = DateTime.UtcNow;

        _manager.RecordStateChange("stripe", true, timestamp1);
        _manager.RecordStateChange("stripe", false, timestamp2);

        var state = _manager.GetState("stripe");

        Assert.NotNull(state);
        Assert.False(state.IsOpen);
        Assert.Equal(2, state.StateChangeCount);
        Assert.Equal(timestamp2, state.LastStateChange);
    }

    [Fact]
    public void IsCircuitOpen_NotExists_ShouldReturnFalse()
    {
        var result = _manager.IsCircuitOpen("nonexistent");

        Assert.False(result);
    }

    [Fact]
    public void IsCircuitOpen_OpenCircuit_ShouldReturnTrue()
    {
        _manager.RecordStateChange("stripe", true, DateTime.UtcNow);

        var result = _manager.IsCircuitOpen("stripe");

        Assert.True(result);
    }

    [Fact]
    public void IsCircuitOpen_ClosedCircuit_ShouldReturnFalse()
    {
        _manager.RecordStateChange("stripe", false, DateTime.UtcNow);

        var result = _manager.IsCircuitOpen("stripe");

        Assert.False(result);
    }

    [Fact]
    public void GetAllStates_Empty_ShouldReturnEmptyDictionary()
    {
        var states = _manager.GetAllStates();

        Assert.Empty(states);
    }

    [Fact]
    public void GetAllStates_WithStates_ShouldReturnAll()
    {
        _manager.RecordStateChange("stripe", true, DateTime.UtcNow);
        _manager.RecordStateChange("omise", false, DateTime.UtcNow);

        var states = _manager.GetAllStates();

        Assert.Equal(2, states.Count);
        Assert.Contains("stripe", states.Keys);
        Assert.Contains("omise", states.Keys);
    }

    [Fact]
    public void GetState_NotExists_ShouldReturnNull()
    {
        var state = _manager.GetState("nonexistent");

        Assert.Null(state);
    }
}
