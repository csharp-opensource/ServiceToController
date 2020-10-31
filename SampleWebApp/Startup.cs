using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceToController;
using System;
using System.Threading.Tasks;

namespace SampleWebApp
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSwaggerDocument(c =>
            {
                c.Version = "v1";
                c.Title = $"Swagger";
                c.Description = "A simple example ASP.NET Core Web API";
                c.AddSecurity("Bearer", new NSwag.OpenApiSecurityScheme
                {
                    //Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
                    Description = "Api Key - ex: /chat?key={apikey}",
                    Name = "key",
                    In = NSwag.OpenApiSecurityApiKeyLocation.Query,
                    Type = NSwag.OpenApiSecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
            });
            var builder = services.AddControllers().AddControllersAsServices();
            dynamic mainInstance = builder.AddCastedService<MyService>(castOptions: new CastOptions
            {
                UseNewInstanceEveryMethod = true,
                BeforeMethod = (_) => Console.WriteLine("BeforeMethod"),
                AfterMethod = (_, res) =>
                {
                    if (res is Task<MyObject> obj)
                    {
                        obj.Result.Name += "-AfterMethod";
                    }
                    return res;
                }
            });
            mainInstance.IsNew = false;
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseOpenApi();
            app.UseSwaggerUi3(c => c.DocExpansion = "list");
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action=Index}/{id?}");
            });
        }
    }
}
