using CloudAwesome.Xrm.Simulate.Interfaces;

namespace CloudAwesome.Xrm.Simulate.Metadata;

public sealed class SimulatedMetadataService(ISimulatorOptions options)
{
    public bool IsLoaded => options.Metadata is not null;

    public SimulatedMetadata? Current => options.Metadata;

    public SimulatedMetadata Load(string path)
    {
        options.Metadata = SimulatedMetadata.Load(path);
        return options.Metadata;
    }
}
