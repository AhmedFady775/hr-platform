extern alias WebAssembly;

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WebAssemblyProgram = WebAssembly::Program;

namespace HrPlatform.Tests.Web;

// fakes out the HttpClient behind LeaveRequestsApiClient so /login-submit and /logout can be
// exercised end-to-end (cookie sign-in, redirects) without a real HrPlatform.Api running
public class CustomWebFactory : WebApplicationFactory<WebAssemblyProgram>
{
    public Func<HttpRequestMessage, HttpResponseMessage> RespondToApi { get; set; } =
        _ => new HttpResponseMessage(HttpStatusCode.NotImplemented);

    private class FakeHandler : HttpMessageHandler
    {
        private readonly CustomWebFactory _owner;
        public FakeHandler(CustomWebFactory owner) => _owner = owner;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_owner.RespondToApi(request));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddHttpClient<WebAssembly::HrPlatform.Web.Services.LeaveRequestsApiClient>(client =>
            {
                client.BaseAddress = new Uri("https://fake-hr-api/");
            }).ConfigurePrimaryHttpMessageHandler(() => new FakeHandler(this));
        });
    }
}
