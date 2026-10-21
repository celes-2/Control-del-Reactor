namespace Aplicacion;

/// <summary>Se buscó algo que no existe (una partida, un usuario). La API lo traduce a 404.</summary>
public class NoEncontradoException(string mensaje) : Exception(mensaje);
