using EdgeHub.Configuration;
using Microsoft.Extensions.Options;

namespace EdgeHub.Security;

/// <summary>
/// The tablet opens the page once with <c>?k=&lt;token&gt;</c>: the token goes into a cookie and the address is cleaned,
/// so the token does not stay in the browser's history. Every <c>/api/*</c> call then needs that cookie (or <c>?k=</c>),
/// and the cookie is renewed on each call. A call that changes something must also come from the screen itself: a page
/// on another port of the same PC would carry the cookie, so the Fetch metadata or the Origin header is checked too.
/// </summary>
public sealed class AccessTokenMiddleware(RequestDelegate next, IOptionsMonitor<SecurityOptions> options)
{
    public const string CookieName = "edgehub_k";
    private const string QueryKey = "k";

    private static readonly CookieOptions CookieSettings = new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        MaxAge = TimeSpan.FromDays(400),
        IsEssential = true,
    };

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string expected = options.CurrentValue.Token;
        HttpRequest request = context.Request;
        bool isApi = request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

        if (request.Query.TryGetValue(QueryKey, out var fromQuery))
        {
            string presented = fromQuery.ToString();
            if (!AccessToken.Matches(presented, expected))
            {
                await RefuseAsync(context, isApi).ConfigureAwait(false);
                return;
            }

            context.Response.Cookies.Append(CookieName, presented, CookieSettings);
            if (HttpMethods.IsGet(request.Method) && !isApi)
            {
                context.Response.Redirect(AddressWithoutToken(request));
                return;
            }
        }
        else if (isApi)
        {
            string? fromCookie = request.Cookies[CookieName];
            if (!AccessToken.Matches(fromCookie, expected))
            {
                await RefuseAsync(context, isApi).ConfigureAwait(false);
                return;
            }

            context.Response.Cookies.Append(CookieName, fromCookie!, CookieSettings);
        }

        if (isApi && !IsReadOnly(request.Method) && !ComesFromTheScreenItself(request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool IsReadOnly(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

    private static bool ComesFromTheScreenItself(HttpRequest request)
    {
        string? site = request.Headers["Sec-Fetch-Site"];
        if (!string.IsNullOrEmpty(site))
        {
            return string.Equals(site, "same-origin", StringComparison.OrdinalIgnoreCase);
        }

        string? origin = request.Headers.Origin;
        return !string.IsNullOrEmpty(origin)
            && string.Equals(origin, $"{request.Scheme}://{request.Host}", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task RefuseAsync(HttpContext context, bool isApi)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        if (!isApi)
        {
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(
                "Jeton refusé. Ouvre l'adresse complète donnée à l'installation (elle se termine par ?k=…).",
                context.RequestAborted).ConfigureAwait(false);
        }
    }

    /// <summary>The same local address without the token, always starting with a single "/".</summary>
    private static string AddressWithoutToken(HttpRequest request)
    {
        string path = "/" + (request.PathBase + request.Path).ToString().TrimStart('/');
        QueryString others = QueryString.Create(
            request.Query.Where(q => !string.Equals(q.Key, QueryKey, StringComparison.OrdinalIgnoreCase)));
        return path + others;
    }
}
