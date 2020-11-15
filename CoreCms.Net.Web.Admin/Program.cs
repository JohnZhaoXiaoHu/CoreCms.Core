using System;
using Autofac.Extensions.DependencyInjection;
using CoreCms.Net.Loging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;
using LogLevel = NLog.LogLevel;

namespace CoreCms.Net.Web.Admin
{
    /// <summary>
    ///     ��ʼ��
    /// </summary>
    public class Program
    {
        /// <summary>
        ///     ��������
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
            //NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();
            //CreateHostBuilder(args).Build().Run();

            var host = CreateHostBuilder(args).Build();
            try
            {
                using (var scope = host.Services.CreateScope())
                {
                    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                    //��ȡ��appsettings.json�е������ַ���
                    var sqlString = configuration.GetSection("ConnectionStrings:SqlServerConnection").Value;
                    //ȷ��NLog.config�������ַ�����appsettings.json��ͬ��
                    NLogUtil.EnsureNlogConfig("NLog.config", sqlString);
                }

                //throw new Exception("�����쳣");//for test
                //������Ŀ����ʱ��Ҫ��������
                NLogUtil.WriteDbLog(LogLevel.Trace, LogType.Web, "��վ�����ɹ�");
                NLogUtil.WriteFileLog(LogLevel.Trace, LogType.Web, "��վ�����ɹ�");

                host.Run();
            }
            catch (Exception ex)
            {
                //ʹ��nlogд��������־�ļ�����һ���ݿ�û����/���ӳɹ���
                var errorMessage = "��վ�����ɹ���ʼ�������쳣";
                NLogUtil.WriteFileLog(LogLevel.Error, LogType.Web, errorMessage, new Exception(errorMessage, ex));
                NLogUtil.WriteDbLog(LogLevel.Error, LogType.Web, errorMessage, new Exception(errorMessage, ex));
                throw;
            }
        }

        /// <summary>
        ///     ��������֧��
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .UseServiceProviderFactory(new AutofacServiceProviderFactory()) //<--NOTE THIS
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders(); //�Ƴ��Ѿ�ע���������־�������
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace); //������С����־����
                })
                .UseNLog() //NLog: Setup NLog for Dependency injection
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .ConfigureKestrel(serverOptions =>
                        {
                            serverOptions.AllowSynchronousIO = true; //����ͬ�� IO
                        })
                        .UseStartup<Startup>();
                });
        }
    }
}