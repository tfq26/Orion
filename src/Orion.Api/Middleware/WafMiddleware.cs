using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Orion.Api.Middleware
{
    public class WafMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly Regex SqlInjectionPattern = new Regex(@"(?i)\b(UNION|SELECT|INSERT|UPDATE|DELETE|DROP|ALTER)\b");
        private static readonly Regex XssPattern = new Regex(@"(?i)(<script.*?>|javascript:|alert\()");

        public WafMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var query = context.Request.QueryString.Value ?? string.Empty;

            if (SqlInjectionPattern.IsMatch(path) || SqlInjectionPattern.IsMatch(query) ||
                XssPattern.IsMatch(path) || XssPattern.IsMatch(query))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("WAF Blocked Request: Malicious payload detected.");
                return;
            }

            await _next(context);
        }
    }
}
