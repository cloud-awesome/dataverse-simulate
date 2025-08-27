namespace CloudAwesome.Xrm.Simulate.DataStores;

internal sealed class MockedServiceBusStore
{
    internal List<string> Messages { get; private set; } = new();
}