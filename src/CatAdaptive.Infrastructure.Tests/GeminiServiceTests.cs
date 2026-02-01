using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using CatAdaptive.Infrastructure.Generation;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Infrastructure.Tests;

public sealed class GeminiServiceTests
{
    [Fact]
    public async Task GenerateTextAsync_ReturnsFallbackWhenApiKeyMissing()
    {
        var service = new GeminiService(null, "model", MockLogger<GeminiService>.Instance);

        var text = await service.GenerateTextAsync("explanation");

        text.Should().NotBeNullOrWhiteSpace();
        text.Should().Contain("explanation").And.Contain("concept");
    }

    [Fact]
    public async Task GenerateFromPromptAsync_ParsesJsonFromResponse()
    {
        var service = new GeminiService("key", "model", MockLogger<GeminiService>.Instance);
        ReplaceHttpClient(service, BuildClient("{\"name\":\"value\"}"));
        SetPrivateField(service, "_apiKey", "key");

        var result = await service.GenerateFromPromptAsync<SampleDto>("prompt");

        result.Name.Should().Be("value");
    }

    [Fact]
    public async Task GenerateFromPromptAsync_ThrowsWhenJsonMissing()
    {
        var service = new GeminiService(null, "model", MockLogger<GeminiService>.Instance);

        var act = async () => await service.GenerateFromPromptAsync<SampleDto>("prompt");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static HttpClient BuildClient(string responseText)
    {
        var handler = new FakeHandler(responseText);
        return new HttpClient(handler);
    }

    private static void ReplaceHttpClient(GeminiService service, HttpClient client)
    {
        SetPrivateField(service, "_httpClient", client);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        field!.SetValue(target, value);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _text;

        public FakeHandler(string text)
        {
            _text = text;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var responseBody = $"{{\"candidates\":[{{\"content\":{{\"parts\":[{{\"text\":{Escape(_text)} }}]}}}}]}}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }

        private static string Escape(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }

    private sealed record SampleDto(string Name);

    private sealed class MockLogger<T> : ILogger<T>
    {
        public static MockLogger<T> Instance { get; } = new();

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }
}
