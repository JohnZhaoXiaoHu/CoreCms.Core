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
using CoreCms.Net.Utility.Extensions;
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

            //ע�븽���洢����
            services.Configure<FilesStorageOptions>(Configuration.GetSection("FilesStorage"));

            //Swagger�ӿ��ĵ�ע��
            services.AddAdminSwaggerSetup();

            //jwt��Ȩ֧��ע��
            services.AddAuthorizationSetupForAdmin();
            //������ע��
            services.AddHttpContextSetup();

            //���������м���AutoFac�������滻����
            services.Replace(ServiceDescriptor.Transient<IControllerActivator, ServiceBasedControllerActivator>());

            //ע��mvc��ע��razor������ͼ
            services.AddMvc(options =>
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
                });

            //����ͨ��������
            services.Configure<KxtSmsOptions>(Configuration.GetSection("KXTSMS"));

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
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
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
            // ��תhttps
            //app.UseHttpsRedirection();
            // ʹ�þ�̬�ļ�
            app.UseStaticFiles();
            // ʹ��cookie
            app.UseCookiePolicy();
            // ���ش�����
            app.UseStatusCodePages();
            // Routing
            app.UseRouting();
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
            var defaultFilesOptions = new DefaultFilesOptions();
            defaultFilesOptions.DefaultFileNames.Clear();
            defaultFilesOptions.DefaultFileNames.Add("index.html");
            app.UseDefaultFiles(defaultFilesOptions);
            app.UseStaticFiles();
        }
    }
}