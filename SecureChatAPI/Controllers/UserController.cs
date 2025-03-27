using Microsoft.AspNetCore.Mvc;
using SecureChatApi.Services;

namespace SecureChatApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly CryptoService _cryptoService;
        
    public UserController(CryptoService cryptoService)
    {
        _cryptoService = cryptoService;
    }
        
    
    // User Creation for real use
    
}


