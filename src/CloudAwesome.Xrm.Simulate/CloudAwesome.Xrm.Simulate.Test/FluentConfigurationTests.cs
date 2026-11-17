using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Xml.Linq;
using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.Queues;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using CloudAwesome.Xrm.Simulate.ServiceProviders;
using CloudAwesome.Xrm.Simulate.ServiceRequests;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.PluginTelemetry;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test;

[TestFixture]
public class FluentConfigurationTests
{
    private static IOrganizationService OrgService => null!;
    private static IServiceProvider Provider => null!;
    
    private static readonly DateTime Now = new(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc);
    private static Entity Row(string name = "account") => new(name, Guid.NewGuid());
    
    private static string MetadataPath => Path.Combine(TestContext.CurrentContext.TestDirectory, "TestMetadata", "entity-metadata.json");
    
    private static IOrganizationService Service(IServiceProvider provider, Guid? caller = null) =>
        ((IOrganizationServiceFactory)provider.GetService(typeof(IOrganizationServiceFactory))!).CreateOrganizationService(caller);
    private static OrganizationResponse Response(object value) => new() { Results = new ParameterCollection { ["value"] = value } };

    [Test]
    public void Old_and_new_call_shapes_keep_the_SDK_return_types_and_old_CLR_signatures()
    {
        _ = OrgService.Simulate(); 
        _ = OrgService.Simulate(null); 
        _ = OrgService.Simulate(null, null);
        _ = OrgService.Simulate(options: new SimulatorOptions()); 
        _ = OrgService.Simulate(dataService: new MockedEntityDataService());
        _ = OrgService.Simulate(configure: s => s.WithClock(Now));
        _ = OrgService.Simulate(new SimulatorOptions(), new MockedEntityDataService(), s => s.WithClock(Now));
        
        _ = Provider.Simulate(); _ = Provider.Simulate(null); 
        _ = Provider.Simulate(new SimulatorOptions(), s => s.WithClock(Now));
        
        Assert.That(typeof(OrganisationServiceSimulator).GetMethod("Simulate", [typeof(IOrganizationService), typeof(ISimulatorOptions), typeof(MockedEntityDataService)]), Is.Not.Null);
        Assert.That(typeof(ServiceProviderSimulator).GetMethod("Simulate", [typeof(IServiceProvider), typeof(ISimulatorOptions)]), Is.Not.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Fluent_and_legacy_initialization_use_the_same_defaults(bool fluent)
    {
        var service = fluent ? OrgService.Simulate(configure: _ => { }) : OrgService.Simulate();
        
        var data = service.Simulated().Data();
        var user = data.Get(data.AuthenticatedUser);
        
        Assert.That(user.GetAttributeValue<string>("fullname"), Is.EqualTo("Simulated User"));
        Assert.That(user.GetAttributeValue<EntityReference>("businessunitid").Id, Is.EqualTo(data.BusinessUnit.Id));
        Assert.That(user.GetAttributeValue<EntityReference>("OrganizationId").Id, Is.EqualTo(data.Organization.Id));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Existing_options_and_fixture_reference_semantics_are_retained_on_both_mocks(bool providerMode)
    {
        var row = Row();
        var user = Row("systemuser");
        
        var options = new SimulatorOptions();
        var service = providerMode
            ? Service(Provider.Simulate(options, s => s.WithUser(user).WithData(row)), Guid.Empty)
            : OrgService.Simulate(options: options, configure: s => s.WithUser(user).WithData(row));
        
        Assert.That(options.AuthenticatedUser, Is.SameAs(user));
        Assert.That(service.Simulated().Data().Get("account", row.Id), Is.SameAs(row));
        
        row["name"] = "live fixture";
        Assert.That(service.Retrieve("account", row.Id, new ColumnSet(true))["name"], Is.EqualTo("live fixture"));
        
        options.FakeServiceFailureSettings = new FakeServiceFailureSettings
        {
            RequestFailureSettings = [new RequestFailureSetting("Create") { Exception = new ApplicationException() }]
        };
        
        Assert.Throws<ApplicationException>(() => service.Create(new Entity("account")));
    }

    [Test]
    public void Live_configuration_is_sequential_and_does_not_rollback_prior_calls()
    {
        var service = OrgService.Simulate(); 
        var row = Row();
        
        Assert.Throws<FileNotFoundException>(() => service.Simulated().Configure(s =>
            s.WithData(row).WithClock(Now).WithMetadata("does-not-exist.json")));
        Assert.That(service.Simulated().Data().Get("account", row.Id), Is.SameAs(row));
        Assert.That(service.Simulated().Data().SystemTime, Is.EqualTo(Now));
        Assert.That(service.Simulated().Audit().Get(), Is.Empty);
    }

    [Test]
    public void Fluent_data_is_additive_and_delegates_Upsert_without_replacing_unrelated_rows()
    {
        var row = Row(); var second = Row(); var third = Row();
        var service = OrgService.Simulate(configure: s => s.WithData(row).WithData(d => d.Upsert(new Entity("account", row.Id) { ["name"] = "initial" })));
        
        service.Simulated().WithData(new Dictionary<string, List<Entity>> { ["account"] = [second] });
        service.Simulated().WithData(d => d.WithEntities(third).Upsert(new Entity("account", row.Id) { ["name"] = "live" }));
        
        Assert.That(service.Simulated().Data().Get("account"), Has.Count.EqualTo(3));
        Assert.That(service.Simulated().Data().Get("account", row.Id), Is.SameAs(row));
        Assert.That(row["name"], Is.EqualTo("live")); Assert.That(service.Simulated().Audit().Get(), Is.Empty);
        
        service.Simulated().Data().Add(new Dictionary<string, List<Entity>> { ["contact"] = [Row("contact")] });
        service.Simulated().WithClock(Now);
        
        Assert.That(service.Simulated().Data().Get("account"), Is.Empty);
    }

    [Test]
    public void Metadata_uses_the_existing_loader_and_is_not_copied_by_unrelated_configuration()
    {
        var model = SimulatedMetadata.Load(MetadataPath);
        var provider = Provider.Simulate(configure: s => s.WithMetadata(model)); var service = Service(provider);
        
        provider.Simulated().WithClock(Now);
        
        Assert.That(provider.Simulated().Metadata().Current, Is.SameAs(model));
        Assert.That(service.Simulated().Metadata().Current, Is.SameAs(model));
        
        provider.Simulated().WithMetadata(m => m.Load(MetadataPath));
        
        Assert.That(service.Simulated().Metadata().Current, Is.SameAs(provider.Simulated().Metadata().Current));
        
        var response = (RetrieveEntityResponse)service.Execute(new RetrieveEntityRequest { LogicalName = "account", EntityFilters = EntityFilters.Entity });
        
        Assert.That(response.EntityMetadata.LogicalName, Is.EqualTo("account"));
        Assert.Throws<SimulatedMetadataException>(() => service.RetrieveMultiple(new QueryExpression("unknown_table")));
    }

    [Test]
    public void Clock_is_sampled_once_at_initialization_and_only_when_set_live()
    {
        var clock = new CountingClock();
        var provider = Provider.Simulate(configure: s => s.WithClock(clock)); var service = Service(provider);
        var first = service.Create(new Entity("account"));
        provider.Simulated().WithData(Row());
        Assert.That(clock.Reads, Is.EqualTo(1));
        provider.Simulated().WithClock(Now.AddDays(1)); var second = service.Create(new Entity("account"));
        Assert.That(service.Simulated().Data().Get("account", first).GetAttributeValue<DateTime>("createdon"), Is.EqualTo(Now));
        Assert.That(service.Simulated().Data().Get("account", second).GetAttributeValue<DateTime>("createdon"), Is.EqualTo(Now.AddDays(1)));
        provider.Simulated().WithFiscalYearSettings(new FiscalYearSettings { FiscalYearStartMonth = 4 });
        Assert.That(service.Simulated().Data().FiscalYearSettings!.FiscalYearStartMonth, Is.EqualTo(4));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Shared_WithServices_wraps_existing_processors_handlers_and_failures(bool providerMode)
    {
        var key = new ProcessorType("account", ProcessorMessage.Create); var processor = new MarkProcessor();
        var service = providerMode
            ? Service(Provider.Simulate(configure: s => s.WithServices(x => x.WithProcessor(key, processor).WithRequestHandler<EchoRequest>((_, _) => Response(42)))))
            : OrgService.Simulate(configure: s => s.WithServices(x => x.WithProcessor(key, processor).WithRequestHandler<EchoRequest>((_, _) => Response(42))));
        var id = service.Create(new Entity("account"));
        Assert.That(service.Simulated().Data().Get("account", id)["marked"], Is.EqualTo(true));
        Assert.That(service.Execute(new EchoRequest()).Results["value"], Is.EqualTo(42));
        service.Simulated().WithServices(x => x.WithFailure(new RequestFailureSetting("Retrieve") { FailingRecords = [id], Exception = new ApplicationException() }));
        Assert.Throws<ApplicationException>(() => service.Retrieve("account", id, new ColumnSet(true)));
        Assert.Throws<InvalidOperationException>(() => service.Simulated().WithRequestHandler<EchoRequest>((_, _) => Response(0)));
        Assert.Throws<InvalidOperationException>(() => service.Simulated().WithRequestHandler<CreateRequest>((_, _) => new CreateResponse()));
    }

    [Test]
    public void Provider_handlers_are_shared_and_run_with_each_factory_services_caller()
    {
        var user = Row("systemuser"); var other = Row("systemuser");
        var provider = Provider.Simulate(configure: s => s.WithUser(user).WithData(other));
        var first = Service(provider, Guid.Empty); var second = Service(provider, other.Id);
        provider.Simulated().WithRequestHandler<EchoRequest>((_, context) => Response(context.Data.AuthenticatedUser.Id));
        Assert.That(first.Execute(new EchoRequest()).Results["value"], Is.EqualTo(user.Id));
        Assert.That(second.Execute(new EchoRequest()).Results["value"], Is.EqualTo(other.Id));
        Assert.That(Service(provider, other.Id).Execute(new EchoRequest()).Results["value"], Is.EqualTo(other.Id));
        first.Simulated().CustomOrgRequests().Add<OtherRequest>((_, _) => Response(9));
        Assert.That(second.Execute(new OtherRequest()).Results["value"], Is.EqualTo(9));
        Assert.Throws<NotSupportedException>(() => Service(Provider.Simulate()).Execute(new EchoRequest()));
    }

    [Test]
    public void Initial_and_live_handlers_keep_existing_batch_and_transaction_dispatch()
    {
        var provider = Provider.Simulate(configure: s => s
            .WithServices(x => x
                .WithRequestHandler<EchoRequest>((_, _) => Response(10))));
        
        var service = Service(provider);
        var transaction = (ExecuteTransactionResponse)service.Execute(new ExecuteTransactionRequest { Requests = [new EchoRequest()], ReturnResponses = true });
        
        Assert.That(transaction.Responses[0].Results["value"], Is.EqualTo(10));
        
        var multiple = (ExecuteMultipleResponse)service.Execute(
            new ExecuteMultipleRequest
            {
                Requests = [new EchoRequest()], Settings = new ExecuteMultipleSettings { ReturnResponses = true }
            });
        
        Assert.That(multiple.Responses[0].Response.Results["value"], Is.EqualTo(10));
    }

    [Test]
    public void Changing_connection_defaults_does_not_rebind_existing_factory_callers()
    {
        var user = Row("systemuser"); 
        var other = Row("systemuser");
        
        var provider = Provider.Simulate(configure: s => s.WithUser(user).WithData(other));
        var bound = Service(provider, Guid.Empty); 
        var system = Service(provider);
        
        bound.Simulated().WithUser(other);
        
        var id = bound.Create(new Entity("account"));
        
        Assert.That(bound.Simulated().Data().Get("account", id).GetAttributeValue<EntityReference>("createdby").Id, Is.EqualTo(user.Id));
        Assert.That(((WhoAmIResponse)bound.Execute(new WhoAmIRequest())).UserId, Is.EqualTo(other.Id));
        Assert.That(Service(provider, Guid.Empty).Simulated().Data().AuthenticatedUser.Id, Is.EqualTo(other.Id));
        Assert.That(system.Simulated().Data().IsSystemUser, Is.True);
        Assert.That(provider.Simulated().Data().Get("systemuser").Count(e => e.Id == other.Id), Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void The_existing_security_facade_handles_initial_and_live_setup(bool initial)
    {
        var user = Row("systemuser"); 
        var bu = Row("businessunit"); 
        bu["name"] = "Root"; 
        var account = Row();
        
        Action<SimulatedSecurityModelService> security = 
            s => s
            .WithBusinessUnit(bu.Id, "Root")
            .WithUser(user.Id, bu.Id)
            .WithMissingEntityPolicy(false)
            .WithRole("Reader", r => r.
                CanRead("account", PrivilegeDepthEnum.Organization))
            .AssignRoleToUser("Reader", user.Id);
        
        var provider = Provider.Simulate(configure: s =>
        {
            s
                .WithUser(user)
                .WithBusinessUnit(bu)
                .WithData(account); 
            if (initial) s.WithSecurity(security);
        });
        
        var service = Service(provider, Guid.Empty);
        if (!initial) provider.Simulated().WithSecurity(security);
        
        Assert.DoesNotThrow(() => service.Retrieve("account", account.Id, new ColumnSet(true)));
        Assert.Throws<FaultException<OrganizationServiceFault>>(() => service.Create(new Entity("account")));
        
        provider.Simulated()
            .WithSecurity(s => s
                .WithRole("Creator", r => r
                    .CanCreate("account", PrivilegeDepthEnum.Organization))
                .AssignRoleToUser("Creator", user.Id));
        
        Assert.DoesNotThrow(() => service.Create(new Entity("account")));
        
        provider.Simulated()
            .WithoutSecurity();
        
        Assert.That(provider.Simulated().Data().Get("role"), Has.Count.EqualTo(2));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void Native_security_facade_retains_all_existing_XML_import_forms(int source)
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "Roles", "Initial Test Security Role.xml");
        var service = OrgService.Simulate(configure: s => s
            .WithSecurity(security => 
            { 
                if (source == 0) security.ImportRoleXml(path);
                else if (source == 1) security.ImportRoleXml(new[] { path });
                else security.ImportRoleXmlDirectory(Path.GetDirectoryName(path)!); 
            }));
        
        Assert.That(service.Simulated().SecurityModel().Model.Roles.Single().EntityPermissions, Has.Count.EqualTo(149));
    }

    [Test]
    public void Supplying_a_security_model_retains_the_existing_model_reference()
    {
        var model = SimulatedSecurityModel.Create();
        var service = OrgService.Simulate(configure: s => s.WithSecurity(model));
        
        service.Simulated()
            .WithClock(Now);
        
        Assert.That(service.Simulated().SecurityModel().Model, Is.SameAs(model));
    }

    private static Type[] ManagedServices => [typeof(IPluginExecutionContext), typeof(IOrganizationServiceFactory), typeof(ITracingService), typeof(ILogger), typeof(IServiceEndpointNotificationService)];
    [TestCaseSource(nameof(ManagedServices))]
    public void Missing_service_configuration_sets_existing_provider_flags(Type type)
    {
        var provider = Provider.Simulate(configure: s => s.WithServices(x => x.WithMissingService(type)));
        
        Assert.That(provider.GetService(type), Is.Null);
        
        provider.Simulated()
            .WithServices(x => x
                .WithMissingService(type, false));
        
        Assert.That(provider.GetService(type), Is.Not.Null);
    }

    [Test]
    public void All_context_properties_are_wired_from_the_single_existing_mock()
    {
        var user = Row("systemuser"); 
        var target = Row(); 
        var image = Row();
        var id = Guid.NewGuid(); 
        var initiating = Guid.NewGuid(); 
        var correlation = Guid.NewGuid(); 
        var operation = Guid.NewGuid(); 
        var request = Guid.NewGuid();
        
        var parent = new PluginExecutionContextMock { MessageName = "Parent" };
        var owning = new EntityReference("sdkmessageprocessingstep", Guid.NewGuid());
        
        var provider = Provider.Simulate(configure: s => s
            .WithUser(user)
            .WithClock(Now)
            .WithPluginContext(p => p
                .WithMessage("Update")
                .WithPrimaryEntity("account", id)
                .WithSecondaryEntity("contact")
                .WithStage(40)
                .WithMode(1)
                .WithIsolationMode(2)
                .WithDepth(3)
                .WithInitiatingUser(initiating)
                .WithRequestId(request)
                .WithCorrelationId(correlation)
                .WithOperationId(operation)
                .WithOperationCreatedOn(Now.AddMinutes(-1))
                .WithExecutingOffline(true)
                .WithOfflinePlayback(true)
                .WithInTransaction(true)
                .WithParentContext(parent)
                .WithOwningExtension(owning)
                .WithInputParameter("Target", target)
                .WithOutputParameter("result", 1)
                .WithSharedVariable("shared", 2)
                .WithPreImage("Before", image)
                .WithPostImage("After", image)));
        
        var context = (IPluginExecutionContext)provider.GetService(typeof(IPluginExecutionContext))!;
        
        Assert.Multiple(() =>
        {
            Assert.That(context.UserId, Is.EqualTo(user.Id)); 
            Assert.That(context.InitiatingUserId, Is.EqualTo(initiating));
            Assert.That(context.MessageName, Is.EqualTo("Update")); 
            Assert.That(context.PrimaryEntityId, Is.EqualTo(id));
            Assert.That(context.PrimaryEntityName, Is.EqualTo("account")); 
            Assert.That(context.SecondaryEntityName, Is.EqualTo("contact"));
            Assert.That(context.Stage, Is.EqualTo(40)); 
            Assert.That(context.Mode, Is.EqualTo(1)); 
            Assert.That(context.IsolationMode, Is.EqualTo(2)); 
            Assert.That(context.Depth, Is.EqualTo(3));
            Assert.That(context.RequestId, Is.EqualTo(request)); 
            Assert.That(context.CorrelationId, Is.EqualTo(correlation)); 
            Assert.That(context.OperationId, Is.EqualTo(operation));
            Assert.That(context.OperationCreatedOn, Is.EqualTo(Now.AddMinutes(-1))); 
            Assert.That(context.ParentContext, Is.SameAs(parent)); 
            Assert.That(context.OwningExtension, Is.SameAs(owning));
            Assert.That(context.IsExecutingOffline && context.IsOfflinePlayback && context.IsInTransaction, Is.True);
            Assert.That(context.InputParameters["Target"], Is.SameAs(target)); 
            Assert.That(context.PreEntityImages["Before"], Is.SameAs(image)); 
            Assert.That(context.PostEntityImages["After"], Is.SameAs(image));
            Assert.That(context.OutputParameters["result"], Is.EqualTo(1)); 
            Assert.That(context.SharedVariables["shared"], Is.EqualTo(2));
            Assert.That(context.OrganizationName, Is.EqualTo("Simulated Organization"));
            Assert.That(context.OrganizationId, Is.EqualTo(provider.Simulated().Data().Organization.Id)); 
            Assert.That(context.BusinessUnitId, Is.EqualTo(provider.Simulated().Data().BusinessUnit.Id));
        });
    }

    [Test]
    public void Context_callbacks_use_the_existing_mock_and_outputs_remain_observable()
    {
        var mock = new PluginExecutionContextMock { MessageName = "Update" };
        var options = new SimulatorOptions();
        var provider = 
            Provider.Simulate(options, s => s
                .WithPluginContext(mock));
        
        Assert.That(options.PluginExecutionContextMock, Is.SameAs(mock));
        
        var context = (IPluginExecutionContext)provider.GetService(typeof(IPluginExecutionContext))!;
        
        context.OutputParameters["answer"] = 42;
        
        Assert.That(((IPluginExecutionContext)provider.GetService(typeof(IPluginExecutionContext))!).OutputParameters["answer"], Is.EqualTo(42));
        
        mock.MessageName = "Create";
        
        Assert.That(((IPluginExecutionContext)provider.GetService(typeof(IPluginExecutionContext))!).MessageName, Is.EqualTo("Create"));
        
        ((ITracingService)provider.GetService(typeof(ITracingService))!).Trace("retained");
        
        provider.Simulated()
            .WithPluginContext(p =>
            {
                p.MessageName = "Delete"; 
                p.Stage = 40;
            });
        
        Assert.That(((IPluginExecutionContext)provider.GetService(typeof(IPluginExecutionContext))!).MessageName, Is.EqualTo("Delete"));
        
        Assert.That(provider.Simulated().Logs().Get(), Has.Count.EqualTo(1));
    }

    [Test]
    public void Queue_and_relationship_configuration_reuses_existing_seeders()
    {
        var account = Row(); var contact = Row("contact"); var queue = Guid.NewGuid(); var item = Guid.NewGuid();
        var relationship = new SimulatedRelationship { Target = account.ToEntityReference(), Relationship = new Relationship("account_contacts"), RelatedEntities = [contact.ToEntityReference()] };
        
        var service = 
            OrgService.Simulate(configure: s => s
                .WithClock(Now)
                .WithData(account, contact)
                .WithData(d => d
                    .WithRelationships([relationship])
                    .WithQueues(q => q
                        .WithQueue(queue)
                        .WithQueueItem(item, queue, account.ToEntityReference()))));
        
        Assert.That(service.Simulated().Data().GetRelationships(), Has.Count.EqualTo(1));
        Assert.That(service.Simulated().Data().Get("queueitem", item).GetAttributeValue<EntityReference>("queueid").Id, Is.EqualTo(queue));
        Assert.That(service.Simulated().Data().Get("queue", queue).GetAttributeValue<DateTime>("createdon"), Is.EqualTo(Now));
        
        service.Simulated().WithData(d => d.WithQueues(q => q.WithQueue(Guid.NewGuid())));
        
        Assert.That(service.Simulated().Data().Get("queue"), Has.Count.EqualTo(2));
    }

    [Test]
    public void Live_fixture_order_follows_existing_reference_validation()
    {
        var service = OrgService.Simulate(); 
        var account = Row(); 
        var contact = Row("contact");
        
        var relationship = new SimulatedRelationship
        {
            Target = account.ToEntityReference(), 
            Relationship = new Relationship("account_contacts"), 
            RelatedEntities = [contact.ToEntityReference()]
        };
        
        Assert.Throws<FaultException<OrganizationServiceFault>>(() => service.Simulated().WithData(d => d.WithRelationships([relationship])));
        
        service.Simulated()
            .WithData(account, contact)
            .WithData(d => d
                .WithRelationships([relationship])
            );
        
        Assert.That(service.Simulated().Data().GetRelationships(), Has.Count.EqualTo(1));
    }

    [Test]
    public void New_fluent_methods_have_generated_XML_summaries_parameters_and_returns()
    {
        var assembly = typeof(OrganizationSimulationBuilder).Assembly;
        var document = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = document.Descendants("member").ToArray();
        
        foreach (var type in assembly.GetExportedTypes().Where(t => t.Name.Contains("Builder") || t == typeof(SimulationConfigurationExtensions) || t == typeof(PluginExecutionContextExtensions)))
        
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            var prefix = "M:" + type.FullName + "." + method.Name;
            
            var entries = members.Where(m => ((string?)m.Attribute("name"))?.StartsWith(prefix, StringComparison.Ordinal) == true).ToArray();
            
            Assert.That(entries, Is.Not.Empty, prefix);
            Assert.That(entries.All(e => !string.IsNullOrWhiteSpace(e.Element("summary")?.Value) && e.Element("returns") is not null), Is.True, prefix);
            
            foreach (var parameter in method.GetParameters())
                Assert.That(entries.Any(e => e.Elements("param").Any(p => (string?)p.Attribute("name") == parameter.Name)), Is.True, prefix + " " + parameter.Name);
        }
    }

    private sealed class EchoRequest : OrganizationRequest { }
    private sealed class OtherRequest : OrganizationRequest { }
    private sealed class MarkProcessor : IEntityProcessor { public Entity Process(Entity entity) { entity["marked"] = true; return entity; } }
    private sealed class CountingClock : IClockSimulator { public int Reads; public DateTime Now { get { Reads++; return FluentConfigurationTests.Now; } } }
}
