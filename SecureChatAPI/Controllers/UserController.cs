using Microsoft.AspNetCore.Mvc;
using SecureChatApi.Services;

namespace SecureChatApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly CryptoService _cryptoService;
        
    public ChatController(CryptoService cryptoService)
    {
        _cryptoService = cryptoService;
    }
        
    
}


