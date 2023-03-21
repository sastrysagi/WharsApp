using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog;
using System;
using ILogger = Serilog.ILogger;

namespace WhatsAppServicesAPI.Middlewares
{
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public LoggingMiddleware(RequestDelegate next, ILogger logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var correlationId = Guid.NewGuid().ToString();

            // Log request
            var requestBody = await GetRequestBody(context.Request);
            _logger.Information("Request: {@Request}", new
            {
                CorrelationId = correlationId,
                Method = context.Request.Method,
                Path = context.Request.Path,
                QueryString = context.Request.QueryString,
                Headers = context.Request.Headers,
                Cookies = context.Request.Cookies,
                Body = requestBody
            });

            // Log response
            var originalBodyStream = context.Response.Body;
            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            await _next(context);

            var responseBody = await GetResponseBody(context.Response);
            _logger.Information("Response: {@Response}", new
            {
                CorrelationId = correlationId,
                StatusCode = context.Response.StatusCode,
                Headers = context.Response.Headers,
                Body = responseBody
            });

            // Copy the response back to the original stream
            await responseBodyStream.CopyToAsync(originalBodyStream);
        }

        private async Task<string> GetRequestBody(HttpRequest request)
        {
            request.EnableBuffering();
            var bodyStream = new StreamReader(request.Body, Encoding.UTF8);
            var bodyText = await bodyStream.ReadToEndAsync();
            request.Body.Position = 0;
            return bodyText;
        }

        private async Task<string> GetResponseBody(HttpResponse response)
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            var bodyStream = new StreamReader(response.Body, Encoding.UTF8);
            var bodyText = await bodyStream.ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);
            return bodyText;
        }
    }
}
