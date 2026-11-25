using Dominio.Comun;

namespace Dominio.Juego;

/// <summary>
/// Puerta automática (US-20, CA-20.4): abre y cierra sola cada cierto tiempo. Cerrada bloquea como un muro.
/// Nunca se cierra con alguien encima: espera a que la celda quede libre (CA-20.8).
/// </summary>
public sealed class Puerta
{
    public Posicion Posicion { get; }
    public bool Abierta { get; private set; }

    /// <summary>Segundos que faltan para el próximo cambio.</summary>
    public int SegundosParaCambio { get; private set; }

    internal Puerta(Posicion posicion, bool abierta)
    {
        Posicion = posicion;
        Abierta = abierta;
    }

    internal void Reiniciar(bool abierta, int segundosParaCambio)
    {
        Abierta = abierta;
        SegundosParaCambio = segundosParaCambio;
    }

    /// <summary>
    /// Pasa un segundo. Con <paramref name="forzadaAbierta"/> (evento de rutas alternativas) queda abierta.
    /// Devuelve true si cambió de estado.
    /// </summary>
    internal bool AvanzarUnSegundo(int ciclo, bool forzadaAbierta, bool ocupada)
    {
        if (forzadaAbierta)
        {
            var cambio = !Abierta;
            Abierta = true;
            SegundosParaCambio = ciclo;
            return cambio;
        }

        SegundosParaCambio--;
        if (SegundosParaCambio > 0)
            return false;

        if (Abierta && ocupada)
        {
            SegundosParaCambio = 1;   // no se cierra encima de alguien: reintenta el siguiente segundo
            return false;
        }

        Abierta = !Abierta;
        SegundosParaCambio = ciclo;
        return true;
    }
}
