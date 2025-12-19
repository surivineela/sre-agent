using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Session.Identity.Attributes;

/// <summary>
/// A feature provider that filters controllers based on the SessionModeAttribute.
/// </summary>
public class SessionModeControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    private readonly SessionMode _currentMode;

    public SessionModeControllerFeatureProvider(SessionMode currentMode)
    {
        _currentMode = currentMode;
    }

    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        // Remove controllers that don't match the current mode
        var controllersToRemove = feature.Controllers
            .Where(controller =>
            {
                var modeAttribute = controller.GetCustomAttribute<SessionModeAttribute>();
                if (modeAttribute == null)
                {
                    // No attribute means available in all modes
                    return false;
                }

                // Check if the controller's mode includes the current mode
                return (modeAttribute.Mode & _currentMode) == 0;
            })
            .ToList();

        foreach (var controller in controllersToRemove)
        {
            feature.Controllers.Remove(controller);
        }
    }
}
