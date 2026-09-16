using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk.PluginTelemetry;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate.ServiceProviders;

public static class TelemetrySimulator
{
    public static ILogger? Create(MockedEntityDataService dataService, MockedTelemetryService mockedTelemetryService,
        ISimulatorOptions? options)
    {
        if (dataService.FakeServiceFailureSettings is { TelemetryService: true })
        {
            return null;
        }

        var telemetryService = Substitute.For<ILogger>();

        telemetryService
            .When(x => x.Log(Arg.Any<LogLevel>(), Arg.Any<string>(), Arg.Any<object[]>()))
            .Do(callInfo =>
            {
                var logLevel = callInfo.Arg<LogLevel>();
                var message = callInfo.Arg<string>();
                var parameters = callInfo.Arg<object[]>();

                mockedTelemetryService.Add(logLevel, message, parameters);
            });

        ConfigureTelemetryMock(telemetryService, mockedTelemetryService, telemetryService.LogCritical, LogLevel.Critical);
        ConfigureTelemetryMock(telemetryService, mockedTelemetryService, telemetryService.LogError, LogLevel.Error);
        ConfigureTelemetryMock(telemetryService, mockedTelemetryService, telemetryService.LogWarning, LogLevel.Warning);
        ConfigureTelemetryMock(telemetryService, mockedTelemetryService, telemetryService.LogInformation, LogLevel.Information);
        ConfigureTelemetryMock(telemetryService, mockedTelemetryService, telemetryService.LogTrace, LogLevel.Trace);
        ConfigureTelemetryMock(telemetryService, mockedTelemetryService, telemetryService.LogDebug, LogLevel.Debug);

        return telemetryService;
    }

    private static void ConfigureTelemetryMock(
        ILogger telemetryService,
        MockedTelemetryService mockedTelemetryService,
        Action<string, object[]> logAction,
        LogLevel logLevel)
    {
        telemetryService
            .When(x => logAction(Arg.Any<string>(), Arg.Any<object[]>()))
            .Do(callInfo =>
            {
                var message = callInfo.Arg<string>();
                var parameters = callInfo.Arg<object[]>();
                mockedTelemetryService.Add(logLevel, message, parameters);
            });
    }
}
