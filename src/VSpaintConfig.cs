using Vintagestory.API.Common;

namespace VSpaint
{
    public class VSpaintConfig
    {
        public const string FileName = "vspaint.json";

        public bool EnableDryingMechanics { get; set; } = false;
        public double DryAfterHours { get; set; } = 3.0;

        public static VSpaintConfig Current { get; private set; } = new VSpaintConfig();

        public static void Load(ICoreAPI api)
        {
            VSpaintConfig loaded = null;
            try { loaded = api.LoadModConfig<VSpaintConfig>(FileName); }
            catch (System.Exception ex)
            {
                api.Logger.Warning("[VSpaint] Failed to load {0}, using defaults: {1}", FileName, ex.Message);
            }

            if (loaded == null)
            {
                Current = new VSpaintConfig();
                api.StoreModConfig(Current, FileName);
            }
            else
            {
                Current = loaded;
            }

            // Guard against junk values that would make drying behave weirdly:
            // 0 dries instantly, negative compares always-true.
            if (Current.DryAfterHours <= 0)
            {
                api.Logger.Warning("[VSpaint] DryAfterHours must be > 0, got {0}; falling back to 3.0", Current.DryAfterHours);
                Current.DryAfterHours = 3.0;
                api.StoreModConfig(Current, FileName);
            }
        }
    }
}
