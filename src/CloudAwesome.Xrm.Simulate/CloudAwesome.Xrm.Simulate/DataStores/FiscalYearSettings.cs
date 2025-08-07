namespace CloudAwesome.Xrm.Simulate.DataStores;

public class FiscalYearSettings
{
	/// <summary>
	/// 1..12; default is 1 for January
	/// </summary>
	public int FiscalYearStartMonth { get; set; } = 1;

	/// <summary>
	/// If true, the fiscal year label = the year that the fiscal year starts in (DEFAULT)
	/// If false, the fiscal year label = the year that it ends in (common in finance)
	/// </summary>
	public bool FiscalYearNameBasedOnStartDate { get; set; } = true;

	/// <summary>
	/// Defaults to FY, as defaulted in dataverse
	/// </summary>
	public string FiscalYearPrefix { get; set; } = "FY";
}