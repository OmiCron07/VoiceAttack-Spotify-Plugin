using SpotifyAPI.Web;

namespace Spotify.Infrastructure
{
  public class UserProfile
  {
    public string Id { get; }

    public string Country { get; }

    public ProductEnum Product { get; }


    public UserProfile(PrivateUser userProfile)
    {
      Id      = userProfile.Id;
      Country = userProfile.Country;

      switch (userProfile.Product.ToLower())
      {
        case "free":
        case "open":
          Product = ProductEnum.Free;

          break;

        case "premium":
          Product = ProductEnum.Premium;

          break;
      }
    }
  }
}