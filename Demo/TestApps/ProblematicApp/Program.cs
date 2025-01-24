using PerformanceIssues.Serivces;
using PerformanceIssues.Services;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
// builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<ILeakyCache, LeakyCache>();
builder.Services.AddSingleton<IEventManager, EventManager>();
builder.Services.AddSingleton<DataGenerator>();
builder.Services.AddSingleton<CPUTaskManager>();

var app = builder.Build();
// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
//     app.MapOpenApi();
// }

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();