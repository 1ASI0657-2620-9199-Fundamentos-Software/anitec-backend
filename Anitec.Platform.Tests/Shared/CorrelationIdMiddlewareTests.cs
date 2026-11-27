using Anitec.Platform.Shared.Infrastructure.Pipeline.Middleware.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace Anitec.Platform.Tests.Shared;

/// <summary>Correlation of the requests inside the core (QAS-10): the id is kept, validated, logged and returned.</summary>
public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task WithoutAnId_OneIsCreated_AndItIsUsedByTheRequestTheResponseAndTheLog()
    {
        var (context, response) = NewContext("/api/v1/herds");
        var logger = new RecordingLogger();

        await new CorrelationIdMiddleware(_ => Task.CompletedTask, logger).InvokeAsync(context);
        await response.FireAsync();

        var id = context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.Matches("^[0-9a-f]{32}$", id);
        Assert.Equal(id, context.Response.Headers[CorrelationIdMiddleware.HeaderName]);
        Assert.Equal(id, context.Items[CorrelationIdMiddleware.ItemKey]);
        Assert.Equal(id, Assert.Single(logger.Entries).CorrelationId);
    }

    [Fact]
    public async Task AWellFormedId_IsKept()
    {
        var (context, response) = NewContext("/api/v1/herds");
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "mobile-app.2026-11-07_0042";
        var logger = new RecordingLogger();

        await new CorrelationIdMiddleware(_ => Task.CompletedTask, logger).InvokeAsync(context);
        await response.FireAsync();

        Assert.Equal("mobile-app.2026-11-07_0042", context.Response.Headers[CorrelationIdMiddleware.HeaderName]);
        Assert.Equal("mobile-app.2026-11-07_0042", Assert.Single(logger.Entries).CorrelationId);
    }

    [Theory]
    [InlineData("has spaces")]
    [InlineData("line\r\nbreak")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789")]
    public async Task AMalformedId_IsReplaced_SoItCannotInjectTextIntoTheLogs(string sent)
    {
        var (context, response) = NewContext("/api/v1/herds");
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = sent;
        var logger = new RecordingLogger();

        await new CorrelationIdMiddleware(_ => Task.CompletedTask, logger).InvokeAsync(context);
        await response.FireAsync();

        var id = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.NotEqual(sent, id);
        Assert.Matches("^[0-9a-f]{32}$", id);
    }

    [Fact]
    public async Task EveryLineOfTheRequest_CarriesTheId_IncludingTheOnesWrittenByOtherComponents()
    {
        var (context, _) = NewContext("/api/v1/herds");
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "abc-123";
        var logger = new RecordingLogger();

        await new CorrelationIdMiddleware(_ =>
        {
            logger.LogWarning("written by a handler");
            return Task.CompletedTask;
        }, logger).InvokeAsync(context);

        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, entry => Assert.Equal("abc-123", entry.CorrelationId));
    }

    [Fact]
    public async Task TheClosingLine_HasMethodPathStatusAndDuration_ButNeverTheQueryOrTheAuthorizationHeader()
    {
        var (context, _) = NewContext("/api/v1/herds");
        context.Request.QueryString = new QueryString("?token=very-secret-query");
        context.Request.Headers.Authorization = "Bearer very-secret-token";
        var logger = new RecordingLogger();

        await new CorrelationIdMiddleware(c =>
        {
            c.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }, logger).InvokeAsync(context);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("GET /api/v1/herds responded 404 in", entry.Message);
        Assert.DoesNotContain("very-secret", entry.Message);
    }

    [Fact]
    public async Task TheClosingLine_IsWrittenEvenWhenTheRequestFails()
    {
        var (context, _) = NewContext("/api/v1/herds");
        var logger = new RecordingLogger();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CorrelationIdMiddleware(_ => throw new InvalidOperationException("boom"), logger).InvokeAsync(context));

        Assert.Single(logger.Entries);
    }

    [Fact]
    public async Task TheHealthChecks_GoToTheDebugLevel_SoTheyDoNotBuryTheUsefulLines()
    {
        var (context, _) = NewContext("/health");
        var logger = new RecordingLogger();

        await new CorrelationIdMiddleware(_ => Task.CompletedTask, logger).InvokeAsync(context);

        Assert.Equal(LogLevel.Debug, Assert.Single(logger.Entries).Level);
    }

    private static (DefaultHttpContext Context, CapturingResponseFeature Response) NewContext(string path)
    {
        var response = new CapturingResponseFeature();
        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(new HttpRequestFeature());
        features.Set<IHttpResponseFeature>(response);
        features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(Stream.Null));
        var context = new DefaultHttpContext(features);
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        return (context, response);
    }

    /// <summary>The default response feature ignores OnStarting; this one keeps the callbacks and runs them on demand.</summary>
    private sealed class CapturingResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _callbacks = [];

        public override void OnStarting(Func<object, Task> callback, object state)
        {
            _callbacks.Add((callback, state));
        }

        public async Task FireAsync()
        {
            foreach (var (callback, state) in _callbacks)
                await callback(state);
        }
    }

    private sealed record Entry(LogLevel Level, string Message, string? CorrelationId);

    /// <summary>Keeps every line together with the correlation id of the scope that was open when it was written.</summary>
    private sealed class RecordingLogger : ILogger<CorrelationIdMiddleware>
    {
        private readonly AsyncLocal<Stack<object>> _scopes = new();

        public List<Entry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            (_scopes.Value ??= new Stack<object>()).Push(state);
            return new Scope(_scopes.Value);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var id = _scopes.Value?.OfType<IDictionary<string, object>>()
                .Select(scope => scope.TryGetValue("CorrelationId", out var value) ? value as string : null)
                .FirstOrDefault(value => value is not null);
            Entries.Add(new Entry(logLevel, formatter(state, exception), id));
        }

        private sealed class Scope(Stack<object> stack) : IDisposable
        {
            public void Dispose()
            {
                stack.Pop();
            }
        }
    }
}
