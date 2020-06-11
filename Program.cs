using System;
using System.Threading.Tasks;
using AzCp.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AzCp
{
  public static class Program
  {
    private const string ENVIRONMENT_PREFIX = "AZCP_";

    public static async Task<int> Main(string[] args)
    {
      try
      {
        await CreateHostBuilder(args).RunConsoleAsync();
        return 0;
      }
      catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
      {
        return 1;
      }
#pragma warning disable CA1031 // Do not catch general exception types
      catch
      {
        return 2;
      }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
          .ConfigureHostConfiguration(configHost =>
          {
            configHost.AddEnvironmentVariables(prefix: ENVIRONMENT_PREFIX);
          })
          .ConfigureServices((hostContext, services) =>
          {
            services.AddSingleton<IFeedback>(new ConsoleFeedback());
            services.AddHostedService<Application>();
          });
  }
}