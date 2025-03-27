using System.Security.Cryptography;
using System.Text;


namespace SecureChatApi.Services;

      public class CryptoService
    {
        public static bool VerifySignature(byte[] message, byte[] signature, byte[] publicKey)
        {
            var algorithm = new NSec.Cryptography.Ed25519();
            
            // Import the public key
            var publicKeyObject = NSec.Cryptography.PublicKey.Import(
                algorithm, 
                publicKey, 
                NSec.Cryptography.KeyBlobFormat.RawPublicKey);
            
            // Verify the signature
            return algorithm.Verify(publicKeyObject, message, signature);
        }
    
}