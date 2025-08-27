using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate;

public static class SimulatorOptionsProcessor
{
	internal static EntityReference ConfigureAuthenticatedUser(MockedEntityDataService dataService, 
		ISimulatorOptions? options)
	{
		if (options?.AuthenticatedUser is not null)
		{
			dataService.Add(options.AuthenticatedUser);
			dataService.AuthenticatedUser = options.AuthenticatedUser.ToEntityReference();
			return options.AuthenticatedUser.ToEntityReference();    
		}
        
		var user = new Entity("systemuser")
		{
			Id = Guid.NewGuid(),
			Attributes =
			{
				["fullname"] = "Simulated User",
				["businessunitid"] = dataService.BusinessUnit,
				["OrganizationId"] = dataService.Organization
			}
		};
        
		dataService.Add(user);
		dataService.AuthenticatedUser = user.ToEntityReference();
		return user.ToEntityReference();
	}
	
	internal static void SetSystemTime(MockedEntityDataService dataService,
		ISimulatorOptions? options)
	{
		if (options?.ClockSimulator is not null)
		{
			dataService.SystemTime = options.ClockSimulator.Now;
		}
	}
	
	internal static void InitialiseMockedData(MockedEntityDataService dataService,
		ISimulatorOptions? options)
	{
		if (options?.InitialiseData is not null)
		{
			dataService.Add(options.InitialiseData);
		}
	}
	
	internal static EntityReference ConfigureUsersBusinessUnit(MockedEntityDataService dataService,
		ISimulatorOptions? options)
	{
		if (options?.BusinessUnit is not null)
		{
			dataService.Add(options.BusinessUnit);
			dataService.BusinessUnit = options.BusinessUnit.ToEntityReference();
			return options.BusinessUnit.ToEntityReference();
		}

		var businessUnit = new Entity("businessunit")
		{
			Id = Guid.NewGuid(),
			Attributes =
			{
				["name"] = "Simulated Root Business Unit"
			}
		};
        
		dataService.Add(businessUnit);
		dataService.BusinessUnit = businessUnit.ToEntityReference();
		return businessUnit.ToEntityReference();
	}
	
	internal static void ConfigureFiscalYearSettings(MockedEntityDataService dataService,
		ISimulatorOptions? options)
	{
		if (options?.FiscalYearSettings is not null)
		{
			dataService.FiscalYearSettings = options.FiscalYearSettings;
		}
	}

	public static EntityReference ConfigureOrganization(MockedEntityDataService dataService,
		ISimulatorOptions? options)
	{
		if (options?.Organization is not null)
		{
			dataService.Add(options.Organization);
			dataService.Organization = options.Organization.ToEntityReference();
			return options.Organization.ToEntityReference();
		}

		var organization = new Entity("organization")
		{
			Id = Guid.NewGuid(),
			Attributes =
			{
				["name"] = "Simulated Organization"
			}
		};
        
		dataService.Add(organization);
		dataService.Organization = organization.ToEntityReference();
		return organization.ToEntityReference();
	}


}