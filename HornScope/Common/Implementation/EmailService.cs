using HornScope.Common.Interface;
using HornScope.Common.Models.settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace HornScope.Common.Implementation
{
    public class EmailService : IEmailService
    {
        public const string LogoContentId = "vcp-logo";

        private readonly Mailsetting _smtpSettings;
        private readonly IRazorViewEngine _razorViewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly IWebHostEnvironment _env;

        public EmailService(
            IOptions<Mailsetting> smtpSettings,
            ITempDataProvider tempDataProvider,
            IRazorViewEngine razorViewEngine,
            IServiceProvider serviceProvider,
            IWebHostEnvironment env)
        {
            _smtpSettings = smtpSettings.Value;
            _tempDataProvider = tempDataProvider;
            _razorViewEngine = razorViewEngine;
            _serviceProvider = serviceProvider;
            _env = env;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string viewNamePath, object model)
        {
            try
            {
                using var client = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port)
                {
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password),
                    EnableSsl = _smtpSettings.EnableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    TargetName = "STARTTLS/" + _smtpSettings.Host
                };

                var htmlContent = await RenderRazorViewToStringAsync(viewNamePath, model);
                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(_smtpSettings.SenderEmail, _smtpSettings.SenderName),
                    Subject = subject,
                    Body = "Veridian Climate Pulse notification.",
                    IsBodyHtml = false
                };

                mailMessage.To.Add(toEmail);

                var htmlView = AlternateView.CreateAlternateViewFromString(htmlContent, Encoding.UTF8, MediaTypeNames.Text.Html);
                AttachLogo(htmlView);
                mailMessage.AlternateViews.Add(htmlView);

                await Task.Run(() => client.Send(mailMessage));

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        private void AttachLogo(AlternateView htmlView)
        {
            var logoPath = Path.Combine(_env.WebRootPath, "assets", "images", "vcp.png");
            if (!File.Exists(logoPath))
            {
                return;
            }

            var logoResource = new LinkedResource(logoPath, "image/png")
            {
                ContentId = LogoContentId,
                TransferEncoding = TransferEncoding.Base64
            };
            logoResource.ContentType.Name = "vcp.png";
            htmlView.LinkedResources.Add(logoResource);
        }

        private async Task<string> RenderRazorViewToStringAsync(string viewName, object model)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.RequestServices = _serviceProvider;

            var actionContext = new ActionContext(
                httpContext,
                new RouteData(),
                new ControllerActionDescriptor()
            );

            var viewResult = _razorViewEngine.GetView(executingFilePath: null, viewName, isMainPage: true);

            if (!viewResult.Success)
                throw new InvalidOperationException($"View {viewName} not found.");

            var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = model
            };

            using var stringWriter = new StringWriter();
            var viewContext = new ViewContext(
                actionContext,
                viewResult.View,
                viewDictionary,
                new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
                stringWriter,
                new HtmlHelperOptions()
            );

            await viewResult.View.RenderAsync(viewContext);
            return stringWriter.ToString();
        }
    }
}
