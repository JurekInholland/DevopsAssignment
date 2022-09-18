using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace FunctionsApp;

public static class Helpers
{
    private static readonly MD5 Algo = MD5.Create();

    public static string GenerateMd5Hash(Stream stream)
    {
        var hash = Algo.ComputeHash(stream);
        return string.Concat(hash.Select(b => b.ToString("x2")));
    }
}
