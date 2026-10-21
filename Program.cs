using SkyNet;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 110L * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 110L * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Builder.IISServerOptions>(o => o.MaxRequestBodySize = 110L * 1024 * 1024);

var app = builder.Build();
OnlineSchool.Core.Local.Accessor = app.Services.GetRequiredService<IHttpContextAccessor>();
OnlineSchool.Core.Init.OnStart().GetAwaiter().GetResult();

app.MapWhen(c => c.Request.Path.Equals("/stripe/webhook", StringComparison.OrdinalIgnoreCase), b => b.Run(OnlineSchool.Core.Pay.Webhook));

app.UseMiddleware<IHandler>();

app.UseStaticHttpCurrent();

app.Run();
