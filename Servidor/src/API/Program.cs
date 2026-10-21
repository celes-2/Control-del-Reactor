using API.Configuracion;
using API.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Cada módulo registra sus servicios en su propio método (así Program.cs casi nunca tiene conflictos).
builder.Services.AddPersistenciaJson(builder.Configuration); // usuarios y avatares en archivos JSON
builder.Services.AddPartidas();                              // casos de uso de partidas (en memoria)
builder.Services.AddTiempoReal(builder.Configuration);       // WebSocket y adaptador MQTT (Palma)

var app = builder.Build();

app.UseMiddleware<ManejadorErrores>();
app.UseTiempoReal();                                         // WebSocket /ws/partida (Palma)
app.MapControllers();

app.Run();