using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace FunctionsApp;

public static class Helpers
{
    public static string GenerateMd5Hash(Stream stream)
    {
        using (var alg = MD5.Create())
        {
            var hash = alg.ComputeHash(stream);
            var oldHas = BitConverter.ToString(hash).Replace("-", "").ToLower();
            return string.Concat(hash.Select(b => b.ToString("x2")));
        }
        // var md5hash = new MD5CryptoServiceProvider().ComputeHash(stream);
        // // convert byte array to string
        // return BitConverter.ToString(md5hash).Replace("-", "").ToLower();
    }

    static string GetHashString(this byte[] bytes, HashAlgorithm cryptoProvider)
    {
        byte[] hash = cryptoProvider.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

}
