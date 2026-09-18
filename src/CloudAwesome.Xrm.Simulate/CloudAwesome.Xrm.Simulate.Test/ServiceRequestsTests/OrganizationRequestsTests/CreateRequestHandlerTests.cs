using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using CloudAwesome.Xrm.Simulate.Test.TestEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.ServiceRequestsTests.OrganizationRequestsTests;

[TestFixture]
public class CreateRequestHandlerTests
{
    private IOrganizationService _organizationService = null!;

    [Test]
    public void Create_Throws_Exception_When_User_Doesnt_Have_Create_Privilege()
    {
        var options = new SimulatorOptions
        {
            SimulatedSecurityModel = new SimulatedSecurityModel
            {
                EntityPermissions =
                [
                    new EntityPermission
                    {
                        LogicalName = "contact",
                        Create = PrivilegeDepthEnum.None
                    }
                ]
            }
        };

        _organizationService = _organizationService.Simulate(options);

        var sut = () => _organizationService.Create(Arthur.Contact());
        var exception = sut.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which;

        exception.Detail.ErrorCode.Should().Be(-2147187962);
        exception.Detail.Message.Should().Contain("does not have Create privilege for entity 'contact'");
    }
}
