using System;
using CloudAwesome.Xrm.Simulate.ServiceProviders;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using CloudAwesome.Xrm.Simulate.Test.TestEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.ServiceProvidersTests;

[TestFixture]
public class PluginExecutionContextSimulatorTests
{
    private readonly IServiceProvider _serviceProvider = null!;

    [Test]
    public void User_From_Context_Is_Set_From_Simulator_Options()
    {
        var options = new SimulatorOptions
        {
            AuthenticatedUser = Arthur.Contact()
        };
        
        var service = _serviceProvider.Simulate(options);
        var executionContext = (IPluginExecutionContext)service.GetService(typeof(IPluginExecutionContext))!;

        executionContext.UserId.Should().Be(Arthur.Contact().Id);
    }

    [Test]
    public void Mocked_Plugin_Depth_Is_Returned_From_Simulator()
    {
        var options = new SimulatorOptions
        {
            PluginExecutionContextMock = new PluginExecutionContextMock
            {
                Depth = 2
            }
        };

        var service = _serviceProvider.Simulate(options);
        var executionContext = (IPluginExecutionContext)service.GetService(typeof(IPluginExecutionContext))!;

        executionContext.Depth.Should().Be(2);
    }
    
    [Test]
    public void Mocked_Plugin_Stage_Is_Returned_From_Simulator()
    {
        var options = new SimulatorOptions
        {
            PluginExecutionContextMock = new PluginExecutionContextMock
            {
                Stage = (int) SdkMessageProcessingStep_Stage.PostOperation
            }
        };

        var service = _serviceProvider.Simulate(options);
        var executionContext = (IPluginExecutionContext)service.GetService(typeof(IPluginExecutionContext))!;

        executionContext.Stage.Should().Be(40);
    }
    
    [Test]
    public void Mocked_Entity_Plugin_Target_Is_Returned_From_Simulator()
    {
        var options = new SimulatorOptions
        {
            PluginExecutionContextMock = new PluginExecutionContextMock
            {
                InputParameters = new ParameterCollection
                {
                    new ("Target", new Entity(Arthur.Contact().LogicalName, Arthur.Contact().Id))
                }
            }
        };

        var service = _serviceProvider.Simulate(options);
        var executionContext = (IPluginExecutionContext)service.GetService(typeof(IPluginExecutionContext))!;

        executionContext.InputParameters.Should().NotBeNull();
        executionContext.InputParameters["Target"].Should().BeOfType<Entity>();

        var target = (Entity) executionContext.InputParameters["Target"];
        target.LogicalName.Should().Be(Contact.EntityLogicalName);
        target.Id.Should().Be(Arthur.Contact().Id);
    }
    
    [Test]
    public void Mocked_EntityReference_Plugin_Target_Is_Returned_From_Simulator()
    {
        var options = new SimulatorOptions
        {
            PluginExecutionContextMock = new PluginExecutionContextMock
            {
                InputParameters = new ParameterCollection
                {
                    new ("Target", new EntityReference(Arthur.Contact().LogicalName, Arthur.Contact().Id))
                }
            }
        };

        var service = _serviceProvider.Simulate(options);
        var executionContext = (IPluginExecutionContext)service.GetService(typeof(IPluginExecutionContext))!;

        executionContext.InputParameters.Should().NotBeNull();
        executionContext.InputParameters["Target"].Should().BeOfType<EntityReference>();

        var target = (EntityReference) executionContext.InputParameters["Target"];
        target.LogicalName.Should().Be(Contact.EntityLogicalName);
        target.Id.Should().Be(Arthur.Contact().Id);
    }

    [Test]
    public void Reinitialising_Execution_Context_Works_With_Valid_Mock()
    {
        var options = new SimulatorOptions
        {
            PluginExecutionContextMock = new PluginExecutionContextMock
            {
                InputParameters = new ParameterCollection
                {
                    new ("Target", new EntityReference(Arthur.Contact().LogicalName, Arthur.Contact().Id))
                }
            }
        };

        var service = _serviceProvider.Simulate(options);

        var executionContextMock = new PluginExecutionContextMock
        {
            InputParameters = new ParameterCollection
            {
                new ("Target", new EntityReference(Arthur.Account().LogicalName, Arthur.Account().Id))
            }
        };
        
        _serviceProvider.Simulated().Data().Reinitialise(executionContextMock);
        
        var executionContext = (IPluginExecutionContext)service.GetService(typeof(IPluginExecutionContext))!;

        executionContext.InputParameters.Should().NotBeNull();
        executionContext.InputParameters["Target"].Should().BeOfType<EntityReference>();

        var target = (EntityReference) executionContext.InputParameters["Target"];
        target.LogicalName.Should().Be("account");
        target.Id.Should().Be(Arthur.Account().Id);
    }
    
    [Test]
    public void Reinitialising_Execution_Context_Works_With_Valid_Mock_If_EntityT_Type_Is_The_Same()
    {
        var options = new SimulatorOptions
        {
            PluginExecutionContextMock = new PluginExecutionContextMock
            {
                InputParameters = new ParameterCollection
                {
                    new ("Target", Arthur.Contact())
                }
            }
        };

        var service = _serviceProvider.Simulate(options);

        var executionContextMock = new PluginExecutionContextMock
        {
            InputParameters = new ParameterCollection
            {
                new ("Target", Siobhan.Contact())
            }
        };
        
        _serviceProvider.Simulated().Data().Reinitialise(executionContextMock);
        
        var executionContext = (IPluginExecutionContext)service.GetService(typeof(IPluginExecutionContext))!;

        executionContext.InputParameters.Should().NotBeNull();
        executionContext.InputParameters["Target"].Should().BeOfType<Contact>();

        var target = (Entity) executionContext.InputParameters["Target"];
        target.LogicalName.Should().Be(Contact.EntityLogicalName);
        target.Id.Should().Be(Siobhan.Contact().Id);
    }

    [Test]
    public void SimulatorOptions_Are_Correctly_Reflected_In_ExecutionContext()
    {
        var options = new SimulatorOptions
        {
            AuthenticatedUser = new SystemUser
            {
                Id = Guid.NewGuid(),
                FullName = "Daphne Moon"
            },
            BusinessUnit = new BusinessUnit
            {
                Id = Guid.NewGuid(),
                Name = "Root BU"
            },
            Organization = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Cloud Awesome"
            },
            ClockSimulator = new MockSystemTime(new DateTime(2009, 8, 15))
        };

        var service = _serviceProvider.Simulate(options);
        var executionContext = (IPluginExecutionContext)service.GetService(typeof(IPluginExecutionContext))!;

        executionContext.UserId.Should().Be(options.AuthenticatedUser.Id);
        executionContext.InitiatingUserId.Should().Be(options.AuthenticatedUser.Id);
        executionContext.BusinessUnitId.Should().Be(options.BusinessUnit.Id);
        executionContext.OrganizationId.Should().Be(options.Organization.Id);
    }
}