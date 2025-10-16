using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.QueryParsers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

/// <summary>
/// 
/// </summary>
/// <remarks>
/// N.B. Filters and LinkEntity currently only work if you've included the attributes in the ColumnSet
/// </remarks>
public class EntityMultipleRetriever(MockedEntityDataService dataService) : IEntityMultipleRetriever
{
    public void MockRequest(IOrganizationService organizationService, 
        ISimulatorOptions? options = null)
    {
        // Handle QueryExpression
        organizationService.RetrieveMultiple(Arg.Is<QueryExpression>(x => x != null))
            .Returns(
                x =>
                {
                    var query = x.Arg<QueryExpression>();
                    var results = QueryExpressionParser.Parse(query, 
                        dataService.Get(), dataService);
                    var resultCollection = new EntityCollection(results.Take(5000).ToList());
                    
                    if (query.PageInfo.ReturnTotalRecordCount)
                    {
                        resultCollection.TotalRecordCount = results.Take(5000).Count();
                        if (results.Count() > 5000)
                        {
                            resultCollection.TotalRecordCountLimitExceeded = true;
                        }
                    }
                    else
                    {
                        resultCollection.TotalRecordCount = -1;
                    }
                    
                    return resultCollection;
                });
        
        // Handle FetchExpression
        organizationService.RetrieveMultiple(Arg.Is<FetchExpression>(x => x != null))
            .Returns(
                x =>
                {
                    var query = x.Arg<FetchExpression>();
                    var results = FetchExpressionParser.Parse(query,
                        dataService.Get(), dataService);
                
                    return new EntityCollection(results.ToList()); 
                });

        // Handle QueryByAttribute
        organizationService.RetrieveMultiple(Arg.Is<QueryByAttribute>(x => x != null))
            .Returns(
                x =>
                {
                    var query = x.Arg<QueryByAttribute>();
                    var results = QueryByAttributeParser.Parse(query,
                        dataService.Get(), dataService);
                
                    return new EntityCollection(results.ToList());    
                });
    }
}