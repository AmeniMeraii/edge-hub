using EdgeHub.Configuration;
using Microsoft.Extensions.Options;

namespace EdgeHub.Security;

/// <summary>
/// The tablet opens the page once with <c>?k=&lt;token&gt;</c>: the token goes into a cookie and the address is cleaned,
/// so the token does not stay in the browser's history. Every <c>/api/*</c> call then needs that cookie (or <c>?k=</c>).
/// </summary>
public sealed class AccessTokenMiddleware(RequestDelegate next, IOptionsMonitor<SecurityOptions> options)
{
    public const string CookieName = "edgehub_k";
    private const string QueryKey = "k";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string expected = options.CurrentValue.Token;
        HttpRequest request = context.Request;

        if (request.Query.TryGetValue(QueryKey, out var fromQuery))
        {
            if (!AccessToken.Matches(fromQuery.ToString(), expected))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            context.Response.Cookies.Append(CookieName, expected, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                MaxAge = TimeSpan.FromDays(400),
                IsEssential = true,
            });

            if (HttpMethods.IsGet(request.Method) && !IsApi(request.Path))
            {
                context.Response.Redirect(AddressWithoutToken(request));
                return;
            }
        }
        else if (IsApi(request.Path) && !AccessToken.Matches(request.Cookies[CookieName], expected))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool IsApi(PathString path) => path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

    private static string AddressWithoutToken(HttpRequest request)
    {
        QueryString others = QueryString.Create(request.Query.Where(q => q.Key != QueryKey));
        return $"{request.PathBase}{request.Path}{others}";
    }
}
