using Microsoft.AspNetCore.Mvc;

namespace CustomerService.Features.Authentication;

public static class AuthenticationEndpoint
{
    public static IEndpointRouteBuilder MapAuthentication(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            JwtTokenGenerator tokenGenerator) =>
        {
            // For demo purposes - in production, validate against database
            // This is a simplified implementation for testing
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return Results.BadRequest(new { error = "Email and password are required" });
            }

            // Demo: Accept any email/password for testing
            // In production: Validate credentials against database with hashed passwords
            var userId = Guid.NewGuid().ToString();
            var roles = new List<string> { "User", "Admin" };

            var token = tokenGenerator.GenerateToken(
                userId,
                request.Email.Split('@')[0],
                request.Email,
                roles
            );

            var expirationInMinutes = 60;
            var response = new LoginResponse(
                token,
                "Bearer",
                expirationInMinutes * 60,
                userId,
                request.Email
            );

            return Results.Ok(response);
        })
        .WithName("Login")
        .WithTags("Authentication")
        .Produces<LoginResponse>()
        .ProducesValidationProblem()
        .AllowAnonymous();

        return app;
    }
}
