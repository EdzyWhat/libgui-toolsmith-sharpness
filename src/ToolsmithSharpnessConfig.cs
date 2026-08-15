using System;
using System.Reflection;

namespace LibGuiToolsmithSharpness;

/// <summary>
/// Reads the player's Toolsmith sharpness-bar *display* preferences so our LibGUI bar colours the
/// fill exactly the way standalone Toolsmith would. Toolsmith draws the bar in one of three modes,
/// chosen by two client-config booleans plus an int gradient selector:
///
/// <list type="bullet">
///   <item><b>Gradient</b> - <c>UseGradientForSharpnessInstead</c>: a smooth ramp built from one of
///     three palettes (<c>GradientSelection</c> 0/1/2).</item>
///   <item><b>All sections</b> - <c>ShowAllSharpnessBarSections</c>: five flat colour segments.</item>
///   <item><b>Flat band</b> (default) - a single flat colour picked by which sharpness band the ratio
///     falls in.</item>
/// </list>
///
/// We only ever <b>read</b> those config values via reflection - the colour palettes themselves are
/// hard-coded in <see cref="SharpnessPalette"/> (copied from Toolsmith source) so we need no
/// Toolsmith.dll reference. If Toolsmith's fields ever move/rename, reflection fails softly and we
/// fall back to Toolsmith's own defaults (both booleans false, selection 0 -> flat bands).
/// </summary>
public static class ToolsmithSharpnessConfig
{
    public enum SharpnessMode
    {
        /// <summary>Single flat colour chosen by band (Toolsmith default).</summary>
        Bands,
        /// <summary>Smooth gradient ramp.</summary>
        Gradient,
        /// <summary>Five flat colour segments.</summary>
        Sections
    }

    private const string ModSystemType = "Toolsmith.ToolsmithModSystem";
    private const string ClientConfigType = "Toolsmith.Config.ToolsmithClientConfigs";

    private static bool _resolved;
    private static bool _available;
    private static FieldInfo? _clientConfigField;   // static field on ToolsmithModSystem
    private static FieldInfo? _gradientSelectionField; // static int on ToolsmithModSystem
    private static FieldInfo? _useGradientField;    // instance bool on ToolsmithClientConfigs
    private static FieldInfo? _showSectionsField;   // instance bool on ToolsmithClientConfigs

    /// <summary>
    /// The player's current sharpness render mode + gradient palette selection. Read live each call
    /// (the config can be toggled at runtime); cheap because the reflected members are cached.
    /// Returns <see cref="SharpnessMode.Bands"/> / 0 if Toolsmith isn't reflectable.
    /// </summary>
    public static (SharpnessMode mode, int gradientSelection) Read()
    {
        Resolve();
        if (!_available)
        {
            return (SharpnessMode.Bands, 0);
        }

        try
        {
            int gradientSelection = _gradientSelectionField?.GetValue(null) is int g ? g : 0;

            object? config = _clientConfigField!.GetValue(null);
            if (config == null)
            {
                return (SharpnessMode.Bands, gradientSelection);
            }

            bool useGradient = _useGradientField!.GetValue(config) is bool ug && ug;
            if (useGradient)
            {
                return (SharpnessMode.Gradient, gradientSelection);
            }

            bool showSections = _showSectionsField!.GetValue(config) is bool ss && ss;
            if (showSections)
            {
                return (SharpnessMode.Sections, gradientSelection);
            }

            return (SharpnessMode.Bands, gradientSelection);
        }
        catch
        {
            return (SharpnessMode.Bands, 0);
        }
    }

    private static void Resolve()
    {
        if (_resolved)
        {
            return;
        }

        _resolved = true;

        try
        {
            Type? modSystem = FindType(ModSystemType);
            Type? clientConfig = FindType(ClientConfigType);
            if (modSystem == null || clientConfig == null)
            {
                return;
            }

            _clientConfigField = modSystem.GetField("ClientConfig", BindingFlags.Public | BindingFlags.Static);
            _gradientSelectionField = modSystem.GetField("GradientSelection", BindingFlags.Public | BindingFlags.Static);
            _useGradientField = clientConfig.GetField("UseGradientForSharpnessInstead", BindingFlags.Public | BindingFlags.Instance);
            _showSectionsField = clientConfig.GetField("ShowAllSharpnessBarSections", BindingFlags.Public | BindingFlags.Instance);

            // GradientSelection is optional (falls back to 0); the config field + both booleans are required.
            _available = _clientConfigField != null && _useGradientField != null && _showSectionsField != null;
        }
        catch
        {
            _available = false;
        }
    }

    private static Type? FindType(string fullName)
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type? t = asm.GetType(fullName, throwOnError: false);
                if (t != null)
                {
                    return t;
                }
            }
            catch
            {
                // Some dynamic assemblies throw on GetType; skip them.
            }
        }

        return null;
    }
}
