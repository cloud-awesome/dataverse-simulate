using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CloudAwesome.Xrm.Simulate.QueryParsers.ConditionHandlers;

public class EqualConditionHandler : IConditionHandler
{
    public ConditionOperator Operator => ConditionOperator.Equal;

    public bool Evaluate(Entity entity, ConditionExpression condition, MockedEntityDataService dataService)
    {
        var attributeValue = entity.GetAttributeValue<object>(condition.AttributeName);

        return QueryValueComparer.EqualsByDataverseValue(attributeValue, condition.Values[0]);
    }
}
