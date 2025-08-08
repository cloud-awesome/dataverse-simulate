# dataverse-simulate

> Mock framework to simulate Dataverse (Power Platform/Dynamics 365 CE) environments to enable unit and integration testing.

View [full documentation](https://docs.cloudawesome.uk/dataverse-simulate), source code on [GitHub](https://github.com/Cloud-Awesome/dataverse-simulate), and add to your project from [NuGet](https://www.nuget.org/packages/CloudAwesome.Xrm.Simulate).

## Summary

`dataverse-simulate` consumes [NSubstitute](https://nsubstitute.github.io/) to mock all the services provided by the Dataverse API and plugin services

- **IOrganizationService** - Methods for CRUD operations and executing OrganizationRequests against the Dataverse platform (e.g. Upsert, Add to Queue, Assign)
- **IServiceProvider** - Access to service container with the following platform services tailored for the current plugin context
  - **IPluginExecutionContext** - Information about the runtime environment for the plugin
  - **ITracingService** - Enables plugins to log runtime information for debugging and analysis
  - **IOrganizationServiceFactory** - Generates instances of IOrganizationService tailored to specific user or system contexts
  - **ILogger** - Logging interface to be used for adding structured telemetry to Azure Application Insights
  - **IServiceEndpointNotificationService** - Sends notifications to an external service, such as Azure Service Bus about system events, such as entity changes
  - **IOrganizationService** - Not typically recommended - Instead of using the service factory, retrieves the orgService running under the SYSTEM user context  *(NOT YET IMPLEMENTED)*

To support functionality with the above, there are also four in memory data stores which simulate the functionality of Dataverse and related technologies

- **Entity Data** - The Dataverse database, such as Accounts, Contacts, and custom entities
- **Logging Data** - A mock of plugin traces, output from the ITracingService
- **Telemetry Data** - A mock of application telemetry, output from the plugin ILogger service
- **Simulator Audit** - A framework-specific audit of every message which has been executed, e.g. Create, RetrieveMultiple, WhoAmI

## Quick start

`dataverse-simulate` exposes two extension methods to the `IOrganizationService` and `IServiceProvider` from which all functionality can be accessed.

- Use `.Simulate()` to mock the `IOrganizationService` or `IServiceProvider`
- Use `.Simulated()` to access the mocked entity data and related, in memory data such as logs and telemetry (varies depending on mocked service)


### Mocking the `IOrganizationService`

```csharp
// Reference a null IOrganisationService which can be consumed by all unit tests.
// This enables the fluent simulation API, it doesn't hold any functionality itself.
// It could be included in a global class if preferred as 
//    all the functionality is created from the `.Simulate()' method.
private IOrganizationService _organizationService = null!;

[Test]
public void Create_Contact_Saves_Record_To_Data_Store()
{
    // Create a mock of the org service
    // Each call to `.Simulate` creates a fresh new mock
    _organizationService = _organizationService.Simulate();
    
    // Thereafter you can use any SDK methods on the org service as usual,
    //    or inject it into your code under test (sut)
    var contactId = _organizationService.Create(Arthur.Contact());
    
    // And use any testing frameworks you like to run assertions
    contactId.Should().NotBeEmpty();

    // Instead of executing a query against the org service, 
    //    you can retrieve, query and interact the in memory data
    //    directly using the `.Simulated` extension
    var contacts = _organizationService.Simulated().Data().Get("contact");
    
    // And run your assertions on that data
    contacts.Count.Should().Be(1);
    contacts.SingleOrDefault()?.Id.Should().Be(contactId);
}
```

Options can be injected to the mocked service to facilitate unit tests

Use the [SimulatorOptions](https://docs.cloudawesome.uk/dataverse-simulate/simulator-options/) class to inject any configuration required by your tests,
such as system time, current authenticated user, or other business logic to trigger on SDK message execution.  

```csharp
var userId = Guid.NewGuid();
var systemTime = new DateTime(2020, 8, 16);

// Create a new `SimulatorOptions` instance. This can include
//		Mocking SystemTime, authenticated user, required test data.
// View documentation for more options!
var options = new SimulatorOptions
{
    // Inject a mocked system time to allow for date-based tests and assertions
    // (You can use the provided MockSystemTime, or create your own implementation 
    //		of `IClockSimulator` if you need any additional functionality)
    ClockSimulator = new MockSystemTime(systemTime),

    // Set the current authenticated user. 
    // This can use early or late-bound entities (i.e. `Entity` or `SystemUser`) 
    AuthenticatedUser = new Entity("systemuser", userId)
    {
        Attributes =
        {
            ["fullname"] = "Lynda Archer"
        }
    }
};

// Pass the options when you simulate the org service
_organizationService = _organizationService.Simulate(options);

// Thereafter you can use org service methods as usual,
//    or inject it into your code under test (sut)
var contactId = _organizationService.Create(Arthur.Contact());

// As before, you can quickly retrieve data from the in-memory store instead of
//		executing retrieve requests, using the `.Simulated().Data()` methods.
var contacts = _organizationService.Simulated().Data().Get("contact");

// And assert on configuration as expected
contacts.Count.Should().Be(1);
var createdContact = (Contact) contacts.SingleOrDefault()!;

// Code is executed under the authenticated user
createdContact.CreatedBy.Id.Should().Be(userId);
createdContact.ModifiedBy.Id.Should().Be(userId);

// Code is executed using the injected system time
createdContact.CreatedOn.Should().Be(systemTime);
createdContact.ModifiedOn.Should().Be(systemTime);
```

### Mocking the `IServiceProvider` to test plugins

```csharp
// Reference a null IServiceProvider which can be consumed by all unit tests.
// This enables the fluent simulation API, it doesn't hold any functionality itself.
// It could be included in a global class if preferred as 
//    all the functionality is created from the `.Simulate()' method.
private IServiceProvider _serviceProvider = null!;

[Test]
public void Follow_Up_Plugin_Creates_Activity_Record() 
{
    // Simulate the service provider
    _serviceProvider = _serviceProvider.Simulate();

    // Create a reference to the plugin you want to test
    // If your plugin has a constructor, it can be used as normal
    var sut = new FollowUpPlugin();

    // The `IPlugin` interface enforces a `Execute(IServiceProvider serviceProvider)` method
    // Feed the mocked service provider and execute
    // All the other services you initialise in the plugin code will be mocked too 
    sut.Execute(_serviceProvider);

    // The `.Simulated()` extension exposes four mocked data services
    //		(Data, Logs, Telemetry, and Audits) to query outputs
    
    // Get all tasks from the database
    var tasks = _serviceProvider.Simulated().Data().Get("task");
    
    // Get all plugin traces created during this test
    var traces = _serviceProvider.Simulated().Logs().Get();

    // Then run your test assertions as usual
    tasks.Count.Should().Be(1);
    tasks.SingleOrDefault()!.Attributes["subject"].Should().Be("Follow up on your call");

    traces.Count.Should().Be(1);
}

```

As with the org service, [SimulatorOptions](https://docs.cloudawesome.uk/dataverse-simulate/simulator-options/) can be injected to the mocked service to facilitate unit tests

```csharp
public void Follow_Up_Plugin_Creates_Activity_Record_Respecting_Simulator_Options()
{
    var userId = Guid.NewGuid();
    var targetAccountId = Guid.NewGuid();
    
    // Create a new `SimulatorOptions` instance. This can include
    //		Mocking SystemTime, authenticated user, required test data
    // View documentation for more options!
    var options = new SimulatorOptions
    {
        // Set the current authenticated user. 
        AuthenticatedUser = new Entity("systemuser", userId)
        {
            Attributes =
            {
                ["fullname"] = "Bruce Purves"
            }
        },
    
        // Set the plugin execution context, including the target entity of the triggered plugin
        // All other members of the `IPluginExecutionContext` such as registered message, 
        //    entity images, and stage can be set in here
        PluginExecutionContextMock = new PluginExecutionContextMock
        {
            InputParameters = [ new("Target", new Entity("account", targetAccountId)) ],
            PrimaryEntityName = "account"
        }
    };

    // Pass the options when you simulate the service provider
    _serviceProvider = _serviceProvider.Simulate(options);

    // Create a reference to the plugin you want to test
    var sut = new FollowUpPlugin();
    sut.Execute(_serviceProvider);

    // The `.Simulated` extension exposes 4 mocked data services to query outputs
    var tasks = _serviceProvider.Simulated().Data().Get("task");
    var traces = _serviceProvider.Simulated().Logs().Get();

    // Then run your test assertions as usual
    tasks.Count.Should().Be(1);
    var followUpTask = tasks.SingleOrDefault()!;

    // Plugin has processed the injected target Account
    followUpTask.Attributes["regardingid"].Should().BeEquivalentTo(new EntityReference
    {
        Id = targetAccountId,
        LogicalName = "account"
    });

    // Plugin has executed under the injected user
    followUpTask.Attributes["createdby"].Should().BeEquivalentTo(new EntityReference
    {
        Id = userId,
        LogicalName = "systemuser"
    });
    
    // And plugin traces should be logged as before
    traces.Count.Should().Be(1);
    traces.SingleOrDefault()!.Should().Be("FollowUpPlugin: Successfully created the task activity.");
}
```
