using System.Globalization;
using System.Runtime.CompilerServices;

namespace Scottplotz.Rendering;

internal static class InvariantCulture
{
    [ModuleInitializer]
    internal static void Apply()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
    }
}
