using JeeBeginner.Reponsitories.AccountManagement;
using JeeBeginner.Reponsitories.Authorization;
using JeeBeginner.Reponsitories.CustomerManagement;
using JeeBeginner.Reponsitories.PartnerManagement;
using JeeBeginner.Reponsitories.NhanVienManagement;
using JeeBeginner.Classes;
using JeeBeginner.Services;
using JeeBeginner.Services.AccountManagement;
using JeeBeginner.Services.Authorization;
using JeeBeginner.Services.CustomerManagement;
using JeeBeginner.Services.Encryption;
using JeeBeginner.Services.PartnerManagement;
using JeeBeginner.Services.NhanVienManagement;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Text;

namespace JeeBeginner
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(o => o.AddPolicy("AllowOrigin", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            }));

            services.AddControllers().AddNewtonsoftJson();
            services.AddControllersWithViews().AddNewtonsoftJson();
            services.AddRazorPages().AddNewtonsoftJson();
            services.AddControllers().AddNewtonsoftJson(options => { options.SerializerSettings.ContractResolver = new DefaultContractResolver(); });

            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                //options.RequireHttpsMetadata = false;
                //options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["JWT:Secret"])),
                };
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        // Trả cùng định dạng lỗi mà các API quản lý đang dùng.
                        context.HandleResponse();
                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json; charset=utf-8";
                        string response = JsonConvert.SerializeObject(JsonResultCommon.BatBuoc("Đăng nhập"));
                        await context.Response.WriteAsync(response);
                    }
                };
            });

            //Swagger
            services.AddSwaggerGen(c =>
            {
                var securityScheme = new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Description = "Dán JWT token vào đây (không cần gõ Bearer).",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Reference = new OpenApiReference
                    {
                        Id = "Bearer",
                        Type = ReferenceType.SecurityScheme
                    }
                };
                c.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                                        {
                                            {securityScheme, new string[] { }}
                                        });
            });

            services.AddMvc().ConfigureApiBehaviorOptions(options =>
            {
                options.SuppressModelStateInvalidFilter = true;
            });
            services.AddOptions();
            #region add Repository
            services.AddTransient<IAccountManagementRepository, AccountManagementRepository>();
            services.AddTransient<IPartnerManagementRepository, PartnerManagementRepository>();
            services.AddTransient<IAuthorizationRepository, AuthorizationRepository>();
            services.AddTransient<ICustomerManagementRepository, CustomerManagementRepository>();
            services.AddTransient<INhanVienManagementRepository, NhanVienManagementRepository>();
            #endregion add Repository
            #region add service
            services.AddTransient<IPartnerManagementService, PartnerManagementService>();
            services.AddTransient<IAccountManagementService, AccountManagementService>();
            services.AddTransient<ICustomAuthorizationService, CustomAuthorizationService>();
            services.AddTransient<ICustomerManagementService, CustomerManagementService>();
            services.AddTransient<INhanVienManagementService, NhanVienManagementService>();
            services.AddTransient<IEncryptionService, EncryptionService>();
            #endregion add service
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "JeeBeginner v1"));
            }

            //app.UseHttpsRedirection();

            app.UseRouting();

            app.UseCors("AllowOrigin");

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseStaticFiles();// For the wwwroot folder

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
