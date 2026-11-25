namespace Contratos.Auth;

/// <summary>Respuesta de POST /api/auth/github/iniciar: Unity muestra UserCode y abre VerificationUri.</summary>
public sealed record GitHubInicioResponse(
    string DeviceCode, string UserCode, string VerificationUri, int ExpiresIn, int Interval);

/// <summary>Cuerpo de POST /api/auth/github/consultar.</summary>
public sealed record GitHubConsultaRequest(string DeviceCode);

/// <summary>
/// Respuesta de POST /api/auth/github/consultar.
/// Estado "PENDIENTE": el jugador todavia no autoriza; volver a preguntar en Interval segundos (si viene).
/// Estado "COMPLETADO": Login trae el JWT propio y el usuario, igual que POST /api/auth/login.
/// Cancelacion, expiracion o fallo llegan como error 400 con el formato { error, codigo, mensaje }.
/// </summary>
public sealed record GitHubConsultaResponse(string Estado, int? Interval, LoginResponse? Login);
