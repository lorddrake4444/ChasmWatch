using Microsoft.AspNetCore.Authentication.Cookies;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCors();
        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        });
        services.AddSignalR();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = 401; // Return "Unauthorized" instead of a redirect
                return Task.CompletedTask;
            };
        });
        services.AddSingleton<IPlayerService, PlayerService>();
        services.AddSingleton<GameState>();
        services.AddSingleton<WorldMap>();
        services.AddSingleton<ObjectiveService>();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        app.UseCors(x => x
                .SetIsOriginAllowed(_ => true)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders("Content-Disposition")
                .AllowCredentials()

        ); // allow any origin
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHub<ChatHub>("/chat");
            endpoints.MapHub<GameHub>("/game");
        });
    }
}