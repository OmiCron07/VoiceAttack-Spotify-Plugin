using System;
using System.Runtime.CompilerServices;

namespace Spotify.Infrastructure
{
  public class Request
  {
    public Request([CallerMemberName] string name = null)
    {
      Name      = name;
      StartTime = DateTimeOffset.UtcNow;
    }


    public string Name { get; }

    public DateTimeOffset StartTime { get; }
  }
}