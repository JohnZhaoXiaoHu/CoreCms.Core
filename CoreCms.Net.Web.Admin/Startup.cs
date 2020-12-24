using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Extras.DynamicProxy;
using AutoMapper;
using CoreCms.Net.Auth;
using CoreCms.Net.CodeGenerator.Repository;
using CoreCms.Net.CodeGenerator.Services;
using CoreCms.Net.Configuration;
using CoreCms.Net.Core.AOP;
using CoreCms.Net.Core.Extensions;
using CoreCms.Net.Filter;
using CoreCms.Net.Loging;
using CoreCms.Net.Mapping;
using CoreCms.Net.Middlewares;
using CoreCms.Net.Model.ViewModels.Options;
using CoreCms.Net.Model.ViewModels.Sms;
using CoreCms.Net.Swagger;
using CoreCms.Net.Task;
using CoreCms.Net.Utility.Extensions;
using CoreCms.Net.WeChatService.CustomMessageHandler;
using CoreCms.Net.WeChatService.MessageHandlers.WebSocket;
using CoreCms.Net.WeChatService.WxOpenMessageHandler;
using Essensoft.AspNetCore.Payment.Alipay;
using Essensoft.AspNetCore.Payment.WeChatPay;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Dashboard.BasicAuthorization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Senparc.CO2NET;
using Senparc.CO2NET.AspNet;
using Senparc.NeuChar.MessageHandlers;
using Senparc.WebSocket;
using Senparc.Weixin;
using Senparc.Weixin.Entities;
using Senparc.Weixin.MP;
using Senparc.Weixin.MP.MessageHandlers.Middleware;
using Senparc.Weixin.RegisterServices;
using Senparc.Weixin.WxOpen;
using Senparc.Weixin.WxOpen.MessageHandlers.Middleware;

namespace CoreCms.Net.Web.Admin
{
    /// <summary>
    ///     ��������
    /// </summary>
    public class Startup
    {
        /// <summary>
        ///     ���캯��
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="env"></param>
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            Env = env;
        }

        /// <summary>
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// </summary>
        public IWebHostEnvironment Env { get; }

        /// <summary>
        ///     This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services"></param>
        public void ConfigureServices(IServiceCollection services)
        {
            //��ӱ���·����ȡ֧��
            services.AddSingleton(new AppSettingsHelper(Env.ContentRootPath));
            services.AddSingleton(new LogLockHelper(Env.ContentRootPath));

            //����ڴ滺��ע��
            services.AddMemoryCache();
            //������ݿ�����SqlSugarע��֧��
            services.AddSqlSugarSetup();
            //���ÿ���CORS��
            services.AddCorsSetup();

            //���session֧��(session������cache���д洢)
            services.AddSession();
            // AutoMapper֧��
            services.AddAutoMapper(typeof(AutoMapperConfiguration));

            //ʹ�� SignalR
            services.AddSignalR();

            // ����Payment ����ע��(֧����֧��/΢��֧��)
            services.AddAlipay();
            services.AddWeChatPay();

            // �� appsettings.json �� ����ѡ��
            services.Configure<WeChatPayOptions>(Configuration.GetSection("WeChatPay"));
            services.Configure<AlipayOptions>(Configuration.GetSection("Alipay"));

            //ע�븽���洢����
            services.Configure<FilesStorageOptions>(Configuration.GetSection("FilesStorage"));

            //Swagger�ӿ��ĵ�ע��
            services.AddAdminSwaggerSetup();

            //jwt��Ȩ֧��ע��
            services.AddAuthorizationSetupForAdmin();
            //������ע��
            services.AddHttpContextSetup();


            //΢��ע��
            services.AddSenparcWeixinServices(Configuration) //Senparc.Weixin ע�ᣨ���룩
                .AddSenparcWebSocket<CustomNetCoreWebSocketMessageHandler>(); //Senparc.WebSocket ע�ᣨ���裩

            //���������м���AutoFac�������滻����
            services.Replace(ServiceDescriptor.Transient<IControllerActivator, ServiceBasedControllerActivator>());

            //ע��mvc��ע��razor������ͼ
            services.AddControllersWithViews(options =>
                {
                    //ʵ����֤
                    options.Filters.Add<RequiredErrorForAdmin>();
                    //�쳣����
                    options.Filters.Add<GlobalExceptionsFilterForAdmin>();
                    //Swagger�޳�����Ҫ����apiչʾ���б�
                    options.Conventions.Add(new ApiExplorerIgnores());
                })
                .AddNewtonsoftJson(p =>
                {
                    //����ѭ������
                    p.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                    //��ʹ���շ���ʽ��key
                    p.SerializerSettings.ContractResolver = new DefaultContractResolver();
                    //����ʱ���ʽ
                    p.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
                }).AddRazorRuntimeCompilation();

            //����ͨ��������
            services.Configure<KxtSmsOptions>(Configuration.GetSection("KXTSMS"));

            //ע��Hangfire��ʱ����
            var isEnabledRedis = AppSettingsHelper.GetContent("RedisCachingConfig", "Enabled").ObjectToBool();
            if (isEnabledRedis)
            {
                services.AddHangfire(x =>
                    x.UseRedisStorage(AppSettingsHelper.GetContent("RedisCachingConfig", "ConnectionString")));
            }
            else
            {
                services.AddHangfire(x =>
                    x.UseSqlServerStorage(AppSettingsHelper.GetContent("ConnectionStrings", "SqlServerConnection")));
            }

        }

        /// <summary>
        ///     Autofac���񹤳�
        /// </summary>
        /// <param name="builder"></param>
        public void ConfigureContainer(ContainerBuilder builder)
        {
            //��ȡ���п��������Ͳ�ʹ������ע��
            var controllerBaseType = typeof(ControllerBase);
            builder.RegisterAssemblyTypes(typeof(Program).Assembly)
                .Where(t => controllerBaseType.IsAssignableFrom(t) && t != controllerBaseType)
                .PropertiesAutowired();

            var basePath = ApplicationEnvironment.ApplicationBasePath;

            #region ���нӿڲ�ķ���ע��

            var servicesDllFile = Path.Combine(basePath, "CoreCms.Net.Services.dll");
            var repositoryDllFile = Path.Combine(basePath, "CoreCms.Net.Repository.dll");

            if (!(File.Exists(servicesDllFile) && File.Exists(repositoryDllFile)))
            {
                var msg = "Repository.dll��Services.dll ��ʧ����Ϊ��Ŀ�����ˣ�������Ҫ��F6���룬��F5���У����� bin �ļ��У���������";
                throw new Exception(msg);
            }

            // AOP ���أ������Ҫ��ָ���Ĺ��ܣ�ֻ��Ҫ�� appsettigns.json ��Ӧ��Ӧ true ���С�
            var cacheType = new List<Type>();
            if (AppSettingsHelper.GetContent("RedisCachingConfig", "Enabled").ObjectToBool())
            {
                builder.RegisterType<RedisCacheAop>();
                cacheType.Add(typeof(RedisCacheAop));
            }
            else
            {
                builder.RegisterType<MemoryCacheAop>();
                cacheType.Add(typeof(MemoryCacheAop));
            }

            if (AppSettingsHelper.GetContent("TranAOP", "Enabled").ObjectToBool())
            {
                builder.RegisterType<TranAop>();
                cacheType.Add(typeof(TranAop));
            }

            // ��ȡ Service.dll ���򼯷��񣬲�ע��
            var assemblysServices = Assembly.LoadFrom(servicesDllFile);
            //֧������ע�������ظ�
            builder.RegisterAssemblyTypes(assemblysServices).AsImplementedInterfaces().InstancePerDependency()
                .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);

            // ��ȡ Repository.dll ���򼯷��񣬲�ע��
            var assemblysRepository = Assembly.LoadFrom(repositoryDllFile);
            //֧������ע�������ظ�
            builder.RegisterAssemblyTypes(assemblysRepository).AsImplementedInterfaces().InstancePerDependency()
                .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);

            #endregion

            #region ����ע��һ�����нӿڵ��࣬����interface��������

            //���������������ӿ�
            builder.RegisterType<CodeGeneratorRepository>().As<ICodeGeneratorRepository>().AsImplementedInterfaces()
                .EnableInterfaceInterceptors();
            builder.RegisterType<CodeGeneratorServices>().As<ICodeGeneratorServices>().AsImplementedInterfaces()
                .EnableInterfaceInterceptors();

            #endregion
        }

        /// <summary>
        ///     This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="env"></param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IOptions<SenparcSetting> senparcSetting,
            IOptions<SenparcWeixinSetting> senparcWeixinSetting)
        {
            // ��¼�����뷵������  (ע�⿪��Ȩ�ޣ���Ȼ�����޷�д��)
            app.UseReuestResponseLog();
            // ��¼ip���� (ע�⿪��Ȩ�ޣ���Ȼ�����޷�д��)
            app.UseIpLogMildd();
            // signalr
            app.UseSignalRSendMildd();

            //ǿ����ʾ����
            //System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("zh-CN");
            var supportedCultures = new[]
            {
              new System.Globalization.CultureInfo("zh-CN"),
              //new CultureInfo("en-US")
            };

            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture("zh-CN"),
                SupportedCultures = supportedCultures,
                SupportedUICultures = supportedCultures,
                RequestCultureProviders = new List<IRequestCultureProvider>
                {
                    new QueryStringRequestCultureProvider(),
                    new CookieRequestCultureProvider(),
                    new AcceptLanguageHeaderRequestCultureProvider()
                }
            });

            #region Hangfire��ʱ����

            var queues = new string[] { GlobalEnumVars.HangFireQueuesConfig.@default.ToString(), GlobalEnumVars.HangFireQueuesConfig.apis.ToString(), GlobalEnumVars.HangFireQueuesConfig.web.ToString(), GlobalEnumVars.HangFireQueuesConfig.recurring.ToString() };
            app.UseHangfireServer(new BackgroundJobServerOptions
            {
                ServerTimeout = TimeSpan.FromMinutes(4),
                SchedulePollingInterval = TimeSpan.FromSeconds(15),//�뼶������Ҫ���ö̵㣬һ�������������Ĭ��ʱ�䣬Ĭ��15��
                ShutdownTimeout = TimeSpan.FromMinutes(30),//��ʱʱ��
                Queues = queues,//����
                WorkerCount = Math.Max(Environment.ProcessorCount, 20)//�����߳�������ǰ���������̣߳�Ĭ��20
            });

            //��Ȩ
            var filter = new BasicAuthAuthorizationFilter(
                new BasicAuthAuthorizationFilterOptions
                {
                    SslRedirect = false,
                    // Require secure connection for dashboard
                    RequireSsl = false,
                    // Case sensitive login checking
                    LoginCaseSensitive = false,
                    // Users
                    Users = new[]
                    {
                        new BasicAuthAuthorizationUser
                        {
                            Login = AppSettingsHelper.GetContent("HangFire", "Login"),
                            PasswordClear = AppSettingsHelper.GetContent("HangFire", "PassWord")
                        }
                    }
                });
            var options = new DashboardOptions
            {
                AppPath = "/",//����ʱ��ת�ĵ�ַ
                DisplayStorageConnectionString = false,//�Ƿ���ʾ���ݿ�������Ϣ
                Authorization = new[] { filter },
                IsReadOnlyFunc = Context =>
                {
                    return false;//�Ƿ�ֻ�����
                }
            };

            app.UseHangfireDashboard("/job", options); //���Ըı�Dashboard��url
            HangfireDispose.HangfireService();

            #endregion

            app.UseSwagger().UseSwaggerUI(c =>
            {
                //���ݰ汾���Ƶ��� ����չʾ
                typeof(CustomApiVersion.ApiVersions).GetEnumNames().OrderByDescending(e => e).ToList().ForEach(
                    version =>
                    {
                        c.SwaggerEndpoint($"/swagger/{version}/swagger.json", $"Doc {version}");
                    });
                c.RoutePrefix = "doc";
            });

            //ʹ�� Session
            app.UseSession();

            if (env.IsDevelopment())
            {
                // �ڿ��������У�ʹ���쳣ҳ�棬�������Ա�¶�����ջ��Ϣ�����Բ�Ҫ��������������
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }



            //���ÿ���CORS��
            app.UseCors("cors");
            // ʹ��cookie
            app.UseCookiePolicy();
            // ���ش�����
            app.UseStatusCodePages();
            // Routing
            app.UseRouting();

            // ���� CO2NET ȫ��ע�ᣬ���룡
            app.UseSenparcGlobal(env, senparcSetting.Value, globalRegister => { }, true)
                .UseSenparcWeixin(senparcWeixinSetting.Value,
                    weixinRegister =>
                    {
                        weixinRegister.RegisterMpAccount(senparcWeixinSetting.Value, "���ں�")
                            .RegisterWxOpenAccount(senparcWeixinSetting.Value, "����");
                    });
            //ʹ�� ���ں� MessageHandler �м��
            app.UseMessageHandlerForMp("/WeixinAsync", CustomMessageHandler.GenerateMessageHandler, options =>
            {
                options.AccountSettingFunc = context => senparcWeixinSetting.Value;
                //�� MessageHandler ���첽����δ�ṩ��дʱ������ͬ�����������裩
                options.DefaultMessageHandlerAsyncEvent = DefaultMessageHandlerAsyncEvent.SelfSynicMethod;
            });
            //ʹ�� С���� MessageHandler �м��
            app.UseMessageHandlerForWxOpen("/WxOpenAsync", CustomWxOpenMessageHandler.GenerateMessageHandler, options =>
            {
                options.DefaultMessageHandlerAsyncEvent = DefaultMessageHandlerAsyncEvent.SelfSynicMethod;
                options.AccountSettingFunc = context => senparcWeixinSetting.Value;
            });


            // ��תhttps
            //app.UseHttpsRedirection();
            // ʹ�þ�̬�ļ�
            app.UseStaticFiles();

            // �ȿ�����֤
            app.UseAuthentication();
            // Ȼ������Ȩ�м��
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    "areas",
                    "{area:exists}/{controller=Default}/{action=Index}/{id?}"
                );

                endpoints.MapControllerRoute(
                    "default",
                    "{controller=Home}/{action=Index}/{id?}");
            });

            //����Ĭ����ʼҳ����default.html��
            //�˴���·���������wwwroot�ļ��е����·��
            //var defaultFilesOptions = new DefaultFilesOptions();
            //defaultFilesOptions.DefaultFileNames.Clear();
            //defaultFilesOptions.DefaultFileNames.Add("index.html");
            //app.UseDefaultFiles(defaultFilesOptions);
            //app.UseStaticFiles();
        }
    }
}