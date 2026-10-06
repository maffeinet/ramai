using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ramai.Api.Middleware;

namespace Ramai.UnitTests;

public sealed class CorrelationIdTests
{
    [Theory]
    [InlineData("request-123")]
    [InlineData("A.b_c:1")]
    public async Task Valid_id_is_reused_in_response_context_and_log(string id)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = id;
        var logger = new CaptureLogger();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, new TestEnvironment());
        await middleware.InvokeAsync(context, logger);
        Assert.Equal(id, context.TraceIdentifier);
        Assert.Equal(id, context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
        Assert.Equal(id, logger.Scope["CorrelationId"]);
        Assert.Equal("Test", logger.Scope["Environment"]);
        Assert.Equal(LogLevel.Information, logger.Level);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has spaces")]
    [InlineData("bad!")]
    [InlineData("nonascii-é")]
    public async Task Invalid_or_missing_id_generates_a_guid(string id)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = id;
        await new CorrelationIdMiddleware(_ => Task.CompletedTask, new TestEnvironment())
            .InvokeAsync(context, new CaptureLogger());
        Assert.True(Guid.TryParseExact(context.TraceIdentifier, "N", out _));
        Assert.Equal(context.TraceIdentifier, context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task Oversized_id_is_replaced_and_sensitive_request_data_is_not_logged()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = new string('a', 129);
        context.Request.QueryString = new QueryString("?token=SENSITIVE_SENTINEL");
        context.Request.Headers.Authorization = "Bearer SENSITIVE_SENTINEL";
        var logger = new CaptureLogger();
        await new CorrelationIdMiddleware(_ => Task.CompletedTask, new TestEnvironment())
            .InvokeAsync(context, logger);
        Assert.True(Guid.TryParseExact(context.TraceIdentifier, "N", out _));
        Assert.DoesNotContain("SENSITIVE_SENTINEL", logger.Message);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Ramai.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class CaptureLogger : ILogger<CorrelationIdMiddleware>
    {
        public Dictionary<string, object> Scope { get; private set; } = [];
        public string Message { get; private set; } = "";
        public LogLevel Level { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            Scope = Assert.IsType<Dictionary<string, object>>(state);
            return null;
        }
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Level = logLevel;
            Message = formatter(state, exception);
        }
    }
}
