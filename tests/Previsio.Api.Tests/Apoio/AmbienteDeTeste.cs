using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Previsio.Api.Tests.Apoio;

public class AmbienteDeTeste : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Production";
    public string ApplicationName { get; set; } = "Previsio.Api";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
