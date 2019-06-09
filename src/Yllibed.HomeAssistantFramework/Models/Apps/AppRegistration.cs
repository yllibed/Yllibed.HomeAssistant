using System;
using System.Linq;
using System.Text.RegularExpressions;
using HomeAssistant.AppStarter.Extensions;

namespace HomeAssistant.AppStarter.Models.Apps
{
    internal partial class AppRegistration
    {
        private readonly Regex _entityRegex;

        internal static AppRegistration TryCreate(Type appType)
        {
            try
            {
                // TODO: Replace this to support ctor injection.
                if (Activator.CreateInstance(appType) is IHassApp app)
                {
                    if (app.TriggeredByEntities != null && app.TriggeredByEntities.Any())
                    {
                        return new AppRegistration(app);
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        public  IHassApp App { get; }

        public AppRegistration(IHassApp app)
        {
            App = app;

            var patterns = app.TriggeredByEntities
                .Split(',')
                .Select(e => '(' + e.ToLowerInvariant().Replace(" ", "").WildcardToRegexExpression() + ')')
                .ToArray();

            var regex = string.Join("|", patterns);

            _entityRegex = new Regex(
                regex,
                RegexOptions.Compiled
                | RegexOptions.CultureInvariant
                | RegexOptions.ExplicitCapture
                | RegexOptions.IgnoreCase
                | RegexOptions.Singleline);
        }

        public bool MatchEntity(string entityId)
        {
            return _entityRegex.IsMatch(entityId);
        }
    }
}
