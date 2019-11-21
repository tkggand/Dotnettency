using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Dotnettency;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Sample.Pages
{
    public class Startup
    {
        [Obsolete]
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var defaultServices = services.Clone();

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
            });

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddMultiTenancy<Tenant>((builder) =>
             {
                 builder.IdentifyTenantsWithRequestAuthorityUri()
                        .InitialiseTenant<TenantShellFactory>()
                        .AddAspNetCore()
                        .ConfigureTenantFileProviders((hostingOptions) =>
                        {
                            var hostWebRootFileProvider = Environment.WebRootFileProvider;
                            hostingOptions.ConfigureTenantWebRootFileProvider(Environment.WebRootPath, (webRootOptions) =>
                            {
                                // WE use the tenant's guid id to partition one tenants files from another on disk.
                                Guid tenantGuid = (webRootOptions.Tenant?.TenantGuid).GetValueOrDefault();
                                webRootOptions.TenantPartitionId(tenantGuid)
                                                   .AllowAccessTo(hostWebRootFileProvider); // We allow the tenant web root file provider to access the environments web root files.
                            }, fp =>
                            {
                                // The file provider we add here, is one that dynamically switches based on the active tenant partition configuration above.
                                Environment.WebRootFileProvider = new CompositeFileProvider(new[] { fp, hostWebRootFileProvider });
                            });

                            var hostContentRootFileProvider = Environment.ContentRootFileProvider;
                            hostingOptions.ConfigureTenantContentFileProvider(Environment.ContentRootPath, (contentRootOptions) =>
                            {
                                // WE use the tenant's guid id to partition one tenants files from another on disk.
                                Guid tenantGuid = (contentRootOptions.Tenant?.TenantGuid).GetValueOrDefault();
                                contentRootOptions.TenantPartitionId(tenantGuid)
                                                   .AllowAccessTo(hostContentRootFileProvider); // We allow the tenant web root file provider to access the environments web root files.
                            }, fp =>
                            {
                                // The file provider we add here, is one that dynamically switches based on the active tenant partition configuration above.
                                Environment.ContentRootFileProvider = new CompositeFileProvider(new[] { fp, hostContentRootFileProvider });
                            });

                        })
                        .ConfigureTenantConfiguration((a, tenantConfig) =>
                        {
                            tenantConfig.AddJsonFile(Environment.ContentRootFileProvider, $"/appsettings.{a.Tenant?.Name}.json", true, true);
                        })
                        .ConfigureTenantContainers((containerOptions) =>
                        {
                            containerOptions
                            .SetDefaultServices(defaultServices)
                            .AutofacAsync(async (tenantContext, tenantServices) =>
                            {
                                // Can now use tenant level configuration to decide how to bootstrap the tenants services here..
                                var currentTenantConfig = await tenantContext.GetConfigurationAsync();
                                var someTenantConfigSetting = currentTenantConfig.GetValue<bool>("SomeSetting");
                                if (someTenantConfigSetting)
                                {
                                    // register services certain way for this tenant. 
                                }

                                if (tenantContext.Tenant != null)
                                {
                                    tenantServices.AddRazorPages((o) =>
                                    {
                                        o.RootDirectory = $"/Pages/{tenantContext.Tenant.Name}";
                                    }).AddNewtonsoftJson();
                                }
                            });
                        })
                        .ConfigureTenantMiddleware((tenantOptions) =>
                        {
                            tenantOptions.AspNetCorePipelineTask(async (context, tenantAppBuilder) =>
                            {

                                var tenantConfig = await context.GetConfiguration();
                                var someTenantConfigSetting = tenantConfig.GetValue<bool>("SomeSetting");
                                if (someTenantConfigSetting)
                                {
                                    // register services certain way for this tenant. 
                                }

                                // Example of using your own custom tenant shell items. 
                                // Check out the ConfigureTenantShellItem<> below.
                                // You can also inject Task<ExampleShellItem> into controllers etc.
                                // ExampleShellItem will be lazily constructed only per tenant, or once after a tenant restart.
                                var exampleShellItem = await context.GetShellItemAsync<ExampleShellItem>();
                                Console.WriteLine("Got tenant shell item for tenant: " + exampleShellItem.TenantName);

                                tenantAppBuilder.Use(async (c, next) =>
                                {
                                    // This is some middleware running in the tenant pipeline.
                                    Console.WriteLine("Running in tenant pipeline: " + context.Tenant?.Name);
                                    await next.Invoke();
                                });

                                tenantAppBuilder.UseRouting();

                                if (context.Tenant != null)
                                {
                                    tenantAppBuilder.UseAuthorization();

                                    tenantAppBuilder.Use(async (c, next) =>
                                    {
                                        // Demonstrates per tenant files.
                                        // /foo.txt exists for one tenant but not another.
                                        var webHostEnvironment = c.RequestServices.GetRequiredService<IWebHostEnvironment>();
                                        var contentFileProvider = webHostEnvironment.ContentRootFileProvider;
                                        var webFileProvider = webHostEnvironment.WebRootFileProvider;

                                        var fooTextFile = webFileProvider.GetFileInfo("/foo.txt");

                                        Console.WriteLine($"/Foo.txt file exists? {fooTextFile.Exists}");

                                        // Demonstrates per tenant config.
                                        // SomeSetting is true for Moogle tenant but not other tenants.                                       
                                        Console.WriteLine($"Tenant config setting: {someTenantConfigSetting}");

                                        await next.Invoke();
                                    });

                                    tenantAppBuilder.UseEndpoints(endpoints =>
                                    {
                                        endpoints.MapRazorPages();
                                    });
                                }
                            });
                        })
                        .ConfigureTenantShellItem((b) =>
                        {
                            // Example - you can configure your own arbitrary shell items
                            // This item will be lazily constructed once per tenant, and stored in tenant shell, and 
                            // optionally disposed of if tenant is restarted (if implements IDisposable).
                            return new ExampleShellItem(b.Tenant?.Name ?? "NULL TENANT");


                            // To access this item, you can do either / all of:
                            // 1. Inject Task<ExampleShellItem> into your controllers etc, then await that task where needed.
                            // 2. Inject ITenantShellItemAccessor<ExampleShellItem> then await it's lazy factory task.
                            // 3. Use IServiceProvider extension method e.g:  await ApplicationServices.GetShellItemAsync<Tenant, ExampleShellItem>();
                            // 4. In startup methods above, use "await context.GetShellItemAsync<ExampleShellItem>()" as shown in the middleware example.

                            // Note: Tenant shell items are removed from the Tenant Shell if the tenant is restarted.
                            //        and then lazily re-initialised again when first consumed after the tenant restart

                            // Another Note: If your service implements IDisposable, it will also be disposed of, when the tenant is restarted.
                            // (That's the convention for any item stored in Tenant Shell)


                        });

             });

        }

        public class ExampleShellItem
        {
            public ExampleShellItem(string tenantName)
            {
                TenantName = tenantName;
                Console.WriteLine($"ExampleShellItem constructed for tenant: {tenantName}");
            }

            public string TenantName { get; set; }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseMultitenancy<Tenant>((builder) =>
            {
                builder.UseTenantContainers()
                       .UsePerTenantMiddlewarePipeline(app);
            });
        }
    }
}
