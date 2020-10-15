using System;
using System.IO;
using System.Threading.Tasks;
using AzCp.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace AzCp
{
  public static class Program
  {
    private const string ENVIRONMENT_PREFIX = "AZCP_";
    private static bool _isDevelopment;

    public static async Task<int> Main(string[] args)
    {
      // todo: Intercept Console.WriteLine to ensure it plays nicely with ConsoleFeedback
      Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();
      try
      {
        await CreateHostBuilder(args)
          .UseSerilog()
          .RunConsoleAsync();

        return 0;
      }
      catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
      {
        return 1;
      }
#pragma warning disable CA1031 // Do not catch general exception types
      catch (Exception ex)
      {
        ex.LogIt($@"{Repository.ApplicationFullVersion}: ");

        return 2;
      }
#pragma warning restore CA1031 // Do not catch general exception types
      finally
      {
        Console.Out.Flush();
        Log.CloseAndFlush();
      }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
          .ConfigureHostConfiguration(configHost =>
          {
            configHost.AddEnvironmentVariables(prefix: ENVIRONMENT_PREFIX);
            configHost.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), Repository.AppSettingsSecretsJsonFilename), true);
          })
          .ConfigureServices((hostContext, services) =>
          {
            services.AddSingleton(Log.Logger);
            services.AddTransient(typeof(IFeedback), typeof(ConsoleFeedback));
            services.AddHostedService<Application>();
            _isDevelopment = hostContext.HostingEnvironment.IsDevelopment();

            ExceptionExtension.IsDevelopment = _isDevelopment;
          });
  }
}