using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
namespace WhatsAppServicesAPI.Middlewares
{
    public class SymmetricEncryptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly SymmetricEncryptionConfiguration _encryptionConfig;

       public SymmetricEncryptionMiddleware(RequestDelegate next, IMemoryCache memoryCache, IConfiguration configuration)
{
    _next = next;
    _encryptionConfig = GetSymmetricEncryptionConfiguration(memoryCache, configuration);
}

        public async Task InvokeAsync(HttpContext context)
        {
            // Decrypt request
            context.Request.Body = await DecryptStreamAsync(context.Request.Body);

            // Replace response body stream to capture the data for encryption
            var originalResponseBodyStream = context.Response.Body;
            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            await _next(context);

            // Encrypt response
            context.Response.Body = originalResponseBodyStream;
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            await EncryptStreamAsync(responseBodyStream, context.Response.Body);
        }

        private async Task<Stream> DecryptStreamAsync(Stream inputStream)
        {
            // Decrypt the input stream
            var decryptedStream = new MemoryStream();
            using (var aes = Aes.Create())
            {
                aes.Key = _encryptionConfig.Key;
                aes.IV = _encryptionConfig.IV;
                var cryptoTransform = aes.CreateDecryptor();
                using var cryptoStream = new CryptoStream(inputStream, cryptoTransform, CryptoStreamMode.Read);
                await cryptoStream.CopyToAsync(decryptedStream);
            }
            decryptedStream.Seek(0, SeekOrigin.Begin);
            return decryptedStream;
        }

        private async Task EncryptStreamAsync(Stream inputStream, Stream outputStream)
        {
            // Encrypt the input stream and write to the output stream
            using (var aes = Aes.Create())
            {
                aes.Key = _encryptionConfig.Key;
                aes.IV = _encryptionConfig.IV;
                var cryptoTransform = aes.CreateEncryptor();
                using var cryptoStream = new CryptoStream(outputStream, cryptoTransform, CryptoStreamMode.Write);
                await inputStream.CopyToAsync(cryptoStream);
            }
        }

        private SymmetricEncryptionConfiguration GetSymmetricEncryptionConfiguration(IMemoryCache memoryCache, IConfiguration configuration)
        {
            return memoryCache.GetOrCreate("SymmetricEncryptionConfiguration", entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                var encryptionConfig = new SymmetricEncryptionConfiguration
                {
                    Key = Convert.FromBase64String(configuration["SymmetricEncryption:Key"]),
                    IV = Convert.FromBase64String(configuration["SymmetricEncryption:IV"])
                };
                return encryptionConfig;
            });
        }
    }

    public class SymmetricEncryptionConfiguration
    {
        public byte[] Key { get; set; }
        public byte[] IV { get; set; }
    }
}
