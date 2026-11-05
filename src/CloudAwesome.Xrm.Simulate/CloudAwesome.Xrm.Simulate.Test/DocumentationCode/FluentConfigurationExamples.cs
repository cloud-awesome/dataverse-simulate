using System;
using System.IO;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.DocumentationCode;

/// <summary>Executable examples for the concern-based fluent configuration API.</summary>
[TestFixture]
public class FluentConfigurationExamples
{
	[Test]
	public void Quick_start_service_with_subsequent_configuration()
	{
		IOrganizationService service = null!;
		var account = new Entity("account", Guid.NewGuid()) { ["name"] = "Example" };
		var now = new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc);

		service = service.Simulate(configure: simulation => 
			simulation
				.WithData(account)
				.WithClock(now));

		service.Simulated().Configure(simulation => simulation
			.WithClock(now.AddHours(1))
			.WithData(data => data
				.Upsert(new Entity("account", account.Id) { ["name"] = "Updated fixture" })));

		Assert.That(service.Retrieve("account", account.Id, new ColumnSet("name"))["name"],
			Is.EqualTo("Updated fixture"));
	}

	[Test]
	public void Metadata_roles_and_assignments_are_discoverable_in_their_groups()
	{
		IOrganizationService service = null!;
		var user = new Entity("systemuser", Guid.NewGuid());
		var account = new Entity("account", Guid.NewGuid()) { ["name"] = "Example" };
		var businessUnit = new Entity("businessunit", Guid.NewGuid()) { ["name"] = "Test BU" };
		var metadataPath =
			Path.Combine(TestContext.CurrentContext.TestDirectory, "TestMetadata", "entity-metadata.json");

		service = service.Simulate(configure: simulation => simulation
			.WithUser(user)
			.WithData(account)
            .WithBusinessUnit(businessUnit)
			.WithMetadata(metadata => metadata.Load(metadataPath))
			.WithSecurity(security => security
                .WithBusinessUnit(businessUnit.Id, "Test BU")
                .WithUser(user.Id, businessUnit.Id)
				.WithMissingEntityPolicy(false)
				.WithRole("Account reader", role => role.CanRead("account", PrivilegeDepthEnum.Organization))
				.AssignRoleToUser("Account reader", user.Id)
			)
		);

		Assert.That(service.Retrieve("account", account.Id, new ColumnSet("name"))["name"], Is.EqualTo("Example"));
	}

	[Test]
	public void Both_mocks_use_WithServices_for_request_behavior()
	{
		IOrganizationService service = null!;
		IServiceProvider provider = null!;

		service = service.Simulate(configure: simulation => simulation
			.WithServices(services => services
				.WithRequestHandler<ScoreRequest>((_, _) => new OrganizationResponse 
				{ 
					Results = new ParameterCollection { ["Score"] = 100 } 
				})
			)
		);

		provider = provider.Simulate(configure: simulation => simulation
			.WithServices(services => services
				.WithRequestHandler<ScoreRequest>((_, _) => new OrganizationResponse 
				{ 
					Results = new ParameterCollection { ["Score"] = 100 } 
				})
			)
		);

		var factory = (IOrganizationServiceFactory)provider.GetService(typeof(IOrganizationServiceFactory))!;
		
		Assert.That(service.Execute(new ScoreRequest()).Results["Score"], Is.EqualTo(100));
		Assert.That(factory.CreateOrganizationService(Guid.Empty).Execute(new ScoreRequest()).Results["Score"],
			Is.EqualTo(100));
	}

	[Test]
	public void Existing_options_remain_live_with_a_fluent_callback()
	{
		IOrganizationService service = null!;
		
		var baseline = new SimulatorOptions { ClockSimulator = new MockSystemTime(new DateTime(2026, 1, 1)) };
		var user = new Entity("systemuser", Guid.NewGuid());

		service = service.Simulate(options: baseline, configure: simulation => simulation.WithUser(user));
		
		Assert.That(baseline.AuthenticatedUser, Is.SameAs(user));
		
		baseline.FakeServiceFailureSettings = new FakeServiceFailureSettings
		{
			RequestFailureSettings = [new RequestFailureSetting("Create") { Exception = new ApplicationException("Expected") }]
		};
		
		Assert.Throws<ApplicationException>(() => service.Create(new Entity("account")));
	}

	[Test]
	public void Plugin_uses_existing_context_images_handlers_and_shared_state()
	{
		IServiceProvider provider = null!;
		var account = new Entity("account", Guid.NewGuid()) { ["name"] = "Example" };
		var now = new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc);

		provider = provider.Simulate(configure: simulation => simulation
			.WithData(account)
			.WithClock(now)
			.WithServices(services => services
				.WithRequestHandler<ScoreRequest>((_, _) => new OrganizationResponse
				{
					Results = new ParameterCollection { ["Score"] = 100 }
				}))
			.WithPluginContext(plugin => plugin
				.WithMessage("Update")
				.WithStage(20)
				.WithPrimaryEntity("account", account.Id)
				.WithInputParameter("Target", account)
				.WithPreImage("Before", account)));

		new ExamplePlugin().Execute(provider);

		Assert.That(provider.Simulated().Data().Get("task"), Has.Count.EqualTo(1));
		Assert.That(provider.Simulated().Logs().Get(), Has.Count.EqualTo(1));
		
		var context = (IPluginExecutionContext)provider.GetService(typeof(IPluginExecutionContext))!;
		
		Assert.That(context.OutputParameters["Score"], Is.EqualTo(100));
	}

	private sealed class ScoreRequest : OrganizationRequest { }
	
	private sealed class ExamplePlugin : IPlugin
	{
		public void Execute(IServiceProvider provider)
		{
			var context = (IPluginExecutionContext)provider.GetService(typeof(IPluginExecutionContext))!;
			var factory = (IOrganizationServiceFactory)provider.GetService(typeof(IOrganizationServiceFactory))!;
			var trace = (ITracingService)provider.GetService(typeof(ITracingService))!;
			var service = factory.CreateOrganizationService(context.UserId);
			
			var score = service.Execute(new ScoreRequest()).Results["Score"];
			var before = context.PreEntityImages["Before"];
			
			service.Create(new Entity("task")
			{
				["subject"] = "Review account", 
				["regardingobjectid"] = before.ToEntityReference()
			});
			context.OutputParameters["Score"] = score;
			
			trace.Trace("Created a review task.");
		}
	}
}
