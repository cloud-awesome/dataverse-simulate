using System;
using System.Linq;
using CloudAwesome.Xrm.Simulate.ServiceProviders;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.ServiceProvidersTests;

[TestFixture]
public class ServiceEndpointNotificationSimulatorTests
{
	private readonly IServiceProvider _serviceProvider = null!;

	[Test]
	public void Execute_Method_Must_Be_Passed_A_Valid_ServiceEndpoint()
	{
		var service = _serviceProvider.Simulate();

		var context = (IPluginExecutionContext)service.GetService(typeof(IPluginExecutionContext))!;
		var serviceBus =
			(IServiceEndpointNotificationService)service.GetService(typeof(IServiceEndpointNotificationService))!;

		var invalidServiceEndpoint = new EntityReference("contact");

		var sut = () => serviceBus.Execute(invalidServiceEndpoint, context);
		sut.Should().Throw<InvalidPluginExecutionException>();
	}
	
	[Test]
	public void Valid_Execute_Method_Should_Process_Expected_Results()
	{
		var service = _serviceProvider.Simulate();

		var context = (IPluginExecutionContext)service.GetService(typeof(IPluginExecutionContext))!;
		var serviceBus =
			(IServiceEndpointNotificationService)service.GetService(typeof(IServiceEndpointNotificationService))!;

		var serviceEndpoint = new EntityReference("serviceendpoint")
		{
			Id = Guid.NewGuid()
		};

		var response = serviceBus.Execute(serviceEndpoint, context);

		response.Should().NotBeNull();
		response.Should().BeOfType<string>();
	}
	
	[Test]
	public void Valid_Execute_Method_Should_Add_Context_To_Simulated_ServiceBus()
	{
		var options = new SimulatorOptions
		{
			PluginExecutionContextMock = new PluginExecutionContextMock
			{
				MessageName = "create",
				PrimaryEntityName = "contact",
				Depth = 5
			},
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

		var context = (IPluginExecutionContext)service.GetService(typeof(IPluginExecutionContext))!;
		var serviceBus =
			(IServiceEndpointNotificationService)service.GetService(typeof(IServiceEndpointNotificationService))!;

		var serviceEndpoint = new EntityReference("serviceendpoint") { Id = Guid.NewGuid() };

		var response = serviceBus.Execute(serviceEndpoint, context);

		var messages = service.Simulated().ServiceBus().Get();
		messages.Count.Should().Be(1);
	}
}