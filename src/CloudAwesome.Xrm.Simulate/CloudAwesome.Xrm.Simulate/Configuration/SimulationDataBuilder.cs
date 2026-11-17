using CloudAwesome.Xrm.Simulate.Configuration;
using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.Queues;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using CloudAwesome.Xrm.Simulate.ServiceProviders;
using CloudAwesome.Xrm.Simulate.ServiceRequests;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate;

/// <summary>Provides additive fixture conveniences over existing data and queue services.</summary>
public sealed class SimulationDataBuilder
{
	private readonly SimulationSetup _setup;
	internal SimulationDataBuilder(SimulationSetup setup) => _setup = setup;

	/// <summary>Appends fixture rows.</summary>
	/// <param name="entities">Fixture rows to retain by reference.</param>
	/// <returns>This builder for further configuration.</returns>
	public SimulationDataBuilder WithEntities(params Entity[] entities)
	{
        return WithEntities((IEnumerable<Entity>)entities);
	}

	/// <summary>Appends rows through the existing Add method.</summary>
	/// <param name="entities">Fixture rows; ids and attributes are supplied by the test.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Existing raw fixture behavior, including duplicate handling, is preserved.</remarks>
	public SimulationDataBuilder WithEntities(IEnumerable<Entity> entities)
	{
        ArgumentNullException.ThrowIfNull(entities);
        var data = _setup.Fixtures;
        foreach (var entity in entities) { ArgumentNullException.ThrowIfNull(entity); data.Add(entity); }
        return this;
	}

	/// <summary>Appends dictionary fixture records.</summary>
	/// <param name="entities">Logical-name collections to append, rather than replacing the live store.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Unlike raw Data().Add(dictionary), this convenience appends records individually.</remarks>
	public SimulationDataBuilder WithEntities(Dictionary<string, List<Entity>> entities)
	{
        ArgumentNullException.ThrowIfNull(entities);
        foreach (var rows in entities.Values) WithEntities(rows);
        return this;
	}

	/// <summary>Merges fixture attributes through the existing Upsert helper.</summary>
	/// <param name="entity">The fixture patch or new row.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>No SDK request, permission check, processor, timestamp, or audit is executed.</remarks>
	public SimulationDataBuilder Upsert(Entity entity)
	{
        ArgumentNullException.ThrowIfNull(entity);
        _setup.Fixtures.Upsert(entity);
        return this;
	}

	/// <summary>Adds relationship fixtures using the existing setup helper.</summary>
	/// <param name="relationships">Relationship fixtures whose endpoints must exist when applied.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Initial setup follows the existing initializer order; live associations require existing endpoints.</remarks>
	public SimulationDataBuilder WithRelationships(IEnumerable<SimulatedRelationship> relationships)
	{
        ArgumentNullException.ThrowIfNull(relationships);
        foreach (var relationship in relationships)
        {
            ArgumentNullException.ThrowIfNull(relationship);
            if (_setup.Data is { } data) data.SetRelationship(relationship);
            else (_setup.Options.InitialiseRelationships ??= []).Add(relationship);
        }
        return this;
	}

	/// <summary>Adds queue fixtures through the existing seeder.</summary>
	/// <param name="model">Queue declarations using the existing typed model.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Queue metadata/reference validation and defaults are owned by the existing queue seeder.</remarks>
	public SimulationDataBuilder WithQueues(SimulatedQueueModel model)
	{
        ArgumentNullException.ThrowIfNull(model);
        if (_setup.Data is { } data)
            SimulatedQueueDataSeeder.Seed(data, new SimulatorOptions { Queues = model, Metadata = _setup.Options.Metadata });
        else
        {
            var target = _setup.Options.Queues ??= new SimulatedQueueModel();
            if (!ReferenceEquals(target, model)) { target.Queues.AddRange(model.Queues); target.QueueItems.AddRange(model.QueueItems); }
        }
        return this;
	}

	/// <summary>Configures typed queue fixture declarations.</summary>
	/// <param name="configure">Declarations on a fresh queue model.</param>
	/// <returns>This builder for further configuration.</returns>
	public SimulationDataBuilder WithQueues(Action<SimulatedQueueModel> configure)
	{
        ArgumentNullException.ThrowIfNull(configure);
        var model = new SimulatedQueueModel();
        configure(model);
        return WithQueues(model);
	}

}
