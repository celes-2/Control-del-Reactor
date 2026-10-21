using System;

namespace Reactor.Cliente.Servicios
{
    /// <summary>Cómo termina la partida (CA-08.4).</summary>
    public enum ModoFin
    {
        Tiempo,
        Puntos,
    }

    /// <summary>
    /// Parámetros que el jugador elige en la pantalla de configuración (US-08).
    /// Solo guarda y limita valores: las reglas de verdad las valida el servidor.
    /// </summary>
    public sealed class AjustesPartida
    {
        public const int MinSegundos = 60, MaxSegundos = 600, PasoSegundos = 30;
        public const int MinPuntos = 5, MaxPuntos = 100, PasoPuntos = 5;
        public const int MinCongelamiento = 1, MaxCongelamiento = 10;
        public const int MinJugadores = 2, MaxJugadoresPermitidos = 4;

        public ModoFin Modo { get; set; } = ModoFin.Tiempo;
        public int DuracionSegundos { get; set; } = 180;
        public int PuntosObjetivo { get; set; } = 20;
        public int SegundosCongelamiento { get; set; } = 3;
        public int MaxJugadores { get; set; } = 4;

        public AjustesPartida Copia() => (AjustesPartida)MemberwiseClone();

        public void CambiarValor(int direccion)
        {
            if (Modo == ModoFin.Tiempo)
                DuracionSegundos = Limitar(DuracionSegundos + direccion * PasoSegundos, MinSegundos, MaxSegundos);
            else
                PuntosObjetivo = Limitar(PuntosObjetivo + direccion * PasoPuntos, MinPuntos, MaxPuntos);
        }

        public void CambiarCongelamiento(int direccion) =>
            SegundosCongelamiento = Limitar(SegundosCongelamiento + direccion, MinCongelamiento, MaxCongelamiento);

        public void CambiarJugadores(int direccion) =>
            MaxJugadores = Limitar(MaxJugadores + direccion, MinJugadores, MaxJugadoresPermitidos);

        /// <summary>Corrige valores fuera de rango (por ejemplo, de un valor guardado viejo).</summary>
        public void Normalizar()
        {
            DuracionSegundos = Limitar(DuracionSegundos, MinSegundos, MaxSegundos);
            PuntosObjetivo = Limitar(PuntosObjetivo, MinPuntos, MaxPuntos);
            SegundosCongelamiento = Limitar(SegundosCongelamiento, MinCongelamiento, MaxCongelamiento);
            MaxJugadores = Limitar(MaxJugadores, MinJugadores, MaxJugadoresPermitidos);
            if (!Enum.IsDefined(typeof(ModoFin), Modo)) Modo = ModoFin.Tiempo;
        }

        static int Limitar(int valor, int minimo, int maximo) => Math.Max(minimo, Math.Min(maximo, valor));
    }
}
