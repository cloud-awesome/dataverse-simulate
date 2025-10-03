namespace CloudAwesome.Xrm.Simulate.DataStores;

public class SimulatedBusinessUnit
{
	public string? Name { get; set; }
	public Guid Id { get; set; }
	public List<SimulatedBusinessUnit> ChildBusinessUnits { get; set; } = new List<SimulatedBusinessUnit>();
}