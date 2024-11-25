using RazorEngine.Configuration;
using RazorEngine.Templating;
using RazorEngine.Text;
using System;

namespace TaskReminder
{
    [Serializable]
    public class Renderer : MarshalByRefObject, IDisposable
    {
        public Renderer _localInstance = null;
        public AppDomain _domain = null;
        public event EventHandler<RenderErrorEventArgs> ErrorOccurred;

        public Renderer()
        {
            if (AppDomain.CurrentDomain.IsDefaultAppDomain())
            {
                string proxyAppDomainName = $"RazorEngineProxyDomain-{Guid.NewGuid()}";
                _domain = AppDomain.CreateDomain(proxyAppDomainName, null, AppDomain.CurrentDomain.SetupInformation);
                Type type = typeof(Renderer);
                _localInstance = (Renderer)_domain.CreateInstanceAndUnwrap(type.Assembly.FullName, type.FullName);
            }
        }

        public void Dispose()
        {
            if (_domain != null)
            {
                _localInstance.Dispose();
                AppDomain.Unload(_domain);
                _domain = null;                
            }
        }

        public string Render(string template, UserMailData userMailData)
        {
            _localInstance.ErrorOccurred += ErrorOccurred;
            return _localInstance.RenderHtml(template, userMailData);
        }

        // Needs to use diffrent instance of RazorEngineService to avoid threading issues
        public string RenderHtml(string template, UserMailData userMailData)
        {
            string compiled = null;

            try
            {
                TemplateServiceConfiguration templateConfig = new TemplateServiceConfiguration();
                templateConfig.EncodedStringFactory = new RawStringFactory();
                templateConfig.DisableTempFileLocking = true;
                using (IRazorEngineService service = RazorEngineService.Create(templateConfig))
                {
                    compiled = service.RunCompile(template, "templateKey", null, userMailData);
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex, userMailData);
            }

            return compiled;
        }

        protected virtual void OnErrorOccurred(Exception ex, UserMailData userMailData)
        {
            RenderErrorEventArgs renderErrorEventArgs = new RenderErrorEventArgs() { Exception = ex, UserMailData = userMailData };
            ErrorOccurred?.Invoke(this, renderErrorEventArgs); // Raise the event if there are any subscribers
        }

        [Serializable]
        public class RenderErrorEventArgs
        {
            public Exception Exception { get; set; }
            public UserMailData UserMailData { get; set; }
        }
    }
}
