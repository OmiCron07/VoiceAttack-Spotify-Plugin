using System;

namespace Spotify.Infrastructure
{
  public class VaProxy
  {
    private readonly dynamic _vaProxy;


    public VaProxy(dynamic vaProxy)
    {
      _vaProxy = vaProxy;
    }


    public string Context => _vaProxy.Context;


    public string GetText(string name)
    {
      return _vaProxy.GetText(name);
    }

    public void SetText(string name, string value)
    {
      _vaProxy.SetText(name, value);
    }

    public int? GetInt(string name)
    {
      return _vaProxy.GetInt(name);
    }

    public void SetInt(string name, int? value)
    {
      _vaProxy.SetInt(name, value);
    }

    public bool? GetBoolean(string name)
    {
      return _vaProxy.GetBoolean(name);
    }

    public void SetBoolean(string name, bool? value)
    {
      _vaProxy.SetBoolean(name, value);
    }

    public DateTime? GetDate(string name)
    {
      return _vaProxy.GetDate(name);
    }

    public void WriteToLog(string message, ColorEnum color = ColorEnum.Blank)
    {
      _vaProxy.WriteToLog(message, color.ToString());
    }

    public Version Version()
    {
      return _vaProxy.VAVersion;
    }
  }


  public enum ColorEnum
  {
    Blank,
    Black,
    Red,
    Blue,
    Green,
    Yellow,
    Orange,
    Purple,
    Gray
  }
}
