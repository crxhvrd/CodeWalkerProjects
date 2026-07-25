using System;
using System.Windows.Forms;

namespace CodeWalker.OivsPacker
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Game data is culture-neutral (floats always use '.'). Pin the invariant
            // culture so a comma-decimal locale can't corrupt values written into
            // packaged game files. See the installer's Program.Main for detail.
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = invariant;
            System.Threading.Thread.CurrentThread.CurrentCulture = invariant;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.Run(new PackerForm());
        }
    }
}
