using Dominio.Juego;

namespace Infraestructura.Hardware;

/// <summary>Un botón presionado en un control, ya traducido al lenguaje del juego.</summary>
public sealed record EventoBoton(string DeviceId, Accion Accion, int Seq);