using Microsoft.Extensions.Caching.Memory;
using RabbitMQ.Client;
using Serilog;
using Serilog.Sinks.RabbitMQ.Sinks.RabbitMQ;
using WhatsAppServicesAPI.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddSingleton<LoggingMiddleware>();
builder.Services.AddSingleton<SymmetricEncryptionMiddleware>();

// Configure Serilog
var rabbitMqConfig = GetRabbitMQConfiguration(builder.Services.BuildServiceProvider().GetService<IMemoryCache>(), builder.Configuration);
var connectionFactory = new ConnectionFactory
{
    HostName = rabbitMqConfig.HostName,
    Port = rabbitMqConfig.Port,
    VirtualHost = rabbitMqConfig.VirtualHost,
    UserName = rabbitMqConfig.UserName,
    Password = rabbitMqConfig.Password
};
using var connection = connectionFactory.CreateConnection();
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.RabbitMQ((RabbitMQClientConfiguration)connection, new RabbitMQSinkConfiguration())
    .CreateLogger();


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.UseSerilogRequestLogging();
app.UseMiddleware<LoggingMiddleware>();
app.UseMiddleware<SymmetricEncryptionMiddleware>();

app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});
app.Run();
RabbitMQConfiguration GetRabbitMQConfiguration(IMemoryCache memoryCache, IConfiguration configuration)
{
    return memoryCache.GetOrCreate("RabbitMQConfiguration", entry =>
    {
        entry.SlidingExpiration = TimeSpan.FromMinutes(5);
        var rabbitMqConfig = new RabbitMQConfiguration
        {
            HostName = configuration["RabbitMQ:Hostname"],
            Port = int.Parse(configuration["RabbitMQ:Port"]),
            VirtualHost = configuration["RabbitMQ:VHost"],
            UserName = configuration["RabbitMQ:Username"],
            Password = configuration["RabbitMQ:Password"]
        };
        return rabbitMqConfig;
    });
}



public class RabbitMQConfiguration
{
    public string HostName { get; set; }
    public int Port { get; set; }
    public string VirtualHost { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
}