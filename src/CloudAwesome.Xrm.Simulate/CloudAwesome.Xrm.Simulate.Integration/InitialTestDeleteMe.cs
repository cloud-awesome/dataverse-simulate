using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather;

/// <summary>
/// This intended use of this project is to create both real integration and mocked tests.
/// 
/// 1. Compare the results of a live API call with the results of a mocked API call;
///     and fix any issues found in both .Simulate and .Simulate.Test
///
/// 2. Investigate what a live API call returns (e.g. which exception/details is thrown in x case);
///     Implement it in the mock; test that the two are equivalent (i.e. Green-Gather-Red-Green)
///
/// 3. When MS releases a new version, re-run all tests to see if any implementation has changed etc; 
///     Mirror in the mock etc...
/// 
/// </summary>
[TestFixture]
public class InitialTestDeleteMe: IntegrationBaseFixture
{
    private readonly IOrganizationService _mockService = null!; 
    
    [Test]
    public void Temp()
    {
        // Live
        var service = DataverseConnectionManager.Instance.GetConnection();
        var liveEntity = CreateAndRetrieveContact(service, _arthur, true);
        
        // Mocked
        var mockService = _mockService.Simulate();
        var mockedEntity = CreateAndRetrieveContact(mockService, _arthur);
        
        // Assert
        mockedEntity.Should().BeEquivalentTo(liveEntity);
    }

    private Entity CreateAndRetrieveContact(IOrganizationService service, Entity entityToCreate, 
        bool deleteRecord = false)
    {
        var id = service.Create(entityToCreate);
        var retrievedRecord = 
            service.Retrieve(entityToCreate.LogicalName, id, new ColumnSet("firstname", "lastname"));
        
        if (deleteRecord)
        {
            service.Delete(entityToCreate.LogicalName, id);
        }

        var returnEntity = new Entity(entityToCreate.LogicalName)
        {
            Attributes =
            {
                ["firstname"] = retrievedRecord.GetAttributeValue<string>("firstname"),
                ["lastname"] = retrievedRecord.GetAttributeValue<string>("lastname")
            }
        };

        return returnEntity;
    }
    
    private readonly Entity _arthur = new Entity("contact")
    {
        Attributes = new AttributeCollection
        {
            new("firstname", "Arthur"),
            new("lastname", "Nicholson-Gumula"),
        }
    };
}