namespace CloudAwesome.Xrm.Simulate.Queues;

public sealed class SimulatedQueueException(string message) : InvalidOperationException(message);
