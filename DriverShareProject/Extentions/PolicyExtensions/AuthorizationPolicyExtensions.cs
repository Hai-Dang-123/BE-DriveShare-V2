namespace DriverShareProject.Extentions.PolicyExtensions
{
    public static class AuthorizationPolicyExtensions
    {
        public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy =>
                    policy.RequireRole("Admin"));

                //options.AddPolicy("RequireWritePermission", policy =>
                //    policy.RequireClaim("Permission", "Write"));
            });

            return services;
        }
    }
}
