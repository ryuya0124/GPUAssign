// Only UI metadata is substituted; discovery, persistence, sync and registry code
// are compiled directly from production sources, without starting WinUI.
namespace Microsoft.UI.Xaml.Media { public class ImageSource { } }
namespace GPUAssign.Services
{
    public static class StoreAppService
    {
        public static string GetStoreAppDisplayName(string id) => id;
    }
}
