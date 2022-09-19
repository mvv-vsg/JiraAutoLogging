using JiraAutoLogging.Config;
using JiraAutoLogging.Service;
using JiraAutoLogging.Worker;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFile(builder.Configuration["LogFile"]);

// Add services to the container.
builder.Services.AddSingleton(builder.Configuration.GetSection("TimeDistribution").Get<TimeDistributionConfig>());
builder.Services.AddSingleton(builder.Configuration.GetSection("Services").Get<ServicesConfig>());
builder.Services.AddSingleton(builder.Configuration.GetSection("TimeLogging").Get<TimeLoggingConfig>());
builder.Services.AddTransient<WorkerService>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHostedService<Worker>();

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

app.Run();