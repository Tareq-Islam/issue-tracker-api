﻿using Application.Abstractions.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Application.Features.AuthFeature;

public class UserLoginQuery : IQuery<IApiResult>
{
    public string LoginName { get; set; }
    public string Password { get; set; } = string.Empty;
    internal class UserLoginQueryHandler : IRequestHandler<UserLoginQuery, IApiResult>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJwtService _jwtService;
        public UserLoginQueryHandler(IUnitOfWork unitOfWork, IJwtService jwtService)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
        }

        public async Task<IApiResult> Handle(UserLoginQuery request, CancellationToken cancellationToken)
        {
            var currentUser = await _unitOfWork.User.Queryable
                .Where(x => x.LoginName.Trim().ToLower() == request.LoginName.ToLower().Trim()).Select(x => new
                {                   
                    x.UserName,
                    x.RoleId,
                    x.Id,
                    x.Role.RoleName,
                    x.PasswordHash,
                    x.PasswordSalt,
                    x.Role.RoleType
                })
                .FirstOrDefaultAsync();

            bool isInvalidUser = currentUser is null;
            if (isInvalidUser)
            {
                return ApiResult.Fail("Invalid Username or Password");
            }

            bool isPasswordValid = _jwtService.IsPasswordVerified(request.Password, currentUser.PasswordHash, currentUser.PasswordSalt);
            if (!isPasswordValid)
            {
                return ApiResult.Fail("Invalid Username or Password");
            }

            var exp = DateTime.Now.AddHours(6);
            var tokenExpiredTime = DateTimeOffset.Parse(exp.ToString()).ToUnixTimeSeconds();

            var claims = new List<Claim>
            {
                new Claim("uid", currentUser.Id.ToString()),
                new Claim("rid", currentUser.RoleId.ToString()),
                new Claim("exp", tokenExpiredTime.ToString())
            };

            var token = _jwtService.BuildToken(claims, exp);

            var response = new
            {
                Token = token,  
                Payload = new
                {
                    rid = currentUser.RoleId,
                    currentUser.UserName,
                    uid = currentUser.Id,                    
                    currentUser.RoleName,
                    currentUser.RoleType
                }
            };

            return ApiResult<dynamic>.Success(response);
        }
    }
}
