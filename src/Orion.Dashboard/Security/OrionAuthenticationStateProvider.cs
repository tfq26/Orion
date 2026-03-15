using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Orion.Dashboard.Security
{
    public class OrionAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly HttpClient _httpClient;
        private AuthenticationState _anonymous;

        public OrionAuthenticationStateProvider(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _anonymous = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                var userResponse = await _httpClient.GetFromJsonAsync<UserResponse>("/auth/user");

                if (userResponse == null || !userResponse.IsAuthenticated)
                {
                    return _anonymous;
                }

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, userResponse.UserId ?? ""),
                    new(ClaimTypes.Name, userResponse.Name ?? ""),
                    new(ClaimTypes.Email, userResponse.Email ?? ""),
                    new("display_name", userResponse.Name ?? ""),
                    new("picture", userResponse.Picture ?? "")
                };

                var identity = new ClaimsIdentity(claims, "WorkOS");
                var user = new ClaimsPrincipal(identity);

                return new AuthenticationState(user);
            }
            catch
            {
                return _anonymous;
            }
        }

        public void NotifyAuthenticationStateChanged()
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        private class UserResponse
        {
            public bool IsAuthenticated { get; set; }
            public string? UserId { get; set; }
            public string? Email { get; set; }
            public string? Name { get; set; }
            public string? Picture { get; set; }
        }
    }
}
