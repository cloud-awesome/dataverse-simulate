namespace CloudAwesome.Xrm.Simulate;

public sealed class SimulatedQueueException(string message) : InvalidOperationException(message);
