using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzCp
{
  public static class Program
  {
    public static IConfigurationRoot _configuration;

    public static int Main(string[] args)
    {
      // Initialize serilog logger
      Log.Logger = new LoggerConfiguration()
           .WriteTo.Console(Serilog.Events.LogEventLevel.Debug)
           .MinimumLevel.Debug()
           .Enrich.FromLogContext()
           .CreateLogger();

      try
      {
        MainAsync(args).Wait();
        return 0;
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Runtime error");
        return 1;
      }
      finally
      {
        Log.CloseAndFlush();
      }
    }

    private static async Task MainAsync(string[] _)
    {
      // Create service collection
      Log.Information("Creating service collection");
      ServiceCollection serviceCollection = new ServiceCollection();
      ConfigureServices(serviceCollection);

      // Create service provider
      Log.Information("Building service provider");
      IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

      // Print connection string to demonstrate configuration object is populated
      // WriteLine(_configuration.GetConnectionString("DataConnection"));

      try
      {
        Log.Information("Starting service");
        await serviceProvider.GetService<App>().Run();
        Log.Information("Ending service");
      }
      catch (Exception ex)
      {
        Log.Fatal(ex, "Error running service");
        throw ex;
      }
      finally
      {
        Log.CloseAndFlush();
      }
    }

    private static void ConfigureServices(IServiceCollection serviceCollection)
    {
      // Add logging
      serviceCollection.AddSingleton(LoggerFactory.Create(builder =>
      {
        builder.AddSerilog(dispose: true);
      }));

      serviceCollection.AddLogging();

      // Build configuration
      _configuration = new ConfigurationBuilder()
          .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
          .AddJsonFile("appsettings.json", false)
          .Build();

      // Add access to generic IConfigurationRoot
      serviceCollection.AddSingleton(_configuration);

      // Add app
      serviceCollection.AddTransient<App>();
    }
  }
}