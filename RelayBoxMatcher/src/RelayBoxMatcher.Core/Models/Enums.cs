using System.Text.Json.Serialization;

namespace RelayBoxMatcher.Core.Models;

/// <summary>
/// Colore dell'etichetta/cappuccio del relè grande (famiglia JIDECO/Mitsuba).
/// I piccoli relè verdi tipo NAIS non fanno parte di questa classificazione.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ColorClass
{
    Sconosciuto = 0,
    Blu,
    Rosa,
    Verde
}

/// <summary>Esito rilevato per uno slot in un'immagine di test.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PresenceStatus
{
    Presente,
    Assente,
    ColoreErrato,
    Incerto
}
