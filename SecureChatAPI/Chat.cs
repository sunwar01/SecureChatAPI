using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SecureChatApi.Services;

namespace SecureChatApi;

public class Chat : Hub
{
        // Store user connections and their public keys
        private static readonly ConcurrentDictionary<string, string> _users = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, byte[]> _publicKeys = new ConcurrentDictionary<string, byte[]>();
        
        // Store signing keys for verification
        private static readonly ConcurrentDictionary<string, byte[]> _signingPublicKeys = new ConcurrentDictionary<string, byte[]>();

        public override async Task OnConnectedAsync()
        {
            // Handle new connection
            await Clients.Caller.SendAsync("RequestUserInfo");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            // Remove user when disconnected
            if (_users.TryRemove(Context.ConnectionId, out string username))
            {
                _publicKeys.TryRemove(username, out _);
                _signingPublicKeys.TryRemove(username, out _);
                await Clients.AllExcept(Context.ConnectionId).SendAsync("UserDisconnected", username);
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        public async Task RegisterUser(string username, string publicKeyBase64, string signingPublicKeyBase64)
        {
            // Register a user with their public keys
            _users[Context.ConnectionId] = username;
            _publicKeys[username] = Convert.FromBase64String(publicKeyBase64);
            _signingPublicKeys[username] = Convert.FromBase64String(signingPublicKeyBase64);
            
            // Notify everyone about connected users
            await Clients.All.SendAsync("UserList", _users.Values);
            
            // Send all public keys to the new user
            foreach (var keyPair in _publicKeys)
            {
                await Clients.Caller.SendAsync("PublicKey", keyPair.Key, Convert.ToBase64String(keyPair.Value));
            }
            
            // Send all signing public keys to the new user
            foreach (var keyPair in _signingPublicKeys)
            {
                await Clients.Caller.SendAsync("SigningPublicKey", keyPair.Key, Convert.ToBase64String(keyPair.Value));
            }
        }

        public async Task SendMessage(string recipientUsername, string encryptedMessageBase64, string hmacBase64, string signatureBase64)
        {
            // Get senders username
            if (!_users.TryGetValue(Context.ConnectionId, out string senderUsername))
                return;
                
            // Verify the senders identity and message integrity
            if (!VerifyMessage(senderUsername, encryptedMessageBase64, hmacBase64, signatureBase64))
            {
                // If verification fails, reject the message and notify the sender
                await Clients.Caller.SendAsync("MessageRejected", "Message verification failed");
                return;
            }
            
            // Send the verified encrypted message to recipient
            await Clients.All.SendAsync("ReceiveMessage", 
                senderUsername, 
                recipientUsername,
                encryptedMessageBase64, 
                hmacBase64, 
                signatureBase64);
                
        
            Console.WriteLine($"Secure message from {senderUsername} to {recipientUsername} verified and forwarded");
        }

        public async Task GetPublicKey(string username)
        {
            // Request for a users public key
            if (_publicKeys.TryGetValue(username, out byte[] publicKey))
            {
                await Clients.Caller.SendAsync("PublicKey", username, Convert.ToBase64String(publicKey));
            }
            
            // Request for a users signing public key
            if (_signingPublicKeys.TryGetValue(username, out byte[] signingPublicKey))
            {
                await Clients.Caller.SendAsync("SigningPublicKey", username, Convert.ToBase64String(signingPublicKey));
            }
        }
        
        
        private bool VerifyMessage(string senderUsername, string encryptedMessageBase64, string hmacBase64, string signatureBase64)
        {
            try
            {
                // Get the senders signing public key
                if (!_signingPublicKeys.TryGetValue(senderUsername, out byte[] signingPublicKey))
                {
                    Console.WriteLine($"No signing public key found for {senderUsername}");
                    return false;
                }
        
     
                Console.WriteLine($"Received signature: {signatureBase64}");
                Console.WriteLine($"Signer username: {senderUsername}");
                Console.WriteLine($"Signing public key exists: {_signingPublicKeys.ContainsKey(senderUsername)}");
        
                // Convert from Base64
                byte[] encryptedMessage = Convert.FromBase64String(encryptedMessageBase64);
                byte[] hmac = Convert.FromBase64String(hmacBase64);
                byte[] signature = Convert.FromBase64String(signatureBase64);
        
     
                Console.WriteLine($"Signature length: {signature.Length}");
                Console.WriteLine($"Public key length: {signingPublicKey.Length}");
        
                // Verify the signature using Ed25519
                bool isSignatureValid = CryptoService.VerifySignature(
                   encryptedMessage, 
                   signature, 
                   signingPublicKey
                );
        
        
                Console.WriteLine($"Signature verification result: {isSignatureValid}");
        
                if (!isSignatureValid)
                {
                 Console.WriteLine($"Signature verification failed for message from {senderUsername}");
                 return false;
                }
        
                // Check if Hmac exists
                if (hmac.Length == 0)
                {
                   Console.WriteLine($"HMAC missing for message from {senderUsername}");
                   return false;
                }
        
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error verifying message: {ex.Message}");
                return false;
            }
        }
        
        // Additional method to handle key rotation if implemented
        public async Task UpdateKeys(string newPublicKeyBase64, string newSigningPublicKeyBase64)
        {
            if (!_users.TryGetValue(Context.ConnectionId, out string username))
                return;
                
            // Update the user's keys
            _publicKeys[username] = Convert.FromBase64String(newPublicKeyBase64);
            _signingPublicKeys[username] = Convert.FromBase64String(newSigningPublicKeyBase64);
            
            // Notify other users of key update
            await Clients.AllExcept(Context.ConnectionId).SendAsync("KeysUpdated", username);
            
            // Send the new keys to everyone
            foreach (var connectionId in _users.Keys)
            {
                if (_users.TryGetValue(connectionId, out string recipient) && recipient != username)
                {
                    await Clients.Client(connectionId).SendAsync("PublicKey", username, newPublicKeyBase64);
                    await Clients.Client(connectionId).SendAsync("SigningPublicKey", username, newSigningPublicKeyBase64);
                }
            }
        }
}