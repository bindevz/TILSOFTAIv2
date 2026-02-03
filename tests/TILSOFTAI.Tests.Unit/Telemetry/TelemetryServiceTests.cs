using System.Diagnostics;
using TILSOFTAI.Domain.Logging;
using TILSOFTAI.Infrastructure.Telemetry;
using Xunit;

namespace TILSOFTAI.Tests.Unit.Telemetry
{
    public class TelemetryServiceTests
    {
        [Fact]
        public void StartActivity_ShouldCreateActivity_WhenSourceHasListeners()
        {
            // Note: ActivitySource requires a listener to actually create an activity object
            using var listener = new ActivityListener
            {
                ShouldListenTo = s => s.Name == "TILSOFTAI",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            var service = new TelemetryService();
            using var activity = service.StartActivity("test_activity");

            Assert.NotNull(activity);
            Assert.Equal("test_activity", activity.OperationName);
        }

        [Fact]
        public void StartActivity_ShouldPropagateLogContext()
        {
             using var listener = new ActivityListener
            {
                ShouldListenTo = s => s.Name == "TILSOFTAI",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            LogContext.Current = new LogContext
            {
                TenantId = "tenant_123",
                UserId = "user_456",
                ConversationId = "conv_789"
            };

            var service = new TelemetryService();
            using var activity = service.StartActivity("context_test");

            Assert.NotNull(activity);
            Assert.Equal("tenant_123", activity.GetTagItem(TILSOFTAI.Domain.Telemetry.TelemetryConstants.Attributes.TenantId));
            Assert.Equal("user_456", activity.GetTagItem(TILSOFTAI.Domain.Telemetry.TelemetryConstants.Attributes.UserId));
            Assert.Equal("conv_789", activity.GetTagItem(TILSOFTAI.Domain.Telemetry.TelemetryConstants.Attributes.ConversationId));
        }
    }
}
