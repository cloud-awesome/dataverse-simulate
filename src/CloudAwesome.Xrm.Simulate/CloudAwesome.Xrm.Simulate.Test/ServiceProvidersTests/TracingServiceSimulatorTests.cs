using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.ServiceProvidersTests;

[TestFixture]
public class TracingServiceSimulatorTests
{
    private readonly IServiceProvider _serviceProvider = null!;
    private ITracingService _tracingService = null!;
    
    private const string TraceMessage = "This is a test plugin trace"; 
    private const string MessageFormat = "Record created with ID {0} at {1}";
    private readonly Guid _id = Guid.NewGuid();
    private readonly DateTime _dateTime = DateTime.Now;
    
    [Test]
    public void Standard_Trace_Is_Saved_To_Logging_Store()
    {
        var service = _serviceProvider.Simulate();
        _tracingService = (ITracingService) service.GetService(typeof(ITracingService))!;

        _tracingService.Trace(TraceMessage);

        var traces = service.Simulated().Logs().Get();

        traces.Count.Should().Be(1);
        traces.FirstOrDefault()!.Should().Be(TraceMessage);
    }
    
    [Test]
    public void Multiple_Traces_Is_Saved_To_Logging_Store()
    {
        var service = _serviceProvider.Simulate();
        _tracingService = (ITracingService) service.GetService(typeof(ITracingService))!;
        
        _tracingService.Trace(TraceMessage);
        _tracingService.Trace($"Second logging: {TraceMessage}");

        var traces = service.Simulated().Logs().Get();

        traces.Count.Should().Be(2);
    }

    [Test]
    public void Structured_Logging_Is_Supported_In_The_Trace()
    {
        var service = _serviceProvider.Simulate();
        _tracingService = (ITracingService) service.GetService(typeof(ITracingService))!;
        
        _tracingService.Trace(MessageFormat, _id, _dateTime);
        
        var traces = service.Simulated().Logs().Get();

        traces.Count.Should().Be(1);
        traces.FirstOrDefault()!.Should().Be(String.Format(MessageFormat, _id, _dateTime));
    }
}
