using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.ApplicationInsights;

namespace Spotify.Infrastructure
{
  public static class ExtensionMethods
  {
    /// <summary>
    ///   Permet de récupérer la partie à gauche d'une string recherchée.
    /// </summary>
    /// <param name="source">La string source auquel on récupère le résultat.</param>
    /// <param name="search">La string recherchée parmi la source.</param>
    /// <param name="comparisonType">Option de la comparaison.</param>
    /// <returns>Retourne la partie à gauche de la string recherchée, ou bien sinon la string source.</returns>
    public static string Left(this string source, string search, StringComparison comparisonType = StringComparison.CurrentCulture)
    {
      if (source == null)
      {
        return string.Empty;
      }

      if (string.IsNullOrWhiteSpace(search))
      {
        return source;
      }

      int index = source.IndexOf(search, comparisonType);

      return index == -1 ? source : source.Substring(0, index);
    }

    /// <summary>
    ///   Permet de récupérer la partie à gauche d'une string recherchée.
    /// </summary>
    /// <param name="source">La string source auquel on récupère le résultat.</param>
    /// <param name="search">Le char recherché parmi la source.</param>
    /// <param name="comparisonType">Option de la comparaison.</param>
    /// <returns>Retourne la partie à gauche de la string recherchée, ou bien sinon la string source.</returns>
    public static string Left(this string source, char search, StringComparison comparisonType = StringComparison.CurrentCulture)
    {
      return Left(source, search.ToString(), comparisonType);
    }

    /// <summary>
    ///   Permet de récupérer une string avec un nombre définit de caractères à partir de la gauche.
    /// </summary>
    /// <param name="source">La string source auquel on récupère le résultat.</param>
    /// <param name="numberOfChar">Nombre de caractère à aller chercher.</param>
    /// <returns>Retourne la partie à gauche de la string selon le nombre de caractères spécifiés.</returns>
    public static string Left(this string source, uint numberOfChar)
    {
      if (string.IsNullOrEmpty(source) || numberOfChar == 0)
      {
        return string.Empty;
      }

      return source.Substring(0, (int) Math.Min(source.Length, numberOfChar));
    }

    /// <summary>
    ///   Permet de récupérer la partie à droite d'une string recherchée.
    /// </summary>
    /// <param name="source">La string source auquel on récupère le résultat.</param>
    /// <param name="search">La string recherchée parmi la source.</param>
    /// <param name="comparisonType">Option de la comparaison.</param>
    /// <returns>Retourne la partie à droite de la string recherchée, ou bien sinon la string source.</returns>
    public static string Right(this string source, string search, StringComparison comparisonType = StringComparison.CurrentCulture)
    {
      if (source == null)
      {
        return string.Empty;
      }

      if (string.IsNullOrWhiteSpace(search))
      {
        return source;
      }

      int index = source.LastIndexOf(search, comparisonType);

      return index == -1 ? source : source.Substring(index + search.Length);
    }

    /// <summary>
    ///   Permet de récupérer la partie à droite d'une string recherchée.
    /// </summary>
    /// <param name="source">La string source auquel on récupère le résultat.</param>
    /// <param name="search">Le char recherché parmi la source.</param>
    /// <param name="comparisonType">Option de la comparaison.</param>
    /// <returns>Retourne la partie à droite de la string recherchée, ou bien sinon la string source.</returns>
    public static string Right(this string source, char search, StringComparison comparisonType = StringComparison.CurrentCulture)
    {
      return Right(source, search.ToString(), comparisonType);
    }

    /// <summary>
    ///   Permet de récupérer une string avec un nombre définit de caractères à partir de la droite.
    /// </summary>
    /// <param name="source">La string source auquel on récupère le résultat.</param>
    /// <param name="numberOfChar">Nombre de caractère à aller chercher.</param>
    /// <returns>Retourne la partie à droite de la string selon le nombre de caractères spécifiés.</returns>
    public static string Right(this string source, uint numberOfChar)
    {
      if (string.IsNullOrEmpty(source) || numberOfChar == 0)
      {
        return string.Empty;
      }

      if (numberOfChar >= source.Length)
      {
        return source;
      }

      return source.Substring((int) (source.Length - numberOfChar), (int) numberOfChar);
    }

    public static void TrackEvent(this TelemetryClient client, string name, params (string Key, string Value)[] properties)
    {
      client.TrackEvent(name, properties.ToDictionary(x => x.Key, x => x.Value));
    }

    public static void TrackMethod(this TelemetryClient client, [CallerMemberName] string method = null)
    {
      client.TrackEvent(method);
    }

    public static void Track(this Request request, bool isSuccess = true, string code = null)
    {
      SpotifyPlugin.Telemetry.TrackRequest(request.Name, request.StartTime, DateTimeOffset.UtcNow - request.StartTime, code, isSuccess);
    }
  }
}