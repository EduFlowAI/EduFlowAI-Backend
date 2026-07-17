using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EduFlowAI.Api.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace EduFlowAI.AI.Tests;

public class ExceptionHandlerTests
{
    private static async Task<(int status, string body)> Handle(Exception ex)
    {
        var handler = new ExceptionHandler(NullLogger<ExceptionHandler>.Instance);
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(ctx, ex, CancellationToken.None);
        Assert.True(handled);

        ctx.Response.Body.Position = 0;
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        return (ctx.Response.StatusCode, body);
    }

    [Fact]
    public async Task KeyNotFound_Maps404_WithTraceId()
    {
        var (status, body) = await Handle(new KeyNotFoundException("no such document"));

        Assert.Equal(404, status);
        Assert.Contains("Resource not found", body);
        Assert.Contains("traceId", body);
    }

    [Fact]
    public async Task ArgumentException_Maps400()
    {
        var (status, _) = await Handle(new ArgumentException("bad input"));
        Assert.Equal(400, status);
    }

    [Fact]
    public async Task HttpRequestException_Maps502_AndHidesUpstreamDetail()
    {
        var (status, body) = await Handle(new HttpRequestException("Gemini 503 SECRET-UPSTREAM-BODY"));

        Assert.Equal(502, status);
        Assert.DoesNotContain("SECRET-UPSTREAM-BODY", body);
    }

    [Fact]
    public async Task UnknownException_Maps500_AndDoesNotLeakMessage()
    {
        var (status, body) = await Handle(new InvalidOperationException("INTERNAL-STACK-SECRET"));

        Assert.Equal(500, status);
        Assert.DoesNotContain("INTERNAL-STACK-SECRET", body);
    }
}
