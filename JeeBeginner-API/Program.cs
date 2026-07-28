using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace JeeBeginner
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // IIS Express may start from bin\Debug instead of the project folder.
            // Traverse upward so the .env beside the project can still be found.
            DotNetEnv.Env.TraversePath().Load();

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
