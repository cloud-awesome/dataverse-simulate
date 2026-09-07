using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather.ParityTesting;

public sealed class LiveDataverseCleanupScope(IOrganizationService service) : IDisposable
{
    private readonly List<EntityReference> _records = new();

    public void TrackForDelete(string logicalName, Guid id)
    {
        if (id == Guid.Empty)
        {
            return;
        }

        _records.Add(new EntityReference(logicalName, id));
    }

    public void Dispose()
    {
        foreach (var record in _records
                     .DistinctBy(r => (r.LogicalName, r.Id))
                     .Reverse())
        {
            try
            {
                service.Delete(record.LogicalName, record.Id);
            }
            catch (Exception ex)
            {
                TestContext.Error.WriteLine(
                    $"Cleanup failed for {record.LogicalName} {record.Id}: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
