using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Crm.Sdk.Messages;
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
        var remaining = _records.DistinctBy(r => (r.LogicalName, r.Id)).Reverse().ToList();
        // Retry failures after dependent records have had their cleanup attempt.
        // A failed service call can also have completed its deletion server-side.
        for (var attempt = 0; attempt < 2 && remaining.Count > 0; attempt++)
        {
            foreach (var record in remaining.ToArray())
            {
                try
                {
                    if (record.LogicalName == "queueitem")
                    {
                        service.Execute(new ReleaseToQueueRequest { QueueItemId = record.Id });
                        service.Execute(new RemoveFromQueueRequest { QueueItemId = record.Id });
                    }
                    else
                    {
                        service.Delete(record.LogicalName, record.Id);
                    }
                    remaining.Remove(record);
                }
                catch (FaultException<OrganizationServiceFault> ex) when (ex.Detail.ErrorCode == -2147220969)
                {
                    remaining.Remove(record);
                }
                catch (Exception ex)
                {
                    if (attempt == 1)
                        TestContext.Error.WriteLine(
                            $"Cleanup failed for {record.LogicalName} {record.Id}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
    }

    public void MarkDeleted(string logicalName, Guid id)
    {
        _records.RemoveAll(record => record.LogicalName == logicalName && record.Id == id);
    }
}
