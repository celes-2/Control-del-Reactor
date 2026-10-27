namespace Reactor.Cliente.Servicios
{
    /// <summary>Una partida en espera a la que se puede unir (una línea de GET /api/partidas).</summary>
    public sealed class PartidaAbierta
    {
        public string Id { get; set; }
        public string Anfitrion { get; set; }
        public int Jugadores { get; set; }
        public int MaxJugadores { get; set; }

        public bool EstaLlena => Jugadores >= MaxJugadores;
    }
}
