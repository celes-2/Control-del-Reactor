using API.Configuracion;
using API.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Cada módulo registra sus servicios en su propio método (así Program.cs casi nunca tiene conflictos).
builder.Services.AddPersistenciaJson(builder.Configuration); // usuarios y avatares en archivos JSON
builder.Services.AddPartidas();                              // casos de uso de partidas (en memoria)
builder.Services.AddTiempoReal(builder.Configuration);       // WebSocket y adaptador MQTT (Palma)
builder.Services.AddRegistroUsuarios(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddAutenticacionJwt();
builder.Services.AddLoginSocial(builder.Configuration);       // US-29 login con GitHub (Palma)
builder.Services.AddHistorialPerfil(builder.Configuration);   // US-04

var app = builder.Build();

app.UseMiddleware<ManejadorErrores>();
app.UseDefaultFiles();                                       // US-30: /movil/ entrega movil/index.html
app.UseStaticFiles();                                        // US-30: página móvil (wwwroot/movil)
app.UseAuthentication();
app.UseAuthorization();
app.UseTiempoReal();                                         // WebSocket /ws/partida (Palma)
app.MapControllers();

app.Run();