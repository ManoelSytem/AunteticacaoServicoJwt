using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ServAutenJwt.DTOs;
using ServAutenJwt.Interface;
using ServAutenJwt.Model;

namespace ServAutenJwt.Controllers
{
    [Route("api/[Controller]/")]
    [ApiController]
    public class AutorizaController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly IUser _user;

        public AutorizaController(UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager, IConfiguration configuration, IUser user)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _user = user;
        }

        [HttpGet("Authenticated")]
        public UsuarioDTO Get(string login, string password)
        {
            var result = new UsuarioDTO();
            using (var context = new Context.AppContext())
            {
                result = context.UsuarioDTO.Where(user => user.Email == login && user.Password == password && user.IsAuthenticated == true).FirstOrDefault();
            }
            
            return result;
        }
        
        [HttpPost("register")]
        public async Task<ActionResult> RegisterUser([FromBody] UsuarioDTO model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.Values.SelectMany(e => e.Errors));
            }

            var user = new IdentityUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);


            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            var context = new Context.AppContext();
            var usuario = new UsuarioDTO()
            {
                Email = model.Email,
                Password = model.Password,
                IsAuthenticated = true
            };

            context.UsuarioDTO.Add(usuario);
            context.SaveChanges();

            return Ok(GeraToken(model));
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] UsuarioDTO userInfo)
        {
            //verifica se o modelo é válido
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.Values.SelectMany(e => e.Errors));
            }
            
            var context = new Context.AppContext();

            var result = from usuario in context.UsuarioDTO
                         where usuario.Email == userInfo.Email && usuario.Password == userInfo.Password
                         select new UsuarioDTO
                         {
                             Email = usuario.Email,
                             Password = usuario.Password,
                             IsAuthenticated = true
                         };

            var resultUser = result.Where(user => user.Email == userInfo.Email && user.Password == userInfo.Password).FirstOrDefault();

            if (resultUser != null)
            {
                context.UsuarioDTO
                .Where(user => user.Email == resultUser.Email)
                .ToList()
                .ForEach(x => x.IsAuthenticated = true);
                context.SaveChanges();
                return Ok(GeraToken(userInfo));
            }
            else
            {
                var resultApi = new ResultApi
                {
                    code = "Login",
                    description = "Login Inválido...."
                };
                return BadRequest(resultApi);
            }
        }

        private UsuarioToken GeraToken(UsuarioDTO userInfo)
        {
            //define declarações do usuário
            var claims = new[]
            {
                 new Claim(JwtRegisteredClaimNames.UniqueName, userInfo.Email),
                 new Claim("SoftwMicro", "softwmicro"),
                 new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
             };

            //gera uma chave com base em um algoritmo simetrico
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:key"]));
            //gera a assinatura digital do token usando o algoritmo Hmac e a chave privada
            var credenciais = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            //Tempo de expiracão do token.
            var expiracao = _configuration["TokenConfiguration:ExpireHours"];
            var expiration = DateTime.UtcNow.AddHours(double.Parse(expiracao));

            // classe que representa um token JWT e gera o token
            JwtSecurityToken token = new JwtSecurityToken(
              issuer: _configuration["TokenConfiguration:Issuer"],
              audience: _configuration["TokenConfiguration:Audience"],
              claims: claims,
              expires: expiration,
              signingCredentials: credenciais);

            //retorna os dados com o token e informacoes
            return new UsuarioToken()
            {
                Authenticated = true,
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                Expiration = expiration,
                Message = "Token JWT OK"
            };
        }
        
        [HttpPost("logout")]
        public async Task Logout()
        {
            await _signInManager.SignOutAsync();
        }
    }


}
